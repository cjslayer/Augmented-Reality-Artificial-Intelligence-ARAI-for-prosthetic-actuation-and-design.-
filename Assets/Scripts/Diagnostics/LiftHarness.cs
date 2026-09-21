#if UNITY_EDITOR
// Scripted diagnostics for the run-011 lift-and-perturb task (Editor Play mode only; compiled out of players).
// Attaches itself to the active ArmGraspAgent when Temp/lift_harness.json exists at scene load, switches the behavior to
// HeuristicOnly, runs the configured trials at the reference morphology (theta reset before the first trial) and writes
// one CSV row per trial. Arm reach/servo, staged wrap and settle logic follow GraspHarness v5 (results/010_verify/session3).
//
// modes
//   complete  reach + wrap, let the gate fire, servo the arm up by liftHeight, agent runs the hold under perturbation
//   telescope complete without teleport; the CSV carries the analytic telescoped shaping sum next to the agent's return
//   calib     arm raised, object teleported into the hand aloft, forced close (grip = envelope | pinch | pinch3), transition
//             forced, hold under perturbation: survival per (grip, mass, scale, seed)
//   regrasp   complete, then open the hand once lifted; the drop must end the episode (one phase reward, one penalty)
//   shelf     arm raised, fingers half curled, thumb open, object rested on the curled fingers, transition forced (no closure)
//   forearm   arm raised, hand open, object laid across the top of the forearm, transition forced (forearm support only)
//   edge      object placed on the pedestal edge, wrap, no lift (liftOffset = 0) or a partial lift below the threshold
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Unity.MLAgents.Policies;

[DefaultExecutionOrder(-100)]   // before the Academy step: the Jacobian probing rotates and restores bone transforms, which must not happen after the agent has staged its kinematic hand bodies
public class LiftHarness : MonoBehaviour
{
    [System.Serializable]
    public class Config
    {
        public string mode = "complete";
        public string csv = "Temp/lift_harness.csv";
        public float[] yaws = { 0f };
        public float[] masses = { 0.6f };
        public float[] scales = { 1f };
        public string[] grips = { "envelope" };
        public int seeds = 1;
        public float liftHeight = 0.15f;      // complete / regrasp: servo the grasp point up by this after the transition
        public float raiseHeight = 0.30f;     // calib / shelf / forearm: raise the open hand by this above the rest height first
        public float edgeInset = 0.03f;       // edge: object centre this far inside the platform's +x edge
        public float timeScale = 20f;
        public bool teleport = true;
        public int maxTrialSteps = 3000;
        public int debugSteps = 0;            // log object state for this many steps after the transition
        public float liftSpeed = 0.3f;        // cap on the arm velocity actions while lifting (1 = 90 deg/s)
        public float liftRate = 0.04f;        // rise rate (m/s) of the lift servo target
        public bool palmPlace = false;        // complete: after the reach, place the object on the palm surface (power grasp) instead of the nominal grasp point
        public float palmGap = 0.001f;        // palmPlace: gap (m) between the palm surface and the object
        public float reachUp = 0f;            // reach: grasp this far (m) above the object centre (a hanging object is pendulum-stable)
        public bool pauseAtWrap = false;      // pause the Editor when the wrap is done (inspection)
        public int wrapSteps = 100;           // complete / regrasp / edge: steps of staged wrap before lifting
        public float holdGain = 0.3f;         // cap on the arm actions that hold the reach pose during the wrap
        public float gripForceScale = -1f;    // >= 0 overrides the agent's gripForceScale for the run (ablations)
        public float gripForceMax = -1f;      // >= 0 overrides the agent's gripForceMax
    }

    public static LiftHarness Instance;
    Config cfg; ArmGraspAgent agent; MorphologyManager mm; BehaviorParameters bp;
    Transform cyl, palm, shoulder, forearm; Collider cylCol, palmCol, forearmCol; Rigidbody cylRb; Collider platformCol;
    readonly List<Collider> segCols = new List<Collider>();
    Transform[] boneT = new Transform[3]; Vector3[] boneAngles = new Vector3[3];
    static readonly int[] axisBone = { 0, 0, 1, 2, 2 }; static readonly int[] axisIdx = { 0, 2, 0, 0, 1 };
    Vector3 refForward = Vector3.up;

