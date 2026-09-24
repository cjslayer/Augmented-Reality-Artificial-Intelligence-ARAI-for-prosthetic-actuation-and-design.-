#if UNITY_EDITOR
// Scripted gates for the articulated hand (Editor Play mode only; compiled out of players). Attaches itself to the active
// ArmGraspAgent when Temp/gates.json exists at scene load, switches the behavior to HeuristicOnly (actions zero: the harness
// drives the joints through the agent's diagnostic setters and the rig's velocity drives) and writes one CSV row per trial.
// modes
//   calibrate  step response of one finger group (index base): rise time / overshoot for the reference (omega, zeta), the
//              flexion direction relative to the palm normal, and the sign convention of the drive angle
//   lift       object placed on the palm (kinematic, on the pedestal), fingers driven to enveloping / pinch targets, the
//              real contact gate (or a forced transition after settleSteps) releases the object, the shoulder raises the
//              hand until the object bottom is liftClearance + liftExtra above the pedestal, then holds for holdSteps under
//              the agent's own perturbation schedule (scale per trial): G1 / G2 / G3
//   stability  theta sweep (randomizeByDefault on) with random target walks for stepsPerTrial steps: NaN, max joint speed,
//              max penetration into the kinematic object: G4
//   push       object released between the fingers, all drives to their closing limits at the force limit, then an
//              external force through the hand: max penetration and whether the object escapes through the fingers: G6
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Unity.MLAgents.Policies;

[DefaultExecutionOrder(-100)]
public class ArticulatedGates : MonoBehaviour
{
    [System.Serializable]
    public class Config
    {
        public string mode = "lift";
        public string csv = "Temp/gates.csv";
        public float[] masses = { 0.6f };
        public float[] scales = { 0f };
        public string[] grips = { "envelope" };
        public int seeds = 1;
        public int settleSteps = 150;       // steps of closing before the transition is forced if the gate has not fired
        public int liftBudget = 400;        // steps allowed for the lift
        public int holdSteps = 500;         // 50 decisions at 10 ms
        public int holdDecisions = 50;      // agent hold requirement (decisions) for the gates
        public float liftExtra = 0.015f;    // raise until bottom >= liftClearance + this
        public float liftSpeedDeg = 20f;    // shoulder flexion velocity (deg/s) during the lift
        public float palmGap = 0.0005f;
        public float placeUp = 0.072f;      // object centre this far from the palm pivot along the finger direction (0 = over the palm box centre)
        public float timeScale = 20f;
        public int stepsPerTrial = 2000;    // stability
        public int draws = 20;              // stability
        public float pushForce = 60f;       // push: external force (N) on the object toward the palm normal, then away
        public int maxTrialSteps = 4000;
        public int debugEvery = 0;
        public bool freezeAtHold = false;
        public int dumpAt = 0;              // step within the trial at which to dump every joint and freeze (0 = off)
        public bool ignoreSelfCollision = false;   // diagnostic A/B only: rebuild the rig with every intra-hand pair ignored (spike-1 behaviour)
        public float fixedTimestep = 0f;    // > 0: physics timestep for this run (DecisionPeriod = 0.1 s / dt); step-count fields above are in physics steps
        public int solverIterations = 0, solverVelocityIterations = 0;   // > 0: override the rig's solver iterations for this run
        public float contactOffset = 0f;    // > 0: override the rig's contact offset (m) for this run
        // contact-penetration tuning levers (spike 6): 0 = keep the current value
        public int objectSolverIterations = 0, objectSolverVelocityIterations = 0;   // the object's Rigidbody (project default 6 / 1)
        public float maxDepenetrationVelocity = 0f;   // m/s, applied to the object Rigidbody, every hand link and Physics.defaultMaxDepenetrationVelocity
        // exploit suite (run 011, step 1)
        public int cycleHoldSteps = 0;      // > 0: after this many harness hold steps open every group (regrasp-cycling attempt: the drop rule must end the episode)
        public float edgeBottom = 0f;       // > 0: lift only until the object bottom is this high (m) above the pedestal, hold the window there, then continue to the normal height
        public bool blockAgentGate = true;  // false: let the agent's own contact gate release the object during the scripted close (gate-timing check)
        public bool noLift = false;         // true: the arm never lifts (the trial ends by the agent's budget / drop rules)
        public bool noClose = false;        // true: fingers stay open (balance / clamp checks)
        public string placement = "palm";   // palm | forearm (object on the forearm's palmar side next to the wrist) | palmUp (object standing on the pronated palm)
        public float wristFlexDeg = 0f, wristPronDeg = 0f;   // wrist setpoints applied before placement
        public int preposeSteps = 0;        // steps to let the wrist reach its setpoints before the object is placed
        // held-out evaluation of a trained policy (mode "eval"): the agent runs its own episodes (InferenceOnly), each seeded with Random.InitState
        public string modelPath = "";       // .onnx under results/ (imported through the Editor importer into Assets/Models/Watch)
        public int seedFrom = 5001, seedTo = 5100;
        public float objectFriction = -1f;  // >= 0: override the agent's object friction for this run (pass-vs-mu)
        public float evalPerturbScale = 1f; // perturbation scale applied through perturbScaleOverride
        public int evalHoldDecisions = 50;
        public string headMode = "sampled";   // "sampled" = the policy's stochastic head (the training-time behaviour); "deterministic" = the ONNX mean head (BehaviorParameters.DeterministicInference)
        public int passIndex = 0;             // enters the per-episode seed hash: two passes with the same (seed, passIndex) are meant to be identical; change it for an independent replicate
        public int jobWorkers = 0;            // > 0: Unity job-system worker thread count for this Play session (PhysX simulation tasks run on it); 1 = single-threaded physics, the reproducibility setting; 0 = leave the default
        public bool legacyNoReseed = false;   // discriminator only: the pre-1716818 seeding (UnityEngine.Random.InitState(raw seed) per episode, NO Inference Engine reseed); theta snapshot, headMode and jobWorkers stay in effect
        // fixed theta (BO Part B, 2026-09-24): every episode's morphology is pinned through the Academy's environment parameters, the trainer's own path
        // (MorphologyManager.ApplyForEpisode: present keys win: morph/randomize 0, morph/len_*, morph/k_*, morph/b_*, morph/I_*, morph/mask_*), and the link masses
        // are scaled by thetaInertia right after the rig rebuild, before the episode's first physics step (ArticulatedGatesLate, execution order +100), because
        // ApplyForEpisode resets inertiaScale to 1 before the rebuild when not randomizing (MorphologyManager.cs:159). Works in eval and in the scripted modes.
        public bool thetaFixed = false;
        public float[] thetaLength = null;   // 5 link-length scales (index, middle, ring, pinky, thumb)
        public float[] thetaK = null;        // 16 joint stiffnesses k (N m/rad), MorphologyManager group order (14 finger groups, wrist flexion, wrist pronation)
        public float[] thetaZeta = null;     // 16 damping ratios (b = 2 zeta sqrt(k I))
        public float[] thetaInertia = null;  // 16 inertia scales (link masses x scale, as MorphologyManager.Sample draws them)
        public string thetaMask = "";        // 14 chars '0' / '1': finger-group actuation mask
        public float massMin = 0f, massMax = 0f;   // > 0: object-mass range for this run through the environment parameters mass/min, mass/max (log-uniform draw as in training; the observation normalisation keeps the agent's serialized range)
        public bool trainingBudgets = false; // true: keep the agent's training episode limits (lift budget 200, margin 100, MaxStep 5000) in scripted modes
        public float[] envelopeTargets = null;   // 14 closing targets (deg) for the envelope grip; null = k_Envelope
        public float[] pinchTargets = null;      // 14 closing targets (deg) for the pinch grip; null = k_Pinch
        public float stiffnessScale = 0f;   // > 0: diagnostic, multiply the reference k of every group for this run (non-randomized modes)
        public int dynamicFromStep = 0;     // > 0: diagnostic, make the object dynamic (force the agent's transition) at this step of the close instead of at the gate
        public bool dumpAtSlip = false;     // dump joints, contacts and renders the first time the released object moves faster than 0.03 m/s (the moment of slip)
        // close-until-contact + preload controller (spike 4): each joint group flexes at closeRateDegPerSec until ITS link touches the object,
        // then holds contactAngle + preloadDeg beyond the contact; groups that never touch stop at the power-grasp caps. Pinch = index + thumb only.
        public bool contactController = true;
        public float closeRateDegPerSec = 90f;
        public float preloadDeg = 12f;
        public float capBaseDeg = 60f, capMiddleDeg = 70f, capEndDeg = 30f, capThumbBaseDeg = 45f, capThumbEndDeg = 45f;
        public int preloadSettleSteps = 20; // steps after the preload target is set before the group counts as done
        public bool thumbActive = true;     // diagnostic: false = the thumb stays at its (abducted) rest pose and does not take part in the close
    }
    public static ArticulatedGates Instance;
    Config cfg; ArmGraspAgent agent; ArticulatedHand hand; MorphologyManager mm; BehaviorParameters bp;
    Transform cyl; Collider cylCol; Rigidbody cylRb; Transform palm;
    struct Trial { public float mass, scale; public string grip; public int seed; }
    readonly List<Trial> trials = new List<Trial>();
    int trial = -1, lastCompleted, lastSuccess, stepsInTrial, phaseStep, warmup = 2; string phase = "idle";
    readonly StringBuilder sb = new StringBuilder();
    float restY, objRadius = 0.025f, liftSign = 1f, maxPen, maxSpeed, maxAbsAngle; bool nanSeen; int liftStartStep = -1, holdStart = -1, gateStep = -1, transitionStep = -1;
    readonly List<float> penSamples = new List<float>(); float trialWallStart;
    int edgeHoldMax = -1; bool edgeDone; float shapingAgentAtT = float.NaN, shapingTelescopedAtT = float.NaN; bool cycled; int cycleAtStep = -1;
    float CurrentPenetration()
    {   // deepest overlap (m) between any hand link (groups, extra links, palm) and the object this step
        float worst = 0f;
        void Test(ArticulationBody b) { if (b == null) return; var c = b.GetComponent<Collider>(); if (c != null && Physics.ComputePenetration(c, c.transform.position, c.transform.rotation, cylCol, cyl.position, cyl.rotation, out _, out float d) && d > worst) worst = d; }
        foreach (var b in hand.Groups) Test(b); foreach (var (eb, _) in hand.ExtraLinks) Test(eb); Test(hand.Palm);
        return worst;
    }
    string PenStats()
    {
        if (penSamples.Count == 0) return "penWindow n=0";
        var s = new List<float>(penSamples); s.Sort();
        return "penWindow n=" + s.Count + " max=" + (s[s.Count - 1] * 1000f).ToString("F2") + "mm p95=" + (s[(int)(0.95f * (s.Count - 1))] * 1000f).ToString("F2") + "mm median=" + (s[s.Count / 2] * 1000f).ToString("F2") + "mm";
    }
    // calibrate
    readonly List<float> resp = new List<float>(); float tipDot0; Vector3 tipRel0;
    float[] walkTarget = new float[ArmGraspAgent.GroupCount]; float[] armAct = new float[3];
    readonly float[] peakTorque = new float[ArmGraspAgent.GroupCount + 2];
    string ctrlSummaryAtClose = "";   // |drive torque| peak per finger group (+ wrist flex, pron) over the trial

    static readonly float[] k_Envelope = { -75f, -85f, -60f, -75f, -85f, -60f, -75f, -85f, -60f, -75f, -85f, -60f, 45f, 60f };
    static readonly float[] k_Pinch    = { -60f, -80f, -60f, -60f, -80f, -60f, 5f, 0f, 0f, 5f, 0f, 0f, 45f, 60f };   // index + middle + thumb; ring / pinky open

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        const string path = "Temp/gates.json";
        if (!File.Exists(path)) return;
        Config c; try { c = JsonUtility.FromJson<Config>(File.ReadAllText(path)); } catch (System.Exception e) { Debug.LogError("[Gates] bad config: " + e.Message); return; }
        foreach (var a in FindObjectsByType<ArmGraspAgent>(FindObjectsSortMode.None)) if (a.isActiveAndEnabled) { var h = a.gameObject.AddComponent<ArticulatedGates>(); h.cfg = c; Instance = h; if (c.thetaFixed) a.gameObject.AddComponent<ArticulatedGatesLate>(); return; }
        Debug.LogError("[Gates] no active ArmGraspAgent");
    }

    void Start()
    {
        agent = GetComponent<ArmGraspAgent>(); mm = GetComponent<MorphologyManager>(); bp = GetComponent<BehaviorParameters>();
        bp.BehaviorType = BehaviorType.HeuristicOnly;
        if (cfg.holdDecisions > 0) agent.requiredHoldDecisions = cfg.holdDecisions;   // gate: 50 decisions of hold (5 s at 0.1 s)
        stepsPer10ms = 1f;
        {   // object-side solver settings and depenetration velocity (levers 2 and 4)
            var cgo = GameObject.FindGameObjectWithTag("Cylinder"); var crb = cgo != null ? cgo.GetComponent<Rigidbody>() : null;
            if (crb != null && cfg.objectSolverIterations > 0) crb.solverIterations = cfg.objectSolverIterations;
            if (crb != null && cfg.objectSolverVelocityIterations > 0) crb.solverVelocityIterations = cfg.objectSolverVelocityIterations;
            if (cfg.maxDepenetrationVelocity > 0f) { Physics.defaultMaxDepenetrationVelocity = cfg.maxDepenetrationVelocity; if (crb != null) crb.maxDepenetrationVelocity = cfg.maxDepenetrationVelocity; if (agent.Hand != null) agent.Hand.maxDepenetrationVelocity = cfg.maxDepenetrationVelocity; }
            Debug.Log("[Gates] contact settings: object iters=" + (crb != null ? crb.solverIterations + "/" + crb.solverVelocityIterations : "-") + " maxDepen=" + Physics.defaultMaxDepenetrationVelocity + " contactOffset=" + (agent.Hand != null ? agent.Hand.contactOffset : -1f) + " handIters=" + (agent.Hand != null ? agent.Hand.solverIterations + "/" + agent.Hand.solverVelocityIterations : "-"));
        }
        if (agent.Hand != null) { if (cfg.solverIterations > 0) agent.Hand.solverIterations = cfg.solverIterations; if (cfg.solverVelocityIterations > 0) agent.Hand.solverVelocityIterations = cfg.solverVelocityIterations; if (cfg.contactOffset > 0f) { agent.Hand.contactOffset = cfg.contactOffset; var cc = GameObject.FindGameObjectWithTag("Cylinder").GetComponent<Collider>(); if (cc != null) cc.contactOffset = cfg.contactOffset; } }
        if (cfg.fixedTimestep > 0f && agent.Hand != null)
        {
            agent.Hand.fixedTimestep = cfg.fixedTimestep; Time.fixedDeltaTime = cfg.fixedTimestep; stepsPer10ms = 0.01f / cfg.fixedTimestep;
            var req = GetComponent<Unity.MLAgents.DecisionRequester>(); if (req != null) req.DecisionPeriod = Mathf.Max(1, Mathf.RoundToInt(0.1f / cfg.fixedTimestep));
            Debug.Log("[Gates] fixedTimestep=" + cfg.fixedTimestep + " DecisionPeriod=" + (req != null ? req.DecisionPeriod : -1));
        }
        if (!cfg.trainingBudgets) { agent.taskBudgetMarginSteps = Mathf.RoundToInt(700 * stepsPer10ms); agent.liftBudgetSteps = Mathf.RoundToInt(600 * stepsPer10ms); agent.MaxStep = 20000; }   // gates: the task budget is derived from K (lift budget + hold + margin); the scripted lift is slower than the policy's   // the scene's episode cap would end a slow scripted lift + 500-step hold as "maxStep"
        if (cfg.jobWorkers > 0) { jobWorkers0 = Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount; Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount = cfg.jobWorkers; Debug.Log("[Gates] job workers " + jobWorkers0 + " -> " + Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount + " (max " + Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerMaximumCount + ")"); }
        if (cfg.ignoreSelfCollision && agent.Hand != null) { agent.Hand.ignoreAllSelfCollision = true; Debug.LogWarning("[Gates] diagnostic: self-collision ignored for this run"); }
        if (cfg.mode == "push") { agent.dropDistance = 10f; agent.dropTiltDeg = 180f; agent.taskBudgetMarginSteps = 5000; agent.liftBudgetSteps = 5000; }   // push: the agent must not end the episode
        if (cfg.mode != "stability") { mm.randomizeByDefault = false; for (int f = 0; f < MorphologyManager.FingerCount; f++) mm.lengthScale[f] = 1f; for (int g = 0; g < MorphologyManager.FingerGroupCount; g++) mm.mask[g] = true; }
        else mm.randomizeByDefault = true;
        if (cfg.stiffnessScale > 0f && cfg.mode != "stability") { for (int g = 0; g < MorphologyManager.GroupCount; g++) { mm.stiffness[g] = mm.ReferenceStiffness(g) * cfg.stiffnessScale; mm.damping[g] = 2f * mm.referenceZeta * Mathf.Sqrt(mm.stiffness[g] * mm.inertia[g]); } Debug.LogWarning("[Gates] diagnostic: reference stiffness x " + cfg.stiffnessScale + " (indexBase k=" + mm.stiffness[0].ToString("F2") + ")"); }
        cyl = GameObject.FindGameObjectWithTag("Cylinder").transform; cylCol = cyl.GetComponent<Collider>(); cylRb = cyl.GetComponent<Rigidbody>();
        foreach (var grip in cfg.grips) foreach (var m in cfg.masses) foreach (var sc in cfg.scales) for (int sd = 0; sd < Mathf.Max(1, cfg.seeds); sd++) trials.Add(new Trial { mass = m, scale = sc, grip = grip, seed = sd });
        if (cfg.mode == "stability") { trials.Clear(); for (int i = 0; i < cfg.draws; i++) trials.Add(new Trial { mass = 0.6f, scale = 0f, grip = "walk", seed = i }); }
        if (cfg.mode == "audit") { trials.Clear(); trials.Add(new Trial { mass = 0.6f, scale = 0f, grip = "audit", seed = 0 }); }
        if (cfg.mode == "eval") { trials.Clear(); for (int sd = cfg.seedFrom; sd <= cfg.seedTo; sd++) trials.Add(new Trial { mass = 0f, scale = cfg.evalPerturbScale, grip = "policy", seed = sd }); EvalSetup(); }
        if (cfg.massMin > 0f && cfg.massMax > 0f) { SetEnvParam("mass/min", cfg.massMin); SetEnvParam("mass/max", cfg.massMax); Debug.Log("[Gates] mass range " + cfg.massMin + "-" + cfg.massMax + " kg (environment parameters)"); }
        if (cfg.thetaFixed && cfg.mode != "eval") ArmGraspAgent.BeforeEpisodeBegin = ApplyFixedTheta;   // eval installs its own hook (which calls ApplyFixedTheta) at the end of the warm-up
        Time.timeScale = cfg.timeScale;
        if (cfg.mode != "eval") sb.AppendLine("trial,mode,grip,mass,scale,seed,success,endReason,steps,gateStep,transitionStep,liftStart,stepsToLift,holdSteps,pulses,maxPulseN,weightN,contactsEnd,palm,forearm,bottomAboveTop,maxPenMm,maxJointSpeedDeg,nan,gripTorqueMean,retPhase,retHold,retBonus,retDrop,note");
        Flush();
        Debug.Log("[Gates] mode=" + cfg.mode + " trials=" + trials.Count + " dt=" + Time.fixedDeltaTime);
    }

    // ---- eval mode: policy runs its own episodes; the harness only seeds them (BeforeEpisodeBegin hook) and records one row per episode ----
    int evalIndex = -1; bool evalReady; float stepsPer10ms = 1f; int jobWorkers0 = -1;
    // per-episode seed: every harness-owned draw (theta, mass, spawn, pulse schedule; all through UnityEngine.Random) and the policy's sampled head
    // are re-seeded at the top of OnEpisodeBegin with DerivedSeed(seed, passIndex) = FNV-1a/murmur-style mix of (seed, 1000, passIndex), so an
    // episode is a function of (build, seed sequence, passIndex, headMode, jobWorkers); mu is not in the hash (common random numbers across mu)
    static int DerivedSeed(int seed, int pass)
    {   // mix(seed, passIndex): the middle word is the constant 1000 (it was round(mu * 1000) until 2026-09-23), so passes at every mu share theta and pulse draws
        // and the old-hash mu 1.0 passes (results/012/eval_n300, results/012/checks/determinism/repro2_*) stay reproducible
        unchecked
        {
            uint h = 2166136261u;
            foreach (uint v in new[] { (uint)seed, 1000u, (uint)pass }) { h ^= v; h *= 16777619u; h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15; }
            return (int)h;
        }
    }
    // theta of the episode whose row is pending: captured in the hook (top of OnEpisodeBegin, before the next episode's draws), because
    // EvalRow runs after the next episode has already re-sampled MorphologyManager (the old live read logged the NEXT episode's theta)
    string snapMask = ""; float[] snapLen = new float[5]; float snapK, snapZeta, snapInertia, snapSpan; int snapActive;
    // decision phase (measurement, 2026-09-24): Academy.StepCount at the reset and that count modulo the scene DecisionRequester's period. The DecisionRequester
    // requests a decision at the Academy steps whose pre-step count is a multiple of the period (DecisionStep 0), and the hook runs after the step counter has
    // advanced, so phaseAtBegin = 0 means the first decision comes at the very next step and phase p means (period - p) zero-action steps precede it.
    // Captured in the hook for the episode that begins; shifted to the row of the episode that ended. warmupSteps = the count at the first seeded episode.
    int beginAcademyStep = -1, beginPhase = -1, rowAcademyStep = -1, rowPhase = -1, warmupAcademySteps = -1;
    void SnapshotPhase()
    {
        rowAcademyStep = beginAcademyStep; rowPhase = beginPhase;
        int c = Unity.MLAgents.Academy.Instance.StepCount; var req = GetComponent<Unity.MLAgents.DecisionRequester>(); int period = req != null ? Mathf.Max(1, req.DecisionPeriod) : 1;
        beginAcademyStep = c; beginPhase = c % period;
        if (warmupAcademySteps < 0) { warmupAcademySteps = c; Debug.Log("[Gates] eval: first seeded episode at Academy step " + c + " DecisionPeriod=" + period + " phase=" + beginPhase); }
    }
    void SnapshotTheta()
    {
        snapMask = ""; for (int g = 0; g < MorphologyManager.FingerGroupCount; g++) snapMask += mm.mask[g] ? "1" : "0";
        for (int f = 0; f < 5; f++) snapLen[f] = mm.lengthScale[f];
        snapZeta = 0f; snapInertia = 0f; for (int g = 0; g < MorphologyManager.FingerGroupCount; g++) { snapZeta += mm.DampingRatio(g) / MorphologyManager.FingerGroupCount; snapInertia += mm.inertiaScale[g] / MorphologyManager.FingerGroupCount; }
        snapK = mm.MeanStiffness(0, MorphologyManager.FingerGroupCount); snapActive = mm.ActiveCount; snapSpan = agent.HandSpanRatio;
    }   // physics steps per 10 ms under the dt override (1 at the training dt)
    void EvalSetup()
    {
        mm.randomizeByDefault = true;
        agent.MaxStep = Mathf.RoundToInt(5000 * stepsPer10ms); agent.taskBudgetMarginSteps = Mathf.RoundToInt(100 * stepsPer10ms); agent.liftBudgetSteps = Mathf.RoundToInt(200 * stepsPer10ms);   // evaluation runs the training episode limits (budget derived from K), not the scripted-hold ones set above; scaled to the dt override so they are identical in wall time
        if (stepsPer10ms != 1f)
        {   // every other step-denominated agent constant, converted so that its wall-time value is unchanged (pulse duration, hold-pay window and total, per-step penalty weights); the hold requirement K x DecisionPeriod needs the scene's DecisionPeriod = 0.1 s / dt, which the agent caches at Initialize
            agent.perturbPulseSteps = Mathf.RoundToInt(agent.perturbPulseSteps * stepsPer10ms); agent.holdRewardBudgetSteps = Mathf.RoundToInt(agent.holdRewardBudgetSteps * stepsPer10ms); agent.holdRewardPerStep /= stepsPer10ms; agent.effortWeight /= stepsPer10ms; agent.safetyWeight /= stepsPer10ms;
            Debug.Log("[Gates] eval dt override: stepsPer10ms=" + stepsPer10ms + " MaxStep=" + agent.MaxStep + " liftBudget=" + agent.liftBudgetSteps + " margin=" + agent.taskBudgetMarginSteps + " pulseSteps=" + agent.perturbPulseSteps + " holdPayBudget=" + agent.holdRewardBudgetSteps + " DecisionPeriod(agent cache)=" + agent.DecisionPeriodCached);
        }
        agent.requiredHoldDecisions = cfg.evalHoldDecisions; agent.perturbScaleOverride = cfg.evalPerturbScale;
        if (cfg.objectFriction >= 0f) agent.objectFriction = cfg.objectFriction;
        if (cfg.objectFriction >= 0f)
        {   // the agent builds the object's PhysicsMaterial once at Initialize (before this Start), so the field alone never reached the physics: apply the override to the live material
            var cyl = GameObject.FindGameObjectWithTag("Cylinder"); var col = cyl != null ? cyl.GetComponent<Collider>() : null;
            if (col != null && col.sharedMaterial != null) { col.sharedMaterial.staticFriction = cfg.objectFriction; col.sharedMaterial.dynamicFriction = cfg.objectFriction; Debug.Log("[Gates] eval: object material mu=" + col.sharedMaterial.dynamicFriction + " combine=" + col.sharedMaterial.frictionCombine); }
            else Debug.LogError("[Gates] eval: object material not found, friction override NOT applied");
        }
        try
        {
            string assetDir = "Assets/Models/Watch"; Directory.CreateDirectory(assetDir);
            string assetPath = assetDir + "/eval_" + Path.GetFileNameWithoutExtension(cfg.modelPath) + ".onnx";
            File.Copy(cfg.modelPath, assetPath, true);
            UnityEditor.AssetDatabase.ImportAsset(assetPath, UnityEditor.ImportAssetOptions.ForceSynchronousImport);
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<Unity.InferenceEngine.ModelAsset>(assetPath);
            if (asset == null) { Debug.LogError("[Gates] eval: model import failed " + assetPath); return; }
            bp.DeterministicInference = cfg.headMode == "deterministic";   // read when the policy is created below (harness switch only; the scene keeps its serialized value)
            agent.SetModel(bp.BehaviorName, asset, Unity.MLAgents.Policies.InferenceDevice.Default); bp.BehaviorType = BehaviorType.InferenceOnly;
            evalReady = true; Debug.Log("[Gates] eval: model " + cfg.modelPath + " seeds " + cfg.seedFrom + "-" + cfg.seedTo + " mu=" + agent.objectFriction + " perturb=" + cfg.evalPerturbScale + " K=" + cfg.evalHoldDecisions + " head=" + cfg.headMode + " passIndex=" + cfg.passIndex + " deterministicInference=" + bp.DeterministicInference + " legacyNoReseed=" + cfg.legacyNoReseed);
        }
        catch (System.Exception e) { Debug.LogError("[Gates] eval: " + e.Message); }
        sb.Length = 0; sb.AppendLine("episode,seed,success,endReason,steps,transitionStep,stepsToLift,holdSteps,holdNeeded,pulses,maxPulseN,contacts,distinctFingers,thumb,palm,forearm,mass,perturbScale,mu,maxPenMm,gripTorqueMean,retShaping,retPhase,retHold,retBonus,retDrop,lenMean,lenIndex,lenMiddle,lenRing,lenPinky,lenThumb,kFingerMean,zetaMean,inertiaScaleMean,activeGroups,mask,handSpanRatio,dtMs,pulseStep1,pulseStep2,pulseStep3,lastPulseStep,pulseActiveAtDrop,head_mode,maxStepTimeout,academyStepAtBegin,phaseAtBegin,warmupSteps"); Flush();
    }
    static int LastPulseBefore(ArmGraspAgent.EpisodeRecord r) { int best = -1; foreach (int s in new[] { r.pulseStart1, r.pulseStart2, r.pulseStart3 }) if (s >= 0 && s <= r.holdSteps && s > best) best = s; return best; }   // hold step of the last pulse that had started by the final step
    void OnDestroy2() { }
    void EvalRow()
    {
        var r = agent.LastEpisode; int idx = evalIndex - 1; if (idx < 0 || idx >= trials.Count) return;   // the hook has already advanced to the next episode's seed
        bool timeout = r.endReason == "maxStep" && r.steps == 0; if (timeout) r.steps = agent.MaxStep;   // ML-Agents' MaxStep interruption resets StepCount before OnEpisodeBegin logs the record: a real 5000-step episode without a grasp, not an aborted one
        string mask = snapMask; float lenMean = 0f; for (int f = 0; f < 5; f++) lenMean += snapLen[f] / 5f; float zMean = snapZeta, iMean = snapInertia;   // theta of THIS episode (snapshot taken in the hook)
        sb.AppendLine(string.Join(",", new string[] { (idx + 1).ToString(), trials[idx].seed.ToString(), r.success ? "1" : "0", r.endReason, r.steps.ToString(), r.transitionStep.ToString(), r.stepsToLift.ToString(), r.holdSteps.ToString(), (cfg.evalHoldDecisions * agent.DecisionPeriodCached).ToString(), r.pulsesApplied.ToString(), r.maxPulseForce.ToString("F2"), r.contacts.ToString(), r.distinctFingers.ToString(), r.thumb ? "1" : "0", r.palm ? "1" : "0", r.forearm ? "1" : "0", r.mass.ToString("F3"), r.perturbScale.ToString("F2"), agent.objectFriction.ToString("F2"), (r.maxPenetration * 1000f).ToString("F2"), r.gripForceMean.ToString("F3"), r.retShaping.ToString("F4"), r.retPhase.ToString("F2"), r.retHold.ToString("F3"), r.retBonus.ToString("F2"), r.retDrop.ToString("F2"), lenMean.ToString("F3"), snapLen[0].ToString("F3"), snapLen[1].ToString("F3"), snapLen[2].ToString("F3"), snapLen[3].ToString("F3"), snapLen[4].ToString("F3"), snapK.ToString("F3"), zMean.ToString("F3"), iMean.ToString("F3"), snapActive.ToString(), mask, snapSpan.ToString("F3"), (Time.fixedDeltaTime * 1000f).ToString("F1"), r.pulseStart1.ToString(), r.pulseStart2.ToString(), r.pulseStart3.ToString(), LastPulseBefore(r).ToString(), r.pulseActiveAtEnd ? "1" : "0", cfg.headMode, timeout ? "1" : "0", rowAcademyStep.ToString(), rowPhase.ToString(), warmupAcademySteps.ToString() }));
        Flush();
    }

    void FixedUpdate()
    {
        if (cfg.mode == "eval")
        {
            if (!evalReady) return;
            // hook: Random.InitState seeds every harness/agent draw; InferenceEngine.Random.SetSeed seeds the policy's sampled head (RandomNormalLike without a seed attribute draws from the package's static stream; 0 would mean 'default seed')
            if (warmup > 0) { warmup--; PreApplyTheta(); if (warmup == 0) { lastCompleted = agent.CompletedEpisodes; ArmGraspAgent.BeforeEpisodeBegin = () => { SnapshotTheta(); SnapshotPhase(); ApplyFixedTheta(); evalIndex++; if (evalIndex >= 0 && evalIndex < trials.Count) { if (cfg.legacyNoReseed) Random.InitState(trials[evalIndex].seed); else { int d = DerivedSeed(trials[evalIndex].seed, cfg.passIndex); Random.InitState(d); Unity.InferenceEngine.Random.SetSeed(d != 0 ? d : 1); } } }; agent.EndEpisode(); } return; }   // the initial (unseeded) episode is discarded; the hook, installed just before the reset, seeds every following one
            int done = agent.CompletedEpisodes;
            if (done != lastCompleted) { lastCompleted = done; EvalRow(); if (evalIndex >= trials.Count) { ArmGraspAgent.BeforeEpisodeBegin = null; Finish(); } }
            return;
        }
        if (warmup > 0) { warmup--; PreApplyTheta(); if (warmup == 0) { hand = agent.Hand; palm = hand.PalmLink; float halfH = cyl.position.y - cylCol.bounds.min.y; restY = agent.PlatformTop + halfH + agent.restClearance; objRadius = 0.5f * cylCol.bounds.size.x; /* upright object at warmup */ lastCompleted = agent.CompletedEpisodes; lastSuccess = agent.SuccessCount; agent.EndEpisode(); } return; }
        int completed = agent.CompletedEpisodes;
        if (completed != lastCompleted) { bool success = agent.SuccessCount != lastSuccess; lastSuccess = agent.SuccessCount; lastCompleted = completed; EndTrial(success, ""); BeginTrial(); return; }
        if (phase == "idle") return;
        var acts = agent.heuristicActions; for (int i = 0; i < acts.Length; i++) acts[i] = 0f;
        stepsInTrial++; phaseStep++;
        hand = agent.Hand; palm = hand.PalmLink;
        var h = hand.Health(); if (!h.finite) nanSeen = true; if (h.maxJointSpeedDeg > maxSpeed) maxSpeed = h.maxJointSpeedDeg;
        for (int g = 0; g < ArmGraspAgent.GroupCount; g++) peakTorque[g] = Mathf.Max(peakTorque[g], Mathf.Abs(hand.GroupDriveForce(g)));
        if (hand.Palm != null) { var df = hand.Palm.driveForce; for (int i = 0; i < 2 && i < df.dofCount; i++) peakTorque[ArmGraspAgent.GroupCount + i] = Mathf.Max(peakTorque[ArmGraspAgent.GroupCount + i], Mathf.Abs(df[i])); }
        for (int g = 0; g < ArmGraspAgent.GroupCount; g++) maxAbsAngle = Mathf.Max(maxAbsAngle, Mathf.Abs(agent.GetGroupAngle(g)));
        if (agent.MaxPenetration > maxPen) maxPen = agent.MaxPenetration;
        if ((cfg.mode == "lift" && phase == "hold") || (cfg.mode == "push" && (phase == "squeeze" || phase == "pushIn" || phase == "pushOut"))) penSamples.Add(CurrentPenetration());
        if (agent.LastHoldCriterionMet && gateStep < 0) gateStep = agent.StepCount;
        if (agent.Phase != ArmGraspAgent.TaskPhase.Reach && transitionStep < 0) transitionStep = agent.StepCount;
        if (cfg.debugEvery > 0 && stepsInTrial % cfg.debugEvery == 0)
            DbgLog("[Gates] dbg step=" + agent.StepCount + " phase=" + phase + "/" + agent.Phase + " cyl=" + cyl.position.ToString("F3") + " v=" + (cylRb.isKinematic ? "kin" : cylRb.linearVelocity.ToString("F2")) + " bottom=" + (cylCol.bounds.min.y - agent.PlatformTop).ToString("F3") + " contacts=" + agent.CurrentContacts + " palm=" + agent.LastPalmTouching + " grip=" + agent.GripForce.ToString("F2") + "Nm imp=" + agent.FrictionForce.ToString("F1") + "N arm=" + agent.GetArmAngle(0).ToString("F1") + "/" + agent.GetArmAngle(1).ToString("F1") + "/" + agent.GetArmAngle(2).ToString("F1") + "/" + agent.GetArmAngle(3).ToString("F1") + "/" + agent.GetArmAngle(4).ToString("F1") + " idx=" + agent.GetGroupAngle(0).ToString("F0") + "/" + agent.GetGroupAngle(1).ToString("F0") + "/" + agent.GetGroupAngle(2).ToString("F0") + " thb=" + agent.GetGroupAngle(12).ToString("F0") + "/" + agent.GetGroupAngle(13).ToString("F0") + " maxV=" + h.maxJointSpeedDeg.ToString("F0") + " pen=" + (agent.MaxPenetration * 1000f).ToString("F1") + "mm lifted=" + agent.Lifted + " shoulderDrive=" + (hand.Bicep.driveForce.dofCount > 0 ? hand.Bicep.driveForce[0].ToString("F1") : "-") + "/" + hand.Bicep.xDrive.forceLimit + " tv=" + hand.Bicep.xDrive.targetVelocity.ToString("F1") + " elbowDrive=" + (hand.Forearm.driveForce.dofCount > 0 ? hand.Forearm.driveForce[0].ToString("F1") : "-") + " elbow[" + hand.Forearm.xDrive.driveType + " k=" + hand.Forearm.xDrive.stiffness + " c=" + hand.Forearm.xDrive.damping + " tv=" + hand.Forearm.xDrive.targetVelocity.ToString("F1") + " t=" + hand.Forearm.xDrive.target.ToString("F1") + " lim=" + hand.Forearm.xDrive.lowerLimit + "/" + hand.Forearm.xDrive.upperLimit + " jv=" + (hand.Forearm.jointVelocity.dofCount > 0 ? (hand.Forearm.jointVelocity[0] * Mathf.Rad2Deg).ToString("F1") : "-") + " dofs=" + hand.Forearm.dofCount + " jf=" + hand.Forearm.jointFriction + "]" + " wristDrive=" + (hand.Palm.driveForce.dofCount > 0 ? hand.Palm.driveForce[0].ToString("F2") : "-") + " graspDist=" + agent.GraspPointDistance.ToString("F3") + " tilt=" + Vector3.Angle(cyl.up, Vector3.up).ToString("F0") + " palmY=" + palm.position.y.ToString("F3") + " objY=" + cyl.position.y.ToString("F3"));
        if (stepsInTrial > cfg.maxTrialSteps) { Debug.LogWarning("[Gates] trial timeout"); agent.EndEpisode(); return; }
        if (cfg.dumpAt > 0 && stepsInTrial == cfg.dumpAt) { DumpJoints(); RenderCloseups(); }
        if (cfg.dumpAtSlip && !slipDumped && !cylRb.isKinematic && cylRb.linearVelocity.magnitude > 0.03f)
        {
            slipDumped = true; Debug.Log("[Gates] SLIP at step " + stepsInTrial + " (phase " + phase + "/" + agent.Phase + ") objV=" + cylRb.linearVelocity.ToString("F2") + " objInPalm=" + PalmFrame(cyl.position - palm.position).ToString("F3"));
            DumpJoints(); LogContacts("SLIP"); RenderCloseups();
        }
        if (cfg.debugEvery > 0 && stepsInTrial % cfg.debugEvery == 0 && hand.Contacts.Count > 0) LogContacts("contacts");
        var t = trials[trial];
        switch (cfg.mode)
        {
            case "calibrate": Calibrate(); break;
            case "audit": Audit(); break;
            case "lift": Lift(t); break;
            case "stability": Stability(); break;
            case "push": Push(t); break;
        }
    }

    void BeginTrial()
    {
        trial++;
        if (trial >= trials.Count) { Finish(); return; }
        var t = trials[trial];
        hand = agent.Hand; palm = hand.PalmLink;   // the skeleton was rebuilt by OnEpisodeBegin
        Random.InitState(2000 + trial * 7919 + t.seed);
        agent.DiagnosticSetMass(t.mass); agent.DiagnosticSetPerturbScale(t.scale); agent.perturbScaleOverride = t.scale;
        stepsInTrial = 0; phaseStep = 0; maxPen = 0f; maxSpeed = 0f; maxAbsAngle = 0f; nanSeen = false; liftStartStep = -1; holdStart = -1; gateStep = -1; transitionStep = -1; resp.Clear(); slipDumped = false; ControllerReset(); ctrlSummaryAtClose = ""; penSamples.Clear(); trialWallStart = Time.realtimeSinceStartup; if (requiredSegments0 >= 0) { agent.requiredContactSegments = requiredSegments0; requiredSegments0 = -1; }
        for (int g = 0; g < peakTorque.Length; g++) peakTorque[g] = 0f;
        if (cfg.mode == "stability") { phase = "walk"; for (int g = 0; g < ArmGraspAgent.GroupCount; g++) walkTarget[g] = 0f; return; }
        if (cfg.mode == "audit") { phase = "audit"; cyl.SetPositionAndRotation(cyl.position + Vector3.up * 2f, Quaternion.identity); Physics.SyncTransforms(); return; }   // object out of the way
        edgeHoldMax = -1; edgeDone = false; shapingAgentAtT = shapingTelescopedAtT = float.NaN; cycled = false; cycleAtStep = -1;
        if (cfg.preposeSteps > 0 && (cfg.wristFlexDeg != 0f || cfg.wristPronDeg != 0f))
        {   // wrist first, object later (placed by PlaceObject at the end of the prepose window)
            agent.SetArmSetpoint(3, cfg.wristFlexDeg); agent.SetArmSetpoint(4, cfg.wristPronDeg);
            cyl.SetPositionAndRotation(cyl.position + Vector3.up * 2f, Quaternion.identity); Physics.SyncTransforms();
            phase = "prepose"; return;
        }
        PlaceObject(t);
        phase = cfg.mode == "calibrate" ? "step" : "close";
        if (cfg.mode == "calibrate")
        {
            var tip = hand.Groups[2].transform; tipRel0 = tip.position - palm.position; tipDot0 = Vector3.Dot(tipRel0, palm.right);
            agent.SetGroupSetpoint(0, -30f);
        }
    }

    void PlaceObject(Trial t)
    {
        var pb = palm.GetComponent<BoxCollider>(); Vector3 pcen = palm.TransformPoint(pb.center); float half = 0.5f * pb.size.x; float r = objRadius;   // not the AABB: a tumbled object's AABB is its tilted extent
        Vector3 n = palm.right; Vector3 pos = pcen + n * (half + r + cfg.palmGap);
        if (cfg.placeUp > 0f) { Vector3 alongFingers = palm.up; alongFingers.y = 0f; alongFingers.Normalize(); pos += alongFingers * (cfg.placeUp - Vector3.Dot(pcen - palm.position, palm.up)); }
        pos.y = restY;
        float halfH = cyl.position.y - cylCol.bounds.min.y;   // upright half height (object is upright when placed)
        if (cfg.placement == "forearm")
        {   // on the forearm's palmar side, 4 cm proximal of the wrist pivot, resting on the pedestal
            var fc = hand.Forearm.GetComponent<CapsuleCollider>(); float fr = fc != null ? fc.radius : 0.016f;
            pos = palm.position - palm.up * 0.04f + n * (fr + r + cfg.palmGap); pos.y = restY;
        }
        else if (cfg.placement == "palmUp")
        {   // standing on the palm face (the wrist has been pronated so the palm normal points up); the object is NOT on the pedestal
            Vector3 face = pcen + n * half; pos = face + Vector3.up * (halfH + cfg.palmGap); pos.x = face.x; pos.z = face.z;
        }
        float pushed = 0f; while (OverlapsHand(pos, Quaternion.identity) && pushed < 0.06f) { pos += n * 0.0005f; pushed += 0.0005f; }
        cyl.SetPositionAndRotation(pos, Quaternion.identity); Physics.SyncTransforms();
        Debug.Log("[Gates] trial " + (trial + 1) + "/" + trials.Count + " " + cfg.mode + " grip=" + t.grip + " m=" + t.mass + " scale=" + t.scale + ": object at " + pos.ToString("F3") + " (pushed out " + (pushed * 1000f).ToString("F0") + " mm), palm normal " + n.ToString("F2") + " palmPos=" + palm.position.ToString("F3") + " pcen=" + pcen.ToString("F3") + " rebuilds=" + hand.RebuildCount + " arm=" + agent.GetArmAngle(0).ToString("F1") + "/" + agent.GetArmAngle(2).ToString("F1") + "/" + agent.GetArmAngle(3).ToString("F1") + "/" + agent.GetArmAngle(4).ToString("F1") + " step=" + agent.StepCount);
    }

    bool OverlapsHand(Vector3 pos, Quaternion rot)
    {
        for (int g = 0; g < ArmGraspAgent.GroupCount; g++)
        {   // the thumb rests in front of the palm (palmar abduction): it is not an obstacle for placement, the object displaces it and it then lies on the object
            var b = hand.Groups[g]; if (b == null || (cfg.contactController && g >= 12)) continue;
            var c = b.GetComponent<Collider>(); if (c != null && Physics.ComputePenetration(c, c.transform.position, c.transform.rotation, cylCol, pos, rot, out _, out _)) return true;
        }
        var pc = palm.GetComponent<Collider>(); if (pc != null && Physics.ComputePenetration(pc, pc.transform.position, pc.transform.rotation, cylCol, pos, rot, out _, out _)) return true;
        return false;
    }

    // ---- anatomical audit of the generated skeleton (mode "audit"): rest-pose axes, fan angles, flexion signs, limits, proportions;
    //      then fingers to a mid flexion and the thumb to its limits: where the thumb pad lands relative to the index / middle pads ----
    readonly StringBuilder audit = new StringBuilder();
    Vector3[] auditTip0 = new Vector3[ArmGraspAgent.GroupCount];
    Vector3 PF(Vector3 v) => PalmFrame(v);   // (along = fingers, out = palm normal, across = palm.forward)
    static Vector3 AxisWorld(ArticulationBody b) => b.transform.TransformDirection(b.anchorRotation * Vector3.right);
    Vector3 LinkEnd(int g)
    {
        var b = hand.Groups[g]; foreach (Transform c in b.transform) if (c.GetComponent<ArticulationBody>() != null) return c.position;
        var cap = b.GetComponent<CapsuleCollider>(); return b.transform.TransformPoint(new Vector3(cap.center.x, cap.center.y + 0.5f * cap.height, cap.center.z));
    }
    void Audit()
    {
        if (phaseStep == 1)
        {
            audit.Length = 0;
            audit.AppendLine("REST POSE (palm frame: along = palm.up toward the fingers, out = palm.right = palm normal / closing side, across = palm.forward)");
            audit.AppendLine("wrist pivot (palm link) to forearm link: " + (Vector3.Distance(hand.Palm.transform.position, hand.Forearm.transform.position) * 1000f).ToString("F1") + " mm; palm->middleBase " + (Vector3.Distance(hand.Palm.transform.position, hand.Groups[3].transform.position) * 1000f).ToString("F1") + " mm");
            var pb = hand.Palm.GetComponent<BoxCollider>(); audit.AppendLine("palm box size(local) " + (pb.size * 1000f).ToString("F1") + " mm, centre(palm frame) " + (PF(hand.Palm.transform.TransformPoint(pb.center) - hand.Palm.transform.position) * 1000f).ToString("F1") + " mm; thickness along out = " + (pb.size.x * 1000f).ToString("F1") + " mm");
            audit.AppendLine("group,pivot(palm frame mm),axis(palm frame),axisElevationFromPalmPlaneDeg,axisInPlaneAngleFromAcrossDeg,linkDirSplayDeg,linkDirCurlDeg,predictedFlexionDotOut,lowerLimit,upperLimit,linkLen mm,capsuleR mm,capsuleH mm,mass g");
            Vector3 along = palm.up, outN = palm.right, across = palm.forward;
            for (int g = 0; g < ArmGraspAgent.GroupCount; g++)
            {
                var b = hand.Groups[g]; Vector3 piv = b.transform.position; Vector3 r = LinkEnd(g) - piv; Vector3 ax = AxisWorld(b);
                Vector3 axP = PF(ax), rP = PF(r.normalized);
                float elev = Mathf.Asin(Mathf.Clamp(axP.y, -1f, 1f)) * Mathf.Rad2Deg;                    // out component of the axis
                float inPlane = Mathf.Atan2(axP.x, axP.z) * Mathf.Rad2Deg;                                 // 0 = along across, +/- tilt toward along
                float splay = Mathf.Atan2(rP.z, rP.x) * Mathf.Rad2Deg, curl = Mathf.Asin(Mathf.Clamp(rP.y, -1f, 1f)) * Mathf.Rad2Deg;
                bool thumb = g >= 12; Vector3 flexDir = (thumb ? 1f : -1f) * Vector3.Cross(ax, r).normalized;   // displacement of the link end for the flexion sign used by the agent
                var cap = b.GetComponent<CapsuleCollider>(); var d = b.xDrive;
                audit.AppendLine(ArticulatedHand.GroupTags[g] + "," + (PF(piv - palm.position) * 1000f).ToString("F1") + "," + axP.ToString("F2") + "," + elev.ToString("F1") + "," + inPlane.ToString("F1") + "," + splay.ToString("F1") + "," + curl.ToString("F1") + "," + Vector3.Dot(flexDir, outN).ToString("F2") + "," + d.lowerLimit + "," + d.upperLimit + "," + (r.magnitude * 1000f).ToString("F1") + "," + (cap.radius * 1000f).ToString("F1") + "," + (cap.height * 1000f).ToString("F1") + "," + (b.mass * 1000f).ToString("F1"));
                auditTip0[g] = PF(LinkEnd(g) - palm.position);   // palm frame, so the wrist droop between the two measurements cancels
            }
            int[] bases = { 0, 3, 6, 9, 12 }; audit.Append("base pivot spacing mm:");
            for (int i = 0; i < 4; i++) audit.Append(' ').Append(ArticulatedHand.GroupTags[bases[i]]).Append('-').Append(ArticulatedHand.GroupTags[bases[i + 1]]).Append(' ').Append((Vector3.Distance(hand.Groups[bases[i]].transform.position, hand.Groups[bases[i + 1]].transform.position) * 1000f).ToString("F1"));
            audit.AppendLine();
            audit.AppendLine("fan (in-plane axis angle relative to middleBase): index " + FanDeg(0, 3).ToString("F1") + " ring " + FanDeg(6, 3).ToString("F1") + " pinky " + FanDeg(9, 3).ToString("F1") + " thumbBase " + FanDeg(12, 3).ToString("F1") + "; thumb base axis vs finger flexion plane normal (middleBase axis): " + Vector3.Angle(AxisWorld(hand.Groups[12]), AxisWorld(hand.Groups[3])).ToString("F1") + " deg");
            audit.AppendLine("arm limits: shoulderFlex " + agent.shoulderFlexionLimits + " abduction " + agent.shoulderAbductionLimits + " elbow " + agent.elbowFlexionLimits + " wristFlex " + agent.wristFlexionLimits + " wristPron " + agent.wristPronationLimits);
            audit.AppendLine("wrist anchor axes (palm frame): twist " + PF(hand.Palm.transform.TransformDirection(hand.Palm.anchorRotation * Vector3.right)).ToString("F2") + " swingY " + PF(hand.Palm.transform.TransformDirection(hand.Palm.anchorRotation * Vector3.up)).ToString("F2") + " swingZ " + PF(hand.Palm.transform.TransformDirection(hand.Palm.anchorRotation * Vector3.forward)).ToString("F2") + "; elbow axis " + PF(hand.Forearm.transform.TransformDirection(hand.Forearm.anchorRotation * Vector3.right)).ToString("F2") + "; opposition angle " + hand.OppositionAngleDeg.ToString("F1") + " deg");
            // now flex: fingers to a mid grasp, thumb to its flexion limits
            for (int g = 0; g < 12; g++) agent.SetGroupSetpoint(g, g % 3 == 0 ? -45f : g % 3 == 1 ? -45f : -30f);
            agent.SetGroupSetpoint(12, hand.Groups[12].xDrive.upperLimit); agent.SetGroupSetpoint(13, hand.Groups[13].xDrive.upperLimit);
        }
        if (phaseStep == 2 || phaseStep == 20 || phaseStep == 149)
        {
            var d12 = hand.Groups[12].xDrive;
            string maskStr = ""; for (int mg = 0; mg < MorphologyManager.FingerGroupCount; mg++) maskStr += mm.mask[mg] ? "1" : "0";
            audit.AppendLine("  [thumb debug step " + phaseStep + "] randomizing=" + mm.Randomizing + " byDefault=" + mm.randomizeByDefault + " mask=" + maskStr + " active=" + mm.ActiveCount + " completedEpisodes=" + agent.CompletedEpisodes + " mask12=" + mm.mask[12] + " drive12 type=" + d12.driveType + " target=" + d12.target + " k=" + d12.stiffness.ToString("F2") + " lim=" + d12.lowerLimit + "/" + d12.upperLimit + " angle=" + hand.GroupAngle(12).ToString("F1") + " torque=" + hand.GroupDriveForce(12).ToString("F3") + " setpointState=" + agent.GetGroupState(12).ToString("F1") + " anchorAxis(palm)=" + PF(AxisWorld(hand.Groups[12])).ToString("F2"));
        }
        if (phaseStep == 150)
        {
            audit.AppendLine("FLEXED (fingers -45/-45/-30, thumb at its upper limits): link-end displacement dot out (should be > 0 for flexion toward the palm)");
            for (int g = 0; g < ArmGraspAgent.GroupCount; g++)
            {
                Vector3 disp = PF(LinkEnd(g) - palm.position) - auditTip0[g];   // palm frame: x along, y out, z across
                audit.AppendLine("  " + ArticulatedHand.GroupTags[g] + " angle=" + hand.GroupAngle(g).ToString("F1") + " dispDotOut=" + (disp.normalized.y).ToString("F2") + " driftAcross(mm, + = toward the thumb)=" + (disp.z * 1000f).ToString("F1") + " end(palm frame mm)=" + (PF(LinkEnd(g) - palm.position) * 1000f).ToString("F1"));
            }
            Vector3 thumbTip = LinkEnd(13), idxPad = LinkEnd(1), midPad = LinkEnd(4), idxTip = LinkEnd(2);
            audit.AppendLine("thumb tip to index PIP-end " + (Vector3.Distance(thumbTip, idxPad) * 1000f).ToString("F1") + " mm, to index tip " + (Vector3.Distance(thumbTip, idxTip) * 1000f).ToString("F1") + " mm, to middle PIP-end " + (Vector3.Distance(thumbTip, midPad) * 1000f).ToString("F1") + " mm; thumb tip (palm frame mm) " + (PF(thumbTip - palm.position) * 1000f).ToString("F1") + " index tip " + (PF(idxTip - palm.position) * 1000f).ToString("F1"));
            audit.AppendLine("selfPenetration at this pose: " + SelfPenetration());
            audit.AppendLine("thumb drives: base target=" + hand.Groups[12].xDrive.target + " angle=" + hand.GroupAngle(12).ToString("F1") + " torque=" + hand.GroupDriveForce(12).ToString("F2") + " lim=" + hand.Groups[12].xDrive.forceLimit + "; end target=" + hand.Groups[13].xDrive.target + " angle=" + hand.GroupAngle(13).ToString("F1") + " torque=" + hand.GroupDriveForce(13).ToString("F2"));
            File.WriteAllText(cfg.csv, audit.ToString()); Debug.Log("[Gates] audit written to " + cfg.csv);
            RenderCloseups();
            agent.EndEpisode();
        }
    }
    float FanDeg(int g, int refG)
    {
        Vector3 a = AxisWorld(hand.Groups[g]), b = AxisWorld(hand.Groups[refG]); Vector3 n = palm.right;
        a -= n * Vector3.Dot(a, n); b -= n * Vector3.Dot(b, n);
        return Vector3.SignedAngle(b.normalized, a.normalized, n);
    }

    void Calibrate()
    {
        resp.Add(agent.GetGroupAngle(0));
        if (phaseStep == 100)
        {
            // rise (10 % -> 90 % of -30), peak overshoot, settled value
            float target = -30f; int i10 = -1, i90 = -1; float peak = 0f;
            for (int i = 0; i < resp.Count; i++) { float f = resp[i] / target; if (i10 < 0 && f >= 0.1f) i10 = i; if (i90 < 0 && f >= 0.9f) i90 = i; if (f > peak) peak = f; }
            var tip = hand.Groups[2].transform; float tipDot = Vector3.Dot(tip.position - palm.position, palm.right);
            string note = "omegaRef=" + mm.NaturalFrequency(0).ToString("F1") + " zetaRef=" + mm.DampingRatio(0).ToString("F2") + " k=" + mm.stiffness[0].ToString("F3") + "Nm/rad b=" + mm.damping[0].ToString("F4") + " Inominal=" + mm.NominalInertia[0].ToString("E2") + " linkMass=" + hand.Groups[0].mass.ToString("F3") + " dt=" + Time.fixedDeltaTime + " rise10-90=" + ((i90 - i10) * Time.fixedDeltaTime * 1000f).ToString("F0") + "ms (theory ~" + (2.16f / mm.NaturalFrequency(0) * 1000f).ToString("F0") + "ms per-rad at the nominal inertia; " + (2.16f / (mm.NaturalFrequency(0) * Mathf.Sqrt(57.3f)) * 1000f).ToString("F0") + "ms if per-deg) overshoot=" + ((peak - 1f) * 100f).ToString("F1") + "% angle@100=" + resp[resp.Count - 1].ToString("F1") + " tipTowardPalmNormal " + tipDot0.ToString("F3") + "->" + tipDot.ToString("F3") + " (flexion -30 should increase it) driveForce=" + hand.GroupDriveForce(0).ToString("F3") + "Nm";
            Debug.Log("[Gates] calibrate: " + note);
            sb.AppendLine((trial + 1) + ",calibrate,step,0,0,0,1,calib," + stepsInTrial + ",-1,-1,-1,-1,0,0,0,0,0,0,0,0," + (maxPen * 1000f).ToString("F2") + "," + maxSpeed.ToString("F0") + "," + (nanSeen ? 1 : 0) + ",0,0,0,0,0," + note.Replace(",", ";"));
            Flush(); agent.EndEpisode();
        }
    }

    // ---- close-until-contact + preload controller (test driver only; drives the agent's diagnostic setpoint setter) ----
    readonly bool[] ctrlContact = new bool[ArmGraspAgent.GroupCount]; readonly float[] ctrlStop = new float[ArmGraspAgent.GroupCount], ctrlTarget = new float[ArmGraspAgent.GroupCount];
    readonly int[] ctrlDoneStep = new int[ArmGraspAgent.GroupCount]; bool ctrlReported; int requiredSegments0 = -1;
    float CapDeg(int g) => g == 12 ? cfg.capThumbBaseDeg : g == 13 ? cfg.capThumbEndDeg : g % 3 == 0 ? cfg.capBaseDeg : g % 3 == 1 ? cfg.capMiddleDeg : cfg.capEndDeg;
    static bool PinchActive(int g) => g <= 2 || g >= 12;   // index + thumb
    void ControllerReset() { for (int g = 0; g < ArmGraspAgent.GroupCount; g++) { ctrlContact[g] = false; ctrlStop[g] = 0f; ctrlTarget[g] = 0f; ctrlDoneStep[g] = -1; } ctrlReported = false; }
    void ControllerStep(bool pinch)
    {
        float dt = Time.fixedDeltaTime;
        for (int g = 0; g < ArmGraspAgent.GroupCount; g++)
        {
            bool thumb = g >= 12; float sign = thumb ? 1f : -1f;   // flexion sign of the drive angle
            if ((pinch && !PinchActive(g)) || (!cfg.thumbActive && thumb)) { agent.SetGroupSetpoint(g, 0f); ctrlTarget[g] = 0f; ctrlDoneStep[g] = 0; continue; }
            if (!ctrlContact[g] && phase == "close")
            {
                if (agent.IsGroupTouching(g))
                {   // contact: hold preloadDeg beyond the angle at which this link met the object
                    ctrlContact[g] = true; ctrlStop[g] = agent.GetGroupAngle(g); ctrlTarget[g] = ctrlStop[g] + sign * cfg.preloadDeg; ctrlDoneStep[g] = phaseStep + cfg.preloadSettleSteps;
                }
                else
                {
                    float cap = sign * CapDeg(g);
                    ctrlTarget[g] = Mathf.MoveTowards(ctrlTarget[g], cap, cfg.closeRateDegPerSec * dt);
                    if (Mathf.Approximately(ctrlTarget[g], cap) && ctrlDoneStep[g] < 0) ctrlDoneStep[g] = phaseStep + cfg.preloadSettleSteps;
                }
            }
            agent.SetGroupSetpoint(g, ctrlTarget[g]);
        }
    }
    bool ControllerDone() { for (int g = 0; g < ArmGraspAgent.GroupCount; g++) if (ctrlDoneStep[g] < 0 || phaseStep < ctrlDoneStep[g]) return false; return true; }
    string ControllerSummary()
    {
        var s = new StringBuilder("controller:");
        for (int g = 0; g < ArmGraspAgent.GroupCount; g++) s.Append(' ').Append(ArticulatedHand.GroupTags[g]).Append(ctrlContact[g] ? " stop=" + ctrlStop[g].ToString("F1") + " preload->" : " nocontact cap->").Append(ctrlTarget[g].ToString("F1")).Append(" angle=").Append(agent.GetGroupAngle(g).ToString("F1")).Append(" touch=").Append(agent.IsGroupTouching(g) ? 1 : 0).Append(';');
        s.Append(" palm=").Append(agent.LastPalmTouching ? "yes" : "no").Append(" contacts=").Append(agent.CurrentContacts);
        s.Append(" | objInPalm(mm)=").Append((PalmFrame(cyl.position - palm.position) * 1000f).ToString("F0")).Append(" r=").Append((objRadius * 1000f).ToString("F0")).Append(" linkEnds(mm):");
        for (int g = 0; g < ArmGraspAgent.GroupCount; g++) s.Append(' ').Append(ArticulatedHand.GroupTags[g]).Append('=').Append((PalmFrame(LinkEnd(g) - palm.position) * 1000f).ToString("F0"));
        var pbx = palm.GetComponent<BoxCollider>(); s.Append(" palmFace(out mm)=").Append(((Vector3.Dot(palm.TransformPoint(pbx.center) - palm.position, palm.right) + 0.5f * pbx.size.x) * 1000f).ToString("F0"));
        return s.ToString();
    }

    void Lift(Trial t)
    {
        var acts = agent.heuristicActions;
        if (phase == "prepose")
        {
            agent.SetArmSetpoint(3, cfg.wristFlexDeg); agent.SetArmSetpoint(4, cfg.wristPronDeg);
            if (phaseStep >= cfg.preposeSteps) { Debug.Log("[Gates] prepose done: wrist " + agent.GetArmAngle(3).ToString("F1") + "/" + agent.GetArmAngle(4).ToString("F1") + " palmNormal.y=" + palm.right.y.ToString("F2")); PlaceObject(t); phaseStep = 0; phase = "close"; }
            return;
        }
        if (cfg.wristFlexDeg != 0f || cfg.wristPronDeg != 0f) { agent.SetArmSetpoint(3, cfg.wristFlexDeg); agent.SetArmSetpoint(4, cfg.wristPronDeg); }
        if (cfg.noClose) { for (int g = 0; g < ArmGraspAgent.GroupCount; g++) agent.SetGroupSetpoint(g, 0f); }
        else if (cycled) { for (int g = 0; g < ArmGraspAgent.GroupCount; g++) agent.SetGroupSetpoint(g, 0f); }
        else if (cfg.contactController) ControllerStep(t.grip == "pinch");
        else
        {
            float[] targets = t.grip == "pinch" ? (cfg.pinchTargets != null && cfg.pinchTargets.Length == ArmGraspAgent.GroupCount ? cfg.pinchTargets : k_Pinch)
                                                : (cfg.envelopeTargets != null && cfg.envelopeTargets.Length == ArmGraspAgent.GroupCount ? cfg.envelopeTargets : k_Envelope);
            // closing targets ramp at the agent's setpoint rate (via the diagnostic setter: same drives the policy would move)
            for (int g = 0; g < ArmGraspAgent.GroupCount; g++)
            {
                float cur = agent.GetGroupState(g).z; float step = agent.setpointRateDegPerSec * Time.fixedDeltaTime;
                agent.SetGroupSetpoint(g, Mathf.MoveTowards(cur, targets[g], step));
            }
        }
        if (cfg.contactController && cfg.blockAgentGate && phase == "close" && requiredSegments0 < 0) { requiredSegments0 = agent.requiredContactSegments; agent.requiredContactSegments = 99; }   // the agent's own gate must not release the object before the preload is in
        if (cfg.contactController && phase == "close" && !ctrlReported && (ControllerDone() || phaseStep >= cfg.settleSteps - 1)) { ctrlReported = true; ctrlSummaryAtClose = ControllerSummary(); Debug.Log("[Gates] " + ctrlSummaryAtClose); if (cfg.dumpAt < 0) { DumpJoints(); RenderCloseups(); } if (requiredSegments0 >= 0) agent.requiredContactSegments = requiredSegments0; }
        if (phase == "close" && agent.Phase != ArmGraspAgent.TaskPhase.Reach && float.IsNaN(shapingAgentAtT)) { shapingAgentAtT = agent.ShapingReturn; shapingTelescopedAtT = agent.TelescopedShaping(); }
        switch (phase)
        {
            case "close":
                if (cfg.dynamicFromStep > 0 && phaseStep == cfg.dynamicFromStep && agent.Phase == ArmGraspAgent.TaskPhase.Reach) { agent.DiagnosticForceTransition(); Debug.Log("[Gates] diagnostic: object dynamic from step " + phaseStep + " (closing continues to settleSteps)"); break; }
                if (cfg.dynamicFromStep > 0 && phaseStep < cfg.settleSteps) break;   // keep closing on the dynamic object until settleSteps
                if (agent.Phase != ArmGraspAgent.TaskPhase.Reach) { phase = "lift"; phaseStep = 0; liftStartStep = agent.StepCount; liftSign = LiftSign(); Debug.Log("[Gates] released by gate at step " + agent.StepCount + " contacts=" + agent.CurrentContacts + " grip=" + agent.GripForce.ToString("F2") + "Nm liftSign=" + liftSign); }
                else if (phaseStep >= cfg.settleSteps || (cfg.contactController && ctrlReported) || (cfg.noClose && phaseStep >= cfg.settleSteps)) { shapingAgentAtT = agent.ShapingReturn; shapingTelescopedAtT = agent.TelescopedShaping(); agent.DiagnosticForceTransition(); phase = "lift"; phaseStep = 0; liftStartStep = agent.StepCount; liftSign = LiftSign(); Debug.Log("[Gates] transition forced at step " + agent.StepCount + " contacts=" + agent.CurrentContacts + " grip=" + agent.GripForce.ToString("F2") + "Nm liftSign=" + liftSign); }
                break;
            case "lift":
                {
                    float bottom = cylCol.bounds.min.y - agent.PlatformTop;
                    if (cfg.noLift) { acts[ArmGraspAgent.GroupCount] = 0f; if (phaseStep > cfg.holdSteps + 20) { Debug.Log("[Gates] noLift window elapsed; ending"); agent.EndEpisode(); } break; }
                    if (cfg.edgeBottom > 0f && !edgeDone)
                    {   // partial lift: stop at edgeBottom and watch the agent's hold counter over the window
                        if (bottom >= cfg.edgeBottom) { acts[ArmGraspAgent.GroupCount] = 0f; phase = "edgehold"; phaseStep = 0; Debug.Log("[Gates] edge: bottom=" + bottom.ToString("F3") + " (clearance " + agent.liftClearance + ")"); }
                        else if (phaseStep > cfg.liftBudget) { Debug.Log("[Gates] lift budget exhausted: bottom=" + bottom.ToString("F3")); agent.EndEpisode(); }
                        else acts[ArmGraspAgent.GroupCount] = liftSign * cfg.liftSpeedDeg / agent.armRotationSpeed;
                        break;
                    }
                    if (bottom >= agent.liftClearance + cfg.liftExtra) { acts[ArmGraspAgent.GroupCount] = 0f; phase = "hold"; phaseStep = 0; holdStart = agent.StepCount; Debug.Log("[Gates] lifted: bottom=" + bottom.ToString("F3") + " at step " + agent.StepCount + " contacts=" + agent.CurrentContacts + " palm=" + agent.LastPalmTouching); if (cfg.freezeAtHold) { Time.timeScale = 0f; phase = "frozen"; } }
                    else if (phaseStep > cfg.liftBudget) { Debug.Log("[Gates] lift budget exhausted: bottom=" + bottom.ToString("F3")); agent.EndEpisode(); }
                    else acts[ArmGraspAgent.GroupCount] = liftSign * cfg.liftSpeedDeg / agent.armRotationSpeed;
                    break;
                }
            case "edgehold":
                edgeHoldMax = Mathf.Max(edgeHoldMax, agent.HoldStepsNow);
                if (phaseStep >= cfg.holdSteps) { edgeDone = true; phase = "lift"; phaseStep = 0; Debug.Log("[Gates] edge window done: agent hold steps max=" + edgeHoldMax + " phase=" + agent.Phase + "; continuing the lift"); }
                break;
            case "hold":
                if (cfg.cycleHoldSteps > 0 && !cycled && phaseStep >= cfg.cycleHoldSteps) { cycled = true; cycleAtStep = agent.StepCount; Debug.Log("[Gates] cycle: opening the hand after " + phaseStep + " hold steps (agent hold=" + agent.HoldStepsNow + ")"); }
                if (phaseStep >= cfg.holdSteps + 20) { Debug.Log("[Gates] hold window elapsed without the agent ending the episode; ending"); agent.EndEpisode(); }
                break;
        }
    }

    bool slipDumped;
    void LogContacts(string tag)
    {   // contact geometry in the palm frame: along = palm.up (fingers), out = palm.right (palm normal), across = palm.forward
        var sc = new StringBuilder("[Gates] " + tag + ": objInPalm=" + PalmFrame(cyl.position - palm.position).ToString("F3") + " n=" + hand.Contacts.Count);
        foreach (var c in hand.Contacts) sc.Append(' ').Append(c.isPalm ? "palm" : c.isForearm ? "forearm" : ArticulatedHand.GroupTags[c.group]).Append(" p=").Append(PalmFrame(c.point - palm.position).ToString("F3")).Append(" n=").Append(PalmFrame(c.normal).ToString("F2")).Append(" sep=").Append((c.separation * 1000f).ToString("F1")).Append("mm imp=").Append(c.impulse.ToString("F3"));
        Debug.Log(sc.ToString());
    }
    void DbgLog(string s) { Debug.Log(s); try { File.AppendAllText(cfg.csv + ".trace.txt", s + "\n"); } catch (System.Exception) { } }   // the Editor console drops long traces
    Vector3 PalmFrame(Vector3 v) => new Vector3(Vector3.Dot(v, palm.up), Vector3.Dot(v, palm.right), Vector3.Dot(v, palm.forward));
    float LiftSign()
    {   // sign of shoulder flexion velocity that raises the palm: v = omega x r with omega along the bicep's twist axis (its x)
        var bicep = hand.Bicep.transform; Vector3 axis = bicep.right; Vector3 r = palm.position - bicep.position;
        return Vector3.Cross(axis, r).y >= 0f ? 1f : -1f;
    }

    void Stability()
    {
        // random walk of finger and wrist targets every 25 steps, arm velocities random every 50 steps; object stays kinematic on the pedestal
        if (phaseStep % 25 == 1)
            for (int g = 0; g < ArmGraspAgent.GroupCount; g++) { walkTarget[g] += Random.Range(-40f, 40f); walkTarget[g] = Mathf.Clamp(walkTarget[g], -100f, 60f); agent.SetGroupSetpoint(g, walkTarget[g]); }
        if (phaseStep % 50 == 1) { agent.SetArmSetpoint(3, Random.Range(-60f, 60f)); agent.SetArmSetpoint(4, Random.Range(-60f, 60f)); for (int a = 0; a < 3; a++) armAct[a] = Random.Range(-0.33f, 0.33f); }
        for (int a = 0; a < 3; a++) agent.heuristicActions[ArmGraspAgent.GroupCount + a] = armAct[a];
        if (phaseStep >= cfg.stepsPerTrial)
        {
            for (int a = 0; a < 3; a++) agent.heuristicActions[ArmGraspAgent.GroupCount + a] = 0f;
            string note = "omegaMean=" + Mean(mm.NaturalFrequency, MorphologyManager.GroupCount).ToString("F1") + " zetaMean=" + Mean(mm.DampingRatio, MorphologyManager.GroupCount).ToString("F2") + " lenMean=" + Mean(f => mm.lengthScale[f], 5).ToString("F2") + " active=" + mm.ActiveCount + " maxAbsAngle=" + maxAbsAngle.ToString("F0");
            Debug.Log("[Gates] stability draw " + (trial + 1) + ": nan=" + nanSeen + " maxJointSpeed=" + maxSpeed.ToString("F0") + " deg/s maxPen=" + (maxPen * 1000f).ToString("F1") + " mm " + note);
            sb.AppendLine((trial + 1) + ",stability,walk,0.6,0," + trial + "," + (nanSeen ? 0 : 1) + ",walk," + stepsInTrial + ",-1,-1,-1,-1,0,0,0,0," + agent.CurrentContacts + ",0,0,0," + (maxPen * 1000f).ToString("F2") + "," + maxSpeed.ToString("F0") + "," + (nanSeen ? 1 : 0) + ",0,0,0,0,0," + note.Replace(",", ";"));
            Flush(); agent.EndEpisode();
        }
    }
    static float Mean(System.Func<int, float> f, int n) { float s = 0f; for (int i = 0; i < n; i++) s += f(i); return s / n; }

    void Push(Trial t)
    {
        var env = cfg.envelopeTargets != null && cfg.envelopeTargets.Length == ArmGraspAgent.GroupCount ? cfg.envelopeTargets : k_Envelope;
        for (int g = 0; g < ArmGraspAgent.GroupCount; g++) agent.SetGroupSetpoint(g, env[g] * 1.3f);   // beyond the closing targets: drives at the force limit against the object
        switch (phase)
        {
            case "close":
                if (phaseStep >= cfg.settleSteps) { agent.DiagnosticForceTransition(); phase = "squeeze"; phaseStep = 0; }
                break;
            case "squeeze":
                if (phaseStep == 200) { Debug.Log("[Gates] push: squeeze only, maxPen=" + (maxPen * 1000f).ToString("F2") + " mm contacts=" + agent.CurrentContacts + " grip=" + agent.GripForce.ToString("F2") + "Nm"); phase = "pushIn"; phaseStep = 0; }
                break;
            case "pushIn":   // external force toward the palm (into the hand), then away from it (out through the fingers)
                cylRb.AddForce(-palm.right * cfg.pushForce, ForceMode.Force);
                if (phaseStep == 200) { Debug.Log("[Gates] push: " + cfg.pushForce + " N toward the palm for 2 s: maxPen=" + (maxPen * 1000f).ToString("F2") + " mm dropped=" + (agent.Phase == ArmGraspAgent.TaskPhase.Reach)); phase = "pushOut"; phaseStep = 0; }
                break;
            case "pushOut":
                cylRb.AddForce(palm.right * cfg.pushForce, ForceMode.Force);
                if (phaseStep == 200) { Debug.Log("[Gates] push: " + cfg.pushForce + " N away from the palm for 2 s: maxPen=" + (maxPen * 1000f).ToString("F2") + " mm contacts=" + agent.CurrentContacts + " objInPalmNormal=" + Vector3.Dot(cyl.position - palm.position, palm.right).ToString("F3") + " " + PenStats()); agent.EndEpisode(); }
                break;
        }
    }

    void EndTrial(bool success, string note)
    {
        if (phase == "idle" || trial < 0 || trial >= trials.Count) return;
        var t = trials[trial]; var r = agent.LastEpisode;
        if (cfg.mode == "calibrate" || cfg.mode == "stability" || cfg.mode == "audit") { phase = "idle"; return; }
        float weight = t.mass * Physics.gravity.magnitude;
        float wall = Time.realtimeSinceStartup - trialWallStart;
        string exploit = " shapingAgent=" + shapingAgentAtT.ToString("F4") + " telescoped=" + shapingTelescopedAtT.ToString("F4") + (cfg.edgeBottom > 0f ? " edgeHoldMax=" + edgeHoldMax : "") + (cfg.cycleHoldSteps > 0 ? " cycleAt=" + cycleAtStep : "") + " forearmEnd=" + (r.forearm ? 1 : 0) + " gateStep=" + gateStep;
        if (string.IsNullOrEmpty(note)) note = exploit + " " + PenStats() + " wall=" + wall.ToString("F1") + "s stepsPerSec=" + (wall > 0f ? (stepsInTrial / wall).ToString("F0") : "-") + " " + TorqueTable() + " " + ctrlSummaryAtClose;
        sb.AppendLine(string.Join(",", new string[] { (trial + 1).ToString(), cfg.mode, t.grip, t.mass.ToString("F3"), t.scale.ToString("F2"), t.seed.ToString(), success ? "1" : "0", r.endReason, r.steps.ToString(), gateStep.ToString(), r.transitionStep.ToString(), liftStartStep.ToString(), r.stepsToLift.ToString(), r.holdSteps.ToString(), r.pulsesApplied.ToString(), r.maxPulseForce.ToString("F2"), weight.ToString("F2"), r.contacts.ToString(), r.palm ? "1" : "0", r.forearm ? "1" : "0", r.bottomAboveTop.ToString("F4"), (r.maxPenetration * 1000f).ToString("F2"), r.maxJointSpeed.ToString("F0"), nanSeen ? "1" : "0", r.gripForceMean.ToString("F3"), r.retPhase.ToString("F2"), r.retHold.ToString("F3"), r.retBonus.ToString("F2"), r.retDrop.ToString("F2"), note }));
        Flush();
        Debug.Log("[Gates] trial " + (trial + 1) + "/" + trials.Count + " " + cfg.mode + " grip=" + t.grip + " m=" + t.mass + " scale=" + t.scale + " -> " + r.endReason + " hold=" + r.holdSteps + " pulses=" + r.pulsesApplied + " maxF=" + r.maxPulseForce.ToString("F1") + "N contacts=" + r.contacts + " palm=" + r.palm + " maxPen=" + (r.maxPenetration * 1000f).ToString("F1") + "mm maxV=" + r.maxJointSpeed.ToString("F0"));
        phase = "idle";
    }

    /// <summary>Per group: configured k (N m/rad), force limit (N m), peak |drive torque| (N m) this trial; semicolon-separated for the CSV note column.</summary>
    string TorqueTable()
    {
        var s = new StringBuilder("torque:");
        for (int g = 0; g < ArmGraspAgent.GroupCount; g++)
        {
            var b = hand.Groups[g]; if (b == null) continue; var d = b.xDrive;
            s.Append(' ').Append(ArticulatedHand.GroupTags[g]).Append(" k=").Append(d.stiffness.ToString("F3")).Append(" lim=").Append(d.forceLimit.ToString("F2")).Append(" peak=").Append(peakTorque[g].ToString("F3")).Append(';');
        }
        if (hand.Palm != null) { s.Append(" wristFlex k=").Append(hand.Palm.xDrive.stiffness.ToString("F2")).Append(" lim=").Append(hand.Palm.xDrive.forceLimit.ToString("F1")).Append(" peak=").Append(peakTorque[ArmGraspAgent.GroupCount].ToString("F3")).Append("; wristPron k=").Append(hand.Palm.yDrive.stiffness.ToString("F2")).Append(" lim=").Append(hand.Palm.yDrive.forceLimit.ToString("F1")).Append(" peak=").Append(peakTorque[ArmGraspAgent.GroupCount + 1].ToString("F3")).Append(';'); }
        return s.ToString();
    }

    void DumpJoints()
    {
        var sbd = new StringBuilder("[Gates] joint dump step=" + agent.StepCount + "\n");
        for (int g = 0; g < ArgCount; g++)
        {
            var b = hand.Groups[g]; if (b == null) continue; var d = b.xDrive;
            sbd.Append(ArticulatedHand.GroupTags[g]).Append(": angle=").Append(hand.GroupAngle(g).ToString("F1")).Append(" target=").Append(d.target.ToString("F1")).Append(" vel=").Append(hand.GroupVelocity(g).ToString("F1"))
               .Append(" drive=").Append(hand.GroupDriveForce(g).ToString("F3")).Append("Nm k=").Append(d.stiffness.ToString("F0")).Append(" c=").Append(d.damping.ToString("F1")).Append(" lim=").Append(d.lowerLimit).Append("/").Append(d.upperLimit).Append(" fmax=").Append(d.forceLimit).Append(" type=").Append(d.driveType)
               .Append(" touch=").Append(agent.IsGroupTouching(g) ? 1 : 0).Append(" mass=").Append(b.mass.ToString("F3")).Append(" dof=").Append(b.dofCount).Append(" pos=").Append(b.transform.position.ToString("F3")).Append('\n');
        }
        foreach (var (name, b) in new[] { ("bicep", hand.Bicep), ("forearm", hand.Forearm), ("palm", hand.Palm) })
        {
            var jp = b.jointPosition; var jv = b.jointVelocity; var sbj = new StringBuilder();
            for (int i = 0; i < jp.dofCount; i++) sbj.Append((jp[i] * Mathf.Rad2Deg).ToString("F1")).Append("(").Append((jv[i] * Mathf.Rad2Deg).ToString("F1")).Append(") ");
            sbd.Append(name).Append(": joints=").Append(sbj).Append(" x[").Append(b.xDrive.driveType).Append(" k=").Append(b.xDrive.stiffness).Append(" c=").Append(b.xDrive.damping).Append(" tv=").Append(b.xDrive.targetVelocity).Append(" f=").Append(b.xDrive.forceLimit).Append(" lim=").Append(b.xDrive.lowerLimit).Append("/").Append(b.xDrive.upperLimit).Append("] y[").Append(b.yDrive.driveType).Append(" tv=").Append(b.yDrive.targetVelocity).Append("] z[").Append(b.zDrive.driveType).Append(" tv=").Append(b.zDrive.targetVelocity).Append("] twist=").Append(b.twistLock).Append(" swY=").Append(b.swingYLock).Append(" swZ=").Append(b.swingZLock).Append(" mass=").Append(b.mass.ToString("F2")).Append(" pos=").Append(b.transform.position.ToString("F3")).Append('\n');
        }
        sbd.Append("object=").Append(cyl.position.ToString("F3")).Append(" kin=").Append(cylRb.isKinematic).Append(" contacts=").Append(agent.CurrentContacts).Append(" palm=").Append(agent.LastPalmTouching);
        sbd.Append(" selfPenetration=").Append(SelfPenetration()).Append(" selfContactEvents=").Append(hand.SelfContactEvents);
        Debug.Log(sbd.ToString());
    }
    /// <summary>Max penetration depth (mm) over the hand's own collision pairs that are NOT ignored (parent-child, palm-base skipped), with the worst pair.</summary>
    string SelfPenetration()
    {
        var bodies = new List<ArticulationBody>(); bodies.AddRange(hand.Groups); bodies.Add(hand.Palm);
        float worst = 0f; string pair = "none"; int pairs = 0, touching = 0; var over2 = new StringBuilder();
        for (int i = 0; i < bodies.Count; i++) for (int j = i + 1; j < bodies.Count; j++)
        {
            var a = bodies[i]; var b = bodies[j]; if (a == null || b == null) continue;
            if (a.transform.parent == b.transform || b.transform.parent == a.transform) continue;
            int ga = hand.GroupOf(a), gb = hand.GroupOf(b);
            if ((a == hand.Palm && gb >= 0 && hand.ParentOfGroup(gb) < 0) || (b == hand.Palm && ga >= 0 && hand.ParentOfGroup(ga) < 0)) continue;
            var ca = a.GetComponent<Collider>(); var cb = b.GetComponent<Collider>(); if (ca == null || cb == null) continue;
            pairs++;
            if (Physics.ComputePenetration(ca, ca.transform.position, ca.transform.rotation, cb, cb.transform.position, cb.transform.rotation, out _, out float dist))
            { touching++; if (dist > worst) { worst = dist; pair = a.name + "/" + b.name; } if (dist > 0.002f) over2.Append(' ').Append(a.name.Substring(2)).Append('/').Append(b.name.Substring(2)).Append(':').Append((dist * 1000f).ToString("F1")); }
        }
        return (worst * 1000f).ToString("F2") + "mm(" + pair + ";overlappingPairs=" + touching + "/" + pairs + ";over2mm:" + (over2.Length > 0 ? over2.ToString() : " none") + ")";
    }
    const int ArgCount = ArmGraspAgent.GroupCount;

    void RenderCloseups()
    {
        var camGo = new GameObject("CloseupCam"); var cam = camGo.AddComponent<Camera>(); cam.fieldOfView = 40f; cam.nearClipPlane = 0.02f;
        var rt = new RenderTexture(1024, 768, 24); cam.targetTexture = rt;
        Vector3[] offs = { new Vector3(0.22f, 0.09f, 0f), new Vector3(0f, 0.09f, 0.22f), new Vector3(-0.22f, 0.09f, 0f), new Vector3(0.11f, 0.25f, 0.11f) };
        string[] names = { "plusX", "plusZ", "minusX", "top" };
        for (int i = 0; i < offs.Length; i++)
        {
            cam.transform.position = cyl.position + offs[i]; cam.transform.LookAt(cyl.position); cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(1024, 768, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1024, 768), 0, 0); tex.Apply(); RenderTexture.active = prev;
            File.WriteAllBytes("Temp/closeup_" + names[i] + ".png", tex.EncodeToPNG()); Destroy(tex);
        }
        {   // hand views: from the pinky side along the flexion axes (fingers curl in this plane) and from in front of the palm
            Vector3 centre = palm.position + palm.up * 0.09f + palm.right * 0.03f;
            var views = new[] { ("fingerplane", centre - palm.forward * 0.45f, palm.up), ("palmface", centre + palm.right * 0.45f, palm.up), ("thumbside", centre + palm.forward * 0.45f, palm.up) };
            cam.fieldOfView = 35f;
            foreach (var (vn, vp, vu) in views)
            {
                cam.transform.position = vp; cam.transform.LookAt(centre, vu); cam.Render();
                var prev = RenderTexture.active; RenderTexture.active = rt;
                var tex = new Texture2D(1024, 768, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1024, 768), 0, 0); tex.Apply(); RenderTexture.active = prev;
                File.WriteAllBytes("Temp/closeup_" + vn + ".png", tex.EncodeToPNG()); Destroy(tex);
            }
        }
        Destroy(camGo); rt.Release(); Debug.Log("[Gates] closeups written to Temp/closeup_*.png");
    }

    // ---- fixed theta through the ML-Agents environment-parameter channel (the trainer's path; MorphologyManager.ApplyForEpisode: present keys win) ----
    static readonly System.Guid k_EnvParamsChannel = new System.Guid("534c891e-810f-11ea-a9d0-822485860400");   // EnvironmentParametersChannel.k_EnvParamsId
    static System.Reflection.MethodInfo s_ProcessSideChannelData;
    static void SetEnvParam(string key, float value)
    {   // wire format of SideChannelManager.ProcessSideChannelData: [16-byte channel id][int32 length][message]; message = WriteString(key) (int32 length + ASCII), WriteInt32(0 = float), WriteFloat32(value)
        var body = new MemoryStream(); using (var bw = new BinaryWriter(body)) { var kb = System.Text.Encoding.ASCII.GetBytes(key); bw.Write(kb.Length); bw.Write(kb); bw.Write(0); bw.Write(value); }
        var all = new MemoryStream(); using (var w = new BinaryWriter(all)) { w.Write(k_EnvParamsChannel.ToByteArray()); byte[] b = body.ToArray(); w.Write(b.Length); w.Write(b); }
        if (s_ProcessSideChannelData == null) s_ProcessSideChannelData = typeof(Unity.MLAgents.SideChannels.SideChannelManager).GetMethod("ProcessSideChannelData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static, null, new[] { typeof(byte[]) }, null);
        s_ProcessSideChannelData.Invoke(null, new object[] { all.ToArray() });
    }
    bool preApplied, massScalePending, massLogged;
    void ApplyFixedTheta()
    {   // at the top of OnEpisodeBegin (before ApplyForEpisode): I = NominalInertia of the current rig (exact from the second application on, hence the pre-apply reset in the warm-up) x scale, b = 2 zeta sqrt(k I)
        if (cfg == null || !cfg.thetaFixed || mm == null) return;
        mm.randomizeByDefault = false; SetEnvParam("morph/randomize", 0f);
        for (int f = 0; f < MorphologyManager.FingerCount; f++) SetEnvParam("morph/len_" + MorphologyManager.FingerNames[f], cfg.thetaLength[f]);
        for (int g = 0; g < MorphologyManager.GroupCount; g++) { float k = cfg.thetaK[g], I = mm.NominalInertia[g] * cfg.thetaInertia[g]; SetEnvParam("morph/k_" + MorphologyManager.GroupNames[g], k); SetEnvParam("morph/I_" + MorphologyManager.GroupNames[g], I); SetEnvParam("morph/b_" + MorphologyManager.GroupNames[g], 2f * cfg.thetaZeta[g] * Mathf.Sqrt(Mathf.Max(k * I, 0f))); }
        for (int g = 0; g < MorphologyManager.FingerGroupCount; g++) SetEnvParam("morph/mask_" + MorphologyManager.GroupNames[g], cfg.thetaMask[g] == '1' ? 1f : 0f);
        massScalePending = true;
    }
    void PreApplyTheta()
    {   // one discarded reset with theta applied so that the rig (and NominalInertia) already carry the fixed link scales when the first counted episode begins
        if (cfg == null || !cfg.thetaFixed || preApplied || warmup != 1) return;
        preApplied = true; var prev = ArmGraspAgent.BeforeEpisodeBegin; ArmGraspAgent.BeforeEpisodeBegin = ApplyFixedTheta; agent.EndEpisode(); ArmGraspAgent.BeforeEpisodeBegin = prev; lastCompleted = agent.CompletedEpisodes;
    }
    internal void LateFixedUpdate()
    {   // execution order +100: runs after the Academy stepper (whose step rebuilt the rig in OnEpisodeBegin) and before this cycle's physics step
        if (cfg == null || !cfg.thetaFixed || !massScalePending || agent == null || agent.Hand == null) return;
        massScalePending = false; var h = agent.Hand;
        for (int g = 0; g < ArticulatedHand.GroupCount; g++) if (h.Groups[g] != null) h.Groups[g].mass *= cfg.thetaInertia[g];
        foreach (var (eb, eg) in h.ExtraLinks) if (eb != null) eb.mass *= cfg.thetaInertia[eg];
        if (!massLogged) { massLogged = true; Debug.Log("[Gates] theta fixed: lengths " + string.Join("/", System.Array.ConvertAll(cfg.thetaLength, v => v.ToString("F3"))) + " k0=" + mm.stiffness[0].ToString("F3") + " zeta0=" + mm.DampingRatio(0).ToString("F3") + " inertiaScale0=" + mm.inertiaScale[0].ToString("F3") + " indexBase mass=" + (h.Groups[0] != null ? h.Groups[0].mass.ToString("F5") : "-") + " mask=" + cfg.thetaMask + " active=" + mm.ActiveCount + " randomizing=" + mm.Randomizing); }
    }
    void Flush() { try { File.WriteAllText(cfg.csv, sb.ToString()); } catch (System.Exception e) { Debug.LogWarning(e.Message); } }
    void Finish()
    {
        if (jobWorkers0 >= 0) { Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount = jobWorkers0; jobWorkers0 = -1; }
        if (cfg.mode == "audit") { try { File.AppendAllText(cfg.csv, "DONE\n"); } catch (System.Exception e) { Debug.LogWarning(e.Message); } } else { sb.AppendLine("DONE"); Flush(); }
        Debug.Log("[Gates] done: " + cfg.csv); phase = "idle"; enabled = false; Time.timeScale = 1f;
        UnityEditor.EditorApplication.isPlaying = false;
    }
    void OnDestroy() { Time.timeScale = 1f; if (cfg != null && cfg.thetaFixed) ArmGraspAgent.BeforeEpisodeBegin = null; if (jobWorkers0 >= 0) { Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount = jobWorkers0; jobWorkers0 = -1; } }   // the worker count persists across Play sessions in the Editor: always restore
}
[DefaultExecutionOrder(100)]
public class ArticulatedGatesLate : MonoBehaviour { void FixedUpdate() { if (ArticulatedGates.Instance != null) ArticulatedGates.Instance.LateFixedUpdate(); } }   // after the Academy stepper, before the physics step: scales the rebuilt rig's link masses for a fixed theta
#endif