    struct Trial { public float yaw, mass, scale; public string grip; public int seed; }
    readonly List<Trial> trials = new List<Trial>();
    int trial = -1, lastCompleted, lastSuccess, stepsInTrial, stallSteps, retreatLeft, retreats, stepsToRange, phaseStep;
    float lastImproveJ; string phase = "idle"; Vector3 target, liftTarget, holdTarget; bool liftTargetSet, holdTargetSet, opened;
    float[] d0, dLast; float gp0, gpLast; float[] r0 = new float[6], rp = new float[6], rm = new float[6];
    readonly StringBuilder sb = new StringBuilder();
    float restY; int transitionSeen = -1, gateStepSeen = -1;

    // ---- bootstrap ----
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        const string path = "Temp/lift_harness.json";
        if (!File.Exists(path)) return;
        Config c;
        try { c = JsonUtility.FromJson<Config>(File.ReadAllText(path)); } catch (System.Exception e) { Debug.LogError("[LiftHarness] bad config: " + e.Message); return; }
        ArmGraspAgent target = null;
        foreach (var a in FindObjectsByType<ArmGraspAgent>(FindObjectsSortMode.None)) if (a.isActiveAndEnabled) { target = a; break; }
        if (target == null) { Debug.LogError("[LiftHarness] no active ArmGraspAgent"); return; }
        var h = target.gameObject.AddComponent<LiftHarness>(); h.cfg = c; Instance = h;
    }

    void Start()
    {
        agent = GetComponent<ArmGraspAgent>(); mm = GetComponent<MorphologyManager>(); bp = GetComponent<BehaviorParameters>();
        if (agent == null || mm == null) { Debug.LogError("[LiftHarness] agent/MorphologyManager missing"); enabled = false; return; }
        bp.BehaviorType = BehaviorType.HeuristicOnly;
        mm.randomizeByDefault = false;
        for (int f = 0; f < MorphologyManager.FingerCount; f++) mm.lengthScale[f] = 1f;
        for (int g = 0; g < MorphologyManager.FingerGroupCount; g++) mm.mask[g] = true;
        for (int g = 0; g < MorphologyManager.GroupCount; g++) { float I = mm.NominalInertia[g], w = g < MorphologyManager.FingerGroupCount ? 25f : 15f; mm.inertia[g] = I; mm.stiffness[g] = I * w * w; mm.damping[g] = 2f * 0.7f * I * w; }
        cyl = GameObject.FindGameObjectWithTag("Cylinder").transform; cylCol = cyl.GetComponent<Collider>(); cylRb = cyl.GetComponent<Rigidbody>();
        palm = transform.Find(agent.palmPath); shoulder = transform.Find(agent.shoulderPath); forearm = transform.Find(agent.forearmPath);
        palmCol = palm != null ? palm.GetComponent<Collider>() : null; forearmCol = forearm != null ? forearm.GetComponent<Collider>() : null;
        var plat = GameObject.Find(agent.platformName); platformCol = plat != null ? plat.GetComponent<Collider>() : null;
        boneT[0] = shoulder; boneT[1] = forearm; boneT[2] = palm;
        foreach (var tg in MorphologyManager.GroupNames) { if (tg.StartsWith("wrist")) continue; foreach (var go in GameObject.FindGameObjectsWithTag(tg)) if (go.TryGetComponent<Collider>(out var c)) segCols.Add(c); }
        float halfH = cyl.position.y - cylCol.bounds.min.y; restY = agent.PlatformTop + halfH + agent.restClearance;
        foreach (var grip in cfg.grips) foreach (var m in cfg.masses) foreach (var sc in cfg.scales) foreach (var y in cfg.yaws) for (int sd = 0; sd < Mathf.Max(1, cfg.seeds); sd++) trials.Add(new Trial { yaw = y, mass = m, scale = sc, grip = grip, seed = sd });
        lastCompleted = agent.CompletedEpisodes; lastSuccess = agent.SuccessCount;
        Time.timeScale = cfg.timeScale;
        sb.AppendLine("trial,mode,grip,yaw,mass,scale,seed,success,endReason,steps,gateStep,transitionStep,stepsToLift,holdSteps,holdEntries,pulses,maxPulseN,weightN,contactsEnd,distinct,thumb,palm,forearm,bottomAboveTop,retShaping,expectedShaping,retPhase,retHold,retBonus,retDrop,retPenalty,holdStepsPaid,gap,antipodality,spread,stepsToRange,retreats,gripForceMean,gripForceEnd,note");
        Flush();
        Debug.Log("[LiftHarness] mode=" + cfg.mode + " trials=" + trials.Count);
        // the first trial starts two steps in: the Academy force-resets every agent on its first step, which would overwrite a teleport
    }
    int warmup = 2;

    // ---- helpers (GraspHarness v5) ----
    Vector3 GraspPoint => agent.GraspPoint;
    Vector3 PalmNormal => palmCol != null ? palmCol.transform.rotation * Vector3.right : Vector3.right;
    /// <summary>Reach target: the object centre pulled toward the palm by palmPull, so the wrap encloses it nearer the palm than the nominal grasp point.</summary>
    Vector3 ReachTarget() => cyl.position + Vector3.up * cfg.reachUp;
    float DistErr => Vector3.Distance(GraspPoint, ReachTarget());
    float OrientErr => Vector3.Angle(palm.forward, refForward);
    bool Settled() { for (int g = 0; g < ArmGraspAgent.GroupCount; g++) if (Mathf.Abs(agent.GetGroupState(g).y) > 1f) return false; return true; }
    void ArmCommand(float[] acts, float[] dTheta, int spread = 1)
    {   // spread > 1: the correction is realized over that many steps (the decision period repeats each action for 5 steps)
        for (int a = 0; a < 3; a++) acts[ArmGraspAgent.GroupCount + a] = Mathf.Clamp(dTheta[a] / (spread * agent.armRotationSpeed * Time.fixedDeltaTime), -1f, 1f);
        for (int a = 3; a < 5; a++) { acts[ArmGraspAgent.GroupCount + a] = 0f; agent.SetArmSetpoint(a, agent.GetArmState(a).z + Mathf.Clamp(dTheta[a] / spread, -agent.setpointRateDegPerSec * Time.fixedDeltaTime, agent.setpointRateDegPerSec * Time.fixedDeltaTime)); }
    }
    float[] Dists() { var d = new float[segCols.Count]; for (int i = 0; i < segCols.Count; i++) d[i] = Vector3.Distance(segCols[i].ClosestPoint(cyl.position), cylCol.ClosestPoint(segCols[i].transform.position)); return d; }
    void Residual(Vector3 tgt, float[] r) { Vector3 dp = GraspPoint - tgt; Vector3 df = (palm.forward - refForward) * 0.1f; r[0] = dp.x; r[1] = dp.y; r[2] = dp.z; r[3] = df.x; r[4] = df.y; r[5] = df.z; }
    float Norm(float[] r) { float s = 0f; foreach (var v in r) s += v * v; return Mathf.Sqrt(s); }
    void ResidualWithDelta(int axis, float delta, Vector3 tgt, float[] r)
    {
        var bt = boneT[axisBone[axis]]; var saved = bt.localRotation; var angles = boneAngles[axisBone[axis]];
        var baseRot = saved * Quaternion.Inverse(Quaternion.Euler(angles)); angles[axisIdx[axis]] += delta;
        bt.localRotation = baseRot * Quaternion.Euler(angles); Residual(tgt, r); bt.localRotation = saved;
    }
    void RefreshBoneAngles() { boneAngles[0] = new Vector3(agent.GetArmAngle(0), 0f, agent.GetArmAngle(1)); boneAngles[1] = new Vector3(agent.GetArmAngle(2), 0f, 0f); boneAngles[2] = new Vector3(agent.GetArmAngle(3), agent.GetArmAngle(4), 0f); }
    float[] DlsStep(Vector3 tgt, float lambda)
    {
        Residual(tgt, r0); float[,] J = new float[6, 5];
        for (int a = 0; a < 5; a++) { ResidualWithDelta(a, 0.5f, tgt, rp); ResidualWithDelta(a, -0.5f, tgt, rm); for (int i = 0; i < 6; i++) J[i, a] = rp[i] - rm[i]; }
        float[,] A = new float[5, 6];
        for (int a = 0; a < 5; a++) { for (int b = 0; b < 5; b++) { float s = 0f; for (int i = 0; i < 6; i++) s += J[i, a] * J[i, b]; A[a, b] = s + (a == b ? lambda : 0f); } float t = 0f; for (int i = 0; i < 6; i++) t += J[i, a] * r0[i]; A[a, 5] = -t; }
        for (int c = 0; c < 5; c++)
        {
            int piv = c; for (int rr = c + 1; rr < 5; rr++) if (Mathf.Abs(A[rr, c]) > Mathf.Abs(A[piv, c])) piv = rr;
            if (piv != c) for (int k = 0; k < 6; k++) { float tmp = A[c, k]; A[c, k] = A[piv, k]; A[piv, k] = tmp; }
            float d = A[c, c]; if (Mathf.Abs(d) < 1e-12f) continue;
            for (int rr = 0; rr < 5; rr++) { if (rr == c) continue; float f = A[rr, c] / d; for (int k = c; k < 6; k++) A[rr, k] -= f * A[c, k]; }
        }
        float[] dTheta = new float[5]; for (int a = 0; a < 5; a++) dTheta[a] = Mathf.Abs(A[a, a]) > 1e-12f ? A[a, 5] / A[a, a] : 0f; return dTheta;
    }
    static void WrapClose(float[] acts, int step) { acts[0] = acts[3] = acts[6] = acts[9] = -1f; if (step > 30) acts[1] = acts[4] = acts[7] = acts[10] = -1f; if (step > 60) acts[2] = acts[5] = acts[8] = acts[11] = -1f; acts[12] = 1f; acts[13] = 1f; }
    static void Open(float[] acts) { for (int g = 0; g < 12; g++) acts[g] = 1f; acts[12] = -1f; acts[13] = -1f; }
    static void Pinch(float[] acts, int step, int fingers)
    {   // index (+ middle when fingers = 2) staged close, the other fingers open, thumb closed
        Open(acts);
        for (int f = 0; f < fingers; f++) { int b = 3 * f; acts[b] = -1f; if (step > 60) acts[b + 1] = -1f; if (step > 120) acts[b + 2] = -1f; }
        acts[12] = 1f; acts[13] = 1f;
    }
    static void HalfCurl(float[] acts) { acts[0] = acts[3] = acts[6] = acts[9] = -1f; acts[1] = acts[4] = acts[7] = acts[10] = -1f; acts[2] = acts[5] = acts[8] = acts[11] = 1f; acts[12] = -1f; acts[13] = -1f; }

    bool OverlapsHand(Vector3 pos, Quaternion rot)
    {
        if (palmCol != null && Physics.ComputePenetration(palmCol, palmCol.transform.position, palmCol.transform.rotation, cylCol, pos, rot, out _, out _)) return true;
        foreach (var c in segCols) if (Physics.ComputePenetration(c, c.transform.position, c.transform.rotation, cylCol, pos, rot, out _, out _)) return true;
        return false;
    }
    /// <summary>Teleport the (kinematic) object to the grasp point, pushed out along the palm normal until clear of the open hand. Returns push-out (m).</summary>
    float PlaceInHand(float yaw)
    {
        Vector3 nrm = palmCol != null ? palmCol.transform.rotation * Vector3.right : Vector3.right;
        Vector3 pos = GraspPoint; Quaternion rot = Quaternion.Euler(0f, yaw, 0f); float pushed = 0f;
        while (OverlapsHand(pos, rot) && pushed < 0.2f) { pos += nrm * 0.001f; pushed += 0.001f; }
        cyl.SetPositionAndRotation(pos, rot); Physics.SyncTransforms(); return pushed;
    }
    void Teleport(Vector3 pos, Quaternion rot) { cyl.SetPositionAndRotation(pos, rot); Physics.SyncTransforms(); }
    /// <summary>Teleport the (kinematic) object onto the palm surface over the palm centre (axis vertical), pushed out along the palm normal until clear of the open hand. Returns push-out (m).</summary>
    float PlaceOnPalm(float yaw)
    {
        var pb = palm.GetComponent<BoxCollider>(); Vector3 pcen = palm.TransformPoint(pb.center); float half = 0.5f * pb.size.x * palm.lossyScale.x;
        float r = cylCol.bounds.size.x * 0.5f; Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
        Vector3 pos = pcen + PalmNormal * (half + r + cfg.palmGap); float pushed = 0f;
        while (OverlapsHand(pos, rot) && pushed < 0.1f) { pos += PalmNormal * 0.001f; pushed += 0.001f; }
        Teleport(pos, rot); return pushed;
    }

    // ---- trial flow ----
    void BeginTrial()
    {
        trial++;
        if (trial == 0) refForward = palm.forward;
        if (trial >= trials.Count) { Finish(); return; }
        var t = trials[trial];
        Random.InitState(1000 + trial * 7919 + t.seed);
        agent.DiagnosticSetMass(t.mass); agent.DiagnosticSetPerturbScale(t.scale); agent.perturbScaleOverride = t.scale;
        Vector3 spawn = new Vector3(agent.spawnCenter.x, restY, agent.spawnCenter.z);
        if (cfg.mode == "edge" && platformCol != null) spawn = new Vector3(platformCol.bounds.max.x - cfg.edgeInset, restY, platformCol.bounds.center.z);
        if (cfg.teleport || cfg.mode == "edge") Teleport(spawn, Quaternion.Euler(0f, t.yaw, 0f));
        stepsInTrial = 0; stallSteps = 0; retreatLeft = 0; retreats = 0; stepsToRange = -1; phaseStep = 0; lastImproveJ = float.MaxValue; target = cyl.position;
        liftTargetSet = false; holdTargetSet = false; opened = false; transitionSeen = -1; gateStepSeen = -1;
        d0 = Dists(); gp0 = DistErr; dLast = d0; gpLast = gp0;
        phase = (cfg.mode == "calib" || cfg.mode == "shelf" || cfg.mode == "forearm") ? "raise" : "reach";
        if (phase == "raise") target = spawn + Vector3.up * cfg.raiseHeight;
    }

    void EndTrial(bool success, string note)
    {
        if (phase == "idle") return;
        var t = trials[trial]; var r = agent.LastEpisode;
        float floor = agent.shapingFloorDistance; float exp = 0f;
        for (int i = 0; i < d0.Length; i++) exp += agent.distanceRewardScale * (d0[i] - dLast[i]) / Mathf.Max(d0[i], floor);
        exp += agent.palmDistanceRewardScale * (gp0 - gpLast) / Mathf.Max(gp0, floor); exp *= agent.shapingScale;
        float weight = t.mass * Physics.gravity.magnitude;
        sb.AppendLine(string.Join(",", new string[] {
            (trial + 1).ToString(), cfg.mode, t.grip, t.yaw.ToString("F0"), t.mass.ToString("F3"), t.scale.ToString("F2"), t.seed.ToString(), success ? "1" : "0", r.endReason, r.steps.ToString(),
            gateStepSeen.ToString(), r.transitionStep.ToString(), r.stepsToLift.ToString(), r.holdSteps.ToString(), r.holdEntries.ToString(), r.pulsesApplied.ToString(), r.maxPulseForce.ToString("F2"), weight.ToString("F2"),
            r.contacts.ToString(), r.distinctFingers.ToString(), r.thumb ? "1" : "0", r.palm ? "1" : "0", r.forearm ? "1" : "0", r.bottomAboveTop.ToString("F4"),
            r.retShaping.ToString("F5"), exp.ToString("F5"), r.retPhase.ToString("F3"), r.retHold.ToString("F4"), r.retBonus.ToString("F2"), r.retDrop.ToString("F2"), r.retPenalty.ToString("F4"), r.holdStepsPaid.ToString(),
            r.coverageGapDeg.ToString("F0"), r.antipodality.ToString("F2"), r.verticalSpread.ToString("F3"), stepsToRange.ToString(), retreats.ToString(), r.gripForceMean.ToString("F2"), r.gripForceEnd.ToString("F2"), note.Replace(",", ";") }));
        Flush();
        Debug.Log("[LiftHarness] trial " + (trial + 1) + "/" + trials.Count + " " + cfg.mode + " grip=" + t.grip + " m=" + t.mass + " scale=" + t.scale + " -> " + r.endReason + " hold=" + r.holdSteps + " pulses=" + r.pulsesApplied + " maxF=" + r.maxPulseForce.ToString("F1") + "N contacts=" + r.contacts + " " + note);
        phase = "idle";
    }

    void FixedUpdate()
    {
        if (warmup > 0) { warmup--; if (warmup == 0) { lastCompleted = agent.CompletedEpisodes; lastSuccess = agent.SuccessCount; agent.EndEpisode(); } return; }
        int completed = agent.CompletedEpisodes;
        if (completed != lastCompleted) { bool success = agent.SuccessCount != lastSuccess; lastSuccess = agent.SuccessCount; lastCompleted = completed; EndTrial(success, ""); BeginTrial(); return; }
        if (phase == "idle") return;
        var acts = agent.heuristicActions; for (int i = 0; i < acts.Length; i++) acts[i] = 0f;
        RefreshBoneAngles(); stepsInTrial++;
        dLast = Dists(); gpLast = DistErr;
        if (agent.LastHoldCriterionMet && gateStepSeen < 0) gateStepSeen = agent.StepCount;
        if (agent.Phase != ArmGraspAgent.TaskPhase.Reach && transitionSeen < 0) transitionSeen = agent.StepCount;
        if (stepsInTrial > cfg.maxTrialSteps) { Debug.LogWarning("[LiftHarness] trial timeout"); agent.EndEpisode(); return; }
        if (cfg.debugSteps > 0 && transitionSeen < 0 && (stepsInTrial % 10 == 0 || agent.LastHoldCriterionMet))
            Debug.Log("[LiftHarness] pre step=" + agent.StepCount + " phase=" + phase + " err=" + DistErr.ToString("F3") + " orient=" + OrientErr.ToString("F1") + " contacts=" + agent.CurrentContacts + " gate=" + agent.LastHoldCriterionMet + " cyl=" + cyl.position.ToString("F3") + " gp=" + GraspPoint.ToString("F3") + " arm=" + agent.GetArmAngle(0).ToString("F1") + "/" + agent.GetArmAngle(1).ToString("F1") + "/" + agent.GetArmAngle(2).ToString("F1") + "/" + agent.GetArmAngle(3).ToString("F1") + "/" + agent.GetArmAngle(4).ToString("F1") + " stall=" + stallSteps);
        if (transitionSeen >= 0 && agent.StepCount - transitionSeen <= cfg.debugSteps)
            Debug.Log("[LiftHarness] dbg step=" + agent.StepCount + " phase=" + agent.Phase + " cyl=" + cyl.position.ToString("F3") + " rbPos=" + cylRb.position.ToString("F3") + " v=" + cylRb.linearVelocity.ToString("F2") + " w=" + cylRb.angularVelocity.ToString("F1") + " bottom=" + (cylCol.bounds.min.y - agent.PlatformTop).ToString("F4") + " dGP=" + Vector3.Distance(cylRb.position, GraspPoint).ToString("F3") + " gp=" + GraspPoint.ToString("F3") + " contacts=" + agent.CurrentContacts + " grip=" + agent.GripForce.ToString("F1") + "N fric=" + agent.FrictionForce.ToString("F1") + "N netF=" + agent.NetFrictionForce.ToString("F1") + " netS=" + agent.NetSqueezeForce.ToString("F1") + " lifted=" + agent.Lifted + " arm=" + agent.GetArmAngle(0).ToString("F1") + "/" + agent.GetArmAngle(1).ToString("F1") + "/" + agent.GetArmAngle(2).ToString("F1"));
        var t = trials[trial];
        switch (phase)
        {
            case "reach": Reach(acts); break;
            case "settleArm":
                {   // damp the arm to rest with tiny actions (the decision period repeats the last action for up to 4 steps)
                    phaseStep++;
                    float[] dS = DlsStep(ReachTarget(), 1e-5f); ArmCommand(acts, dS, 5);
                    for (int a = 0; a < 3; a++) acts[ArmGraspAgent.GroupCount + a] = Mathf.Clamp(acts[ArmGraspAgent.GroupCount + a], -0.05f, 0.05f);
                    if (phaseStep >= 15) { phase = "grip"; phaseStep = 0; }
                    break;
                }
            case "raise":
                {   // open hand, servo the grasp point to the raised target
                    Open(acts);
                    float[] dTheta = DlsStep(target, 1e-5f); ArmCommand(acts, dTheta);
                    float j = Norm(r0);
                    if (j < lastImproveJ - 1e-5f) { lastImproveJ = j; stallSteps = 0; } else stallSteps++;
                    if (Vector3.Distance(GraspPoint, target) < 0.02f || stallSteps > 60 || stepsInTrial > 800) { phase = "place"; phaseStep = 0; }
                    break;
                }
            case "place":
                {
                    Open(acts); phaseStep++;
                    if (phaseStep < 20) break;   // let the fingers settle open
                    string note = "";
                    if (cfg.mode == "calib") { float push = cfg.palmPlace ? PlaceOnPalm(t.yaw) : PlaceInHand(t.yaw); note = (cfg.palmPlace ? "palm placement, pushOut=" : "pushOut=") + (push * 1000f).ToString("F0") + "mm"; phase = "close"; }
                    else if (cfg.mode == "shelf") { phase = "curl"; }
                    else { PlaceOnForearm(t.yaw); phase = "release"; }
                    phaseStep = 0; Debug.Log("[LiftHarness] placed " + note + " graspPoint=" + GraspPoint.ToString("F3"));
                    break;
                }
            case "curl":
                {   // half-curled fingers, thumb open; then rest the object on top of them (GraspHarness shelf)
                    HalfCurl(acts); phaseStep++;
                    if (phaseStep >= 45 && Settled())
                    {
                        Vector3 top = Vector3.zero; float maxY = float.MinValue; int k = 0;
                        foreach (var c in segCols) { if (c.tag.StartsWith("thumb")) continue; var b = c.bounds; top += new Vector3(b.center.x, 0f, b.center.z); if (b.max.y > maxY) maxY = b.max.y; k++; }
                        top /= k; float halfH = cylCol.bounds.size.y * 0.5f;
                        Teleport(new Vector3(top.x, maxY + halfH + 0.001f, top.z), Quaternion.Euler(0f, t.yaw, 0f));
                        phase = "release"; phaseStep = 0;
                    }
                    break;
                }
            case "close":
                {
                    phaseStep++;
                    if (t.grip == "pinch") Pinch(acts, phaseStep, 1); else if (t.grip == "pinch3") Pinch(acts, phaseStep, 2); else WrapClose(acts, phaseStep);
                    if (phaseStep >= 250 && Settled()) { phase = "release"; phaseStep = 0; }
                    break;
                }
            case "release":
                {   // keep the closure command, force the transition once, then let the agent's phase machine run the hold + perturbation
                    HoldCommand(acts, t);
                    phaseStep++;
                    if (phaseStep == 1) { agent.DiagnosticForceTransition(); Debug.Log("[LiftHarness] released: bottomAboveTop=" + (cylCol.bounds.min.y - agent.PlatformTop).ToString("F3") + " contacts=" + agent.CurrentContacts + " mass=" + agent.ObjectMass + " scale=" + agent.PerturbScaleInEffect); }
                    break;
                }
            case "grip":
                {   // complete / telescope / regrasp / edge: wrap; after the transition servo the arm up by liftHeight (edge: liftOffset from cfg.liftHeight, may be 0)
                    phaseStep++;
                    if (phaseStep == 1 && cfg.palmPlace && agent.Phase == ArmGraspAgent.TaskPhase.Reach)
                    {   // scripted power-grasp placement: the (still kinematic) object on the palm surface over the palm centre, then out along
                        // the normal until it clears the open hand; height stays at the rest height (the palm normal is horizontal)
                        var pb = palm.GetComponent<BoxCollider>(); Vector3 pcen = palm.TransformPoint(pb.center); float half = 0.5f * pb.size.x * palm.lossyScale.x;
                        float r = cylCol.bounds.size.x * 0.5f;
                        Vector3 pos = pcen + PalmNormal * (half + r + cfg.palmGap); pos.y = restY; float pushed = 0f;
                        while (OverlapsHand(pos, cyl.rotation) && pushed < 0.1f) { pos += PalmNormal * 0.001f; pushed += 0.001f; }
                        Teleport(pos, cyl.rotation); d0 = Dists(); gp0 = DistErr;
                        Debug.Log("[LiftHarness] object placed on the palm: gap to palm surface=" + ((Vector3.Dot(cyl.position - pcen, PalmNormal) - half - r) * 1000f).ToString("F0") + " mm (pushed out " + (pushed * 1000f).ToString("F0") + " mm) palmTouch=" + agent.LastPalmTouching);
                    }
                    WrapClose(acts, phaseStep);
                    if (agent.Phase == ArmGraspAgent.TaskPhase.Reach || phaseStep < cfg.wrapSteps)
                    {   // arm still while the fingers wrap
                    }
                    else
                    {   // wrap complete: lift with a constant shoulder-flexion ramp until the object bottom is liftHeight above the pedestal, then hold still
                        if (!liftTargetSet)
                        {
                            liftStart = GraspPoint; liftStep = 0; liftTargetSet = true;
                            var sbA = new StringBuilder(); for (int g = 0; g < ArmGraspAgent.GroupCount; g++) sbA.Append(agent.GetGroupAngle(g).ToString("F0")).Append(g == 13 ? "" : "/");
                            Vector3 rel = palm.InverseTransformPoint(cyl.position);
                            Debug.Log("[LiftHarness] wrap done at step " + agent.StepCount + " contacts=" + agent.CurrentContacts + " grip=" + agent.GripForce.ToString("F1") + "N settled=" + Settled() + " angles=" + sbA + " objInPalm=" + rel.ToString("F3") + " graspOffsetEff=" + agent.EffectiveGraspPointOffset.ToString("F3") + " palmNormal=" + PalmNormal.ToString("F2") + ": lifting");
                            if (cfg.pauseAtWrap) { Time.timeScale = 0f; phase = "frozen"; Debug.Log("[LiftHarness] frozen for inspection"); return; }
                        }
                        LiftCommand(acts);
                        if (cfg.mode == "regrasp" && agent.Lifted && agent.HoldSteps >= 10 && !opened) { opened = true; phase = "open"; phaseStep = 0; }
                    }
                    break;
                }
            case "open":
                {   // regrasp exploit: open the hand aloft; the drop must end the episode. If it somehow does not, try to close again.
                    phaseStep++;
                    if (phaseStep < 100) Open(acts); else WrapClose(acts, phaseStep - 100);
                    LiftCommand(acts);
                    break;
                }
        }
    }

    Vector3 liftStart; int liftStep;
    /// <summary>Servo the grasp point straight up along a slowly rising target (liftRate m/s) to liftHeight + 4 cm, then hold it there.</summary>
    void LiftCommand(float[] acts)
    {
        if (cfg.liftHeight <= 0f) return;
        liftStep++;
        float rise = Mathf.Min(cfg.liftRate * liftStep * Time.fixedDeltaTime, cfg.liftHeight + 0.04f);
        float[] dTheta = DlsStep(liftStart + Vector3.up * rise, 1e-5f); ArmCommand(acts, dTheta, 5);
        for (int a = 0; a < 3; a++) acts[ArmGraspAgent.GroupCount + a] = Mathf.Clamp(acts[ArmGraspAgent.GroupCount + a], -cfg.liftSpeed, cfg.liftSpeed);
    }

    void HoldCommand(float[] acts, Trial t)
    {
        if (cfg.mode == "calib") { if (t.grip == "pinch") Pinch(acts, 999, 1); else if (t.grip == "pinch3") Pinch(acts, 999, 2); else WrapClose(acts, 999); }
        else if (cfg.mode == "shelf") HalfCurl(acts);
        else Open(acts);
    }

    void PlaceOnForearm(float yaw)
    {   // object lying horizontally across the top of the forearm capsule, midway along it
        var b = forearmCol.bounds; Vector3 c = b.center; float r = cylCol.bounds.size.x * 0.5f;
        Vector3 axis = forearm.up; axis.y = 0f; axis = axis.sqrMagnitude > 1e-6f ? axis.normalized : Vector3.forward;
        Vector3 across = Vector3.Cross(axis, Vector3.up).normalized;
        Quaternion rot = Quaternion.FromToRotation(Vector3.up, across);   // capsule axis horizontal, perpendicular to the forearm
        Teleport(new Vector3(c.x, b.max.y + r + 0.002f, c.z), rot);
    }

    void Reach(float[] acts)
    {
        float err = DistErr, orient = OrientErr; int contacts = agent.CurrentContacts;
        if (retreatLeft > 0) { retreatLeft--; if (retreatLeft == 0) target = ReachTarget(); }
        else
        {
            target = ReachTarget();
            bool inRange = err < 0.012f && orient < 6f;
            bool converged = (stallSteps > 40 && err < 0.06f && orient < 30f) || (retreats >= 6 && stallSteps > 20 && err < 0.10f && orient < 30f);
            if (inRange || converged) { stepsToRange = stepsInTrial; phase = "settleArm"; phaseStep = 0; return; }
        }
        float[] dTheta = DlsStep(target, 1e-5f);
        ArmCommand(acts, dTheta);
        float j = Norm(r0);
        if (stepsInTrial > 2500) { agent.EndEpisode(); return; }
        if (j < lastImproveJ - 1e-5f) { lastImproveJ = j; stallSteps = 0; } else stallSteps++;
        if (stallSteps > 20 && contacts > 0 && err > 0.06f && retreatLeft == 0 && retreats < 6)
        {
            retreats++; retreatLeft = 30; stallSteps = 0; lastImproveJ = float.MaxValue;
            Vector3 away = GraspPoint - cyl.position; away.y = 0f; away = away.sqrMagnitude > 1e-6f ? away.normalized : -palm.right;
            target = cyl.position + away * 0.12f + Vector3.up * 0.05f;
        }
    }

    void Flush() { try { File.WriteAllText(cfg.csv, sb.ToString()); } catch (System.Exception e) { Debug.LogWarning(e.Message); } }
    void Finish()
    {
        sb.AppendLine("DONE"); Flush(); Debug.Log("[LiftHarness] done: " + cfg.csv); phase = "idle"; enabled = false; Time.timeScale = 1f;
        UnityEditor.EditorApplication.isPlaying = false;
    }
    void OnDestroy() { Time.timeScale = 1f; }
}
#endif
