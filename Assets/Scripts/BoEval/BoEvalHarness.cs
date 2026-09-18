// Fixed-theta evaluation endpoint for the BO outer loop (tools/bo_eval, Part A).
//
// Attached automatically at scene load when the player is started with `-boEvalJob <job.json>` (BoEvalBootstrap); the
// committed scene (InferenceOnly, deployed model) is used unchanged, and without that argument the component is never
// created. The job pins the morphology theta (link scales, spring omega/zeta/inertia scale, 14-bit mask) through the
// MorphologyManager serialized fields before every episode, with morph/randomize off, runs the seeded episodes and the
// drop test with exactly the mechanics of the recorded reference-hand evaluation
// (results/010/validation/reference_hand/GraspDiagnostic_v5_referenceHand.cs: Random.InitState(seed) before each reset,
// hold = 50 consecutive held steps, then per friction value `dropRepeats` drops from the identical pre-drop state with
// Physics.Simulate inside one FixedUpdate, arm kinematic, platform disabled, pass = displacement < 0.1 m after 2 s), and
// optionally runs a forced-close feasibility oracle first (MorphVerify close mode: object teleported to GraspPoint, backed
// off along the palm normal, staged wrap for `oracleSteps` steps under HeuristicOnly, gate = hold criterion met).
// Outputs in `outDir`: episodes.csv and drops.csv (same columns as the reference evaluation), oracle.json, decisions.csv
// (optional, per-decision vector observation + action for the inference-fidelity check) and done.json when finished.
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using Unity.InferenceEngine;

[System.Serializable]
public class BoEvalJob
{
    public string inferenceCheckInputs;  // set: run the model on this fixed observation set (BoEvalInferenceInputs JSON) and exit; no episodes
    public float[] lengthScale = { 1f, 1f, 1f, 1f, 1f };   // index, middle, ring, pinky, thumb
    public float[] omega;          // 16 impedance groups (14 finger groups + wristFlex, wristPron), rad/s
    public float[] zeta;           // 16 damping ratios
    public float[] inertiaScale;   // 16 multipliers on the geometric nominal inertia at these link scales
    public int[] mask;             // 14 finger groups, 1 = actuated
    public int[] seeds;            // Random.InitState(seed) before each episode reset
    public float[] mu = { 1f };    // drop-test friction levels
    public int dropRepeats = 3;
    public bool deterministic = false;   // BehaviorParameters.DeterministicInference (false = the recorded evaluations' sampling path)
    public bool oracle = true;           // forced-close feasibility check before the episodes
    public int oracleSteps = 300;
    public bool logDecisions = false;    // decisions.csv: per-decision vector observation + stored action
    public int logDecisionsMax = 400;
    public float timeScale = 20f;
    public string outDir;
}

[System.Serializable]
public class BoEvalOracle
{
    public bool ran, gateMet, gateHeldAtEnd;
    public int firstGateStep = -1, maxContacts, finalContacts, activeGroups, steps;
    public float pushOutMm;
}

[System.Serializable]
public class BoEvalDone
{
    public int episodes, holds, discarded, seedsRequested;
    public float elapsedSec;
    public bool deterministic;
    public string error = "";
}

/// <summary>Fixed observation set for the inference-fidelity check: n sets, each input flattened row-major (obs_0 is n x 16 x 13).</summary>
[System.Serializable]
public class BoEvalInferenceInputs
{
    public int n;
    public float[] obs_0, obs_1, obs_2, obs_3, obs_4, obs_5, obs_6, obs_7, obs_8, obs_9, obs_10, obs_11;
}

public static class BoEvalBootstrap
{
    public const string ArgName = "-boEvalJob";
    // Editor: a job path in <project>/Temp/boeval_job.txt attaches the harness at the same point of the Play-mode timeline as the
    // player's command-line argument (the trigger file is consumed so a later manual Play does not rerun the job).
    public static string EditorTriggerPath => Path.Combine(Application.dataPath, "..", "Temp", "boeval_job.txt");
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Attach()
    {
        var args = System.Environment.GetCommandLineArgs(); string job = null;
        for (int i = 0; i < args.Length - 1; i++) if (args[i] == ArgName) job = args[i + 1];
#if UNITY_EDITOR
        if (string.IsNullOrEmpty(job) && File.Exists(EditorTriggerPath)) { job = File.ReadAllText(EditorTriggerPath).Trim(); File.Delete(EditorTriggerPath); }
#endif
        if (string.IsNullOrEmpty(job)) return;
        BoEvalHarness.Attach(job);
    }
}

public class BoEvalHarness : MonoBehaviour
{
    public string jobPath;
    public int holdStepsRequired = 50;
    public float dropSeconds = 2f, classifySeconds = 0.3f, passDisplacement = 0.1f, freeFallFraction = 0.8f;

    public static BoEvalHarness Attach(string jobPath)
    {
        var agent = Object.FindFirstObjectByType<ArmGraspAgent>();   // active objects only: the inactive prefab instance is skipped
        if (agent == null) { Debug.LogError("[BoEval] no active ArmGraspAgent in the scene"); return null; }
        var h = agent.gameObject.AddComponent<BoEvalHarness>(); h.jobPath = jobPath; return h;
    }

    enum Phase { Oracle, Policy, Done }
    Phase phase = Phase.Policy;
    BoEvalJob job; BoEvalOracle oracle = new BoEvalOracle();
    ArmGraspAgent agent; MorphologyManager mm; BehaviorParameters bp;
    Transform cyl; Rigidbody cylRb, armRb; Collider cylCol, platform, palmCol;
    List<Collider> segCols = new List<Collider>();
    FieldInfo holdField; PhysicsMaterial mat;
    StringBuilder sbE = new StringBuilder(), sbD = new StringBuilder(), sbDec = new StringBuilder();
    int lastCompleted, episodeIndex, steps, currentSeed = -1, discarded, holds, oracleStep, lastLoggedStep = -1, decCount;
    bool recording, dropDone;
    float lastShaping, lastQuality, lastPenalty; int lastPaid;
    string thetaRow = ""; float t0;

    void Start()
    {
        t0 = Time.realtimeSinceStartup;
        try { job = JsonUtility.FromJson<BoEvalJob>(File.ReadAllText(jobPath)); }
        catch (System.Exception e) { Fail("cannot read job: " + e.Message); return; }
        if (job == null || string.IsNullOrEmpty(job.outDir)) { Fail("job has no outDir"); return; }
        string err = Validate(job); if (err != null) { Fail(err); return; }

        agent = GetComponent<ArmGraspAgent>(); mm = GetComponent<MorphologyManager>(); bp = GetComponent<BehaviorParameters>();
        if (agent == null || mm == null || bp == null) { Fail("ArmGraspAgent / MorphologyManager / BehaviorParameters missing"); return; }
        if (!string.IsNullOrEmpty(job.inferenceCheckInputs))
        {   // fidelity check only: the deployed model on a fixed observation set through the Inference Engine CPU backend
            try
            {
                Directory.CreateDirectory(job.outDir);
                string r = InferenceCheck(bp.Model, job.inferenceCheckInputs, Path.Combine(job.outDir, "inference_check.csv"));
                Debug.Log("[BoEval] inference check " + r);
                WriteJson("done.json", JsonUtility.ToJson(new BoEvalDone { deterministic = true }, true));
                phase = Phase.Done; Quit();
            }
            catch (System.Exception e) { Fail("inference check: " + e); }
            return;
        }
        cyl = GameObject.FindGameObjectWithTag("Cylinder").transform; cylRb = cyl.GetComponent<Rigidbody>(); cylCol = cyl.GetComponent<Collider>();
        armRb = agent.arm.GetComponent<Rigidbody>();
        var plat = GameObject.Find("Platform"); platform = plat != null ? plat.GetComponent<Collider>() : null;
        foreach (var tg in MorphologyManager.GroupNames) { if (tg.StartsWith("wrist")) continue; foreach (var go in GameObject.FindGameObjectsWithTag(tg)) if (go.TryGetComponent<Collider>(out var c)) segCols.Add(c); }
        var palm = transform.Find(agent.palmPath); palmCol = palm != null ? palm.GetComponent<Collider>() : null;
        holdField = typeof(ArmGraspAgent).GetField("m_HoldSteps", BindingFlags.NonPublic | BindingFlags.Instance);
        mat = new PhysicsMaterial("BoEvalMu"); mat.frictionCombine = PhysicsMaterialCombine.Maximum; mat.bounciness = 0f; mat.bounceCombine = PhysicsMaterialCombine.Minimum;

        agent.requiredHoldDecisions = 100000;   // the harness detects the hold; the agent never ends the episode on success
        mm.randomizeByDefault = false;          // morph/randomize off: ApplyForEpisode keeps the pinned fields (no env params in a player without a trainer)
        bp.DeterministicInference = job.deterministic;

        sbE.AppendLine("episode,seed,holdReached,steps,return,retShaping,retQuality,meanQ,contacts,distinctFingers,thumb,palm,coverageGapDeg,antipodality,verticalSpread,wedge,spawnDistance,cylYaw,cylX,cylY,cylZ,minContactHeight,palmHeight,randomizing,lenMean,lenIndex,lenMiddle,lenRing,lenPinky,lenThumb,omegaFingerMean,omegaFingerMin,omegaFingerMax,zetaFingerMean,omegaWristMean,activeGroups,mask,handSpan,fingerLengthRatio");
        sbD.AppendLine("episode,seed,mu,repeat,disp01,disp02,disp03,axial03,lateral03,contactsAt03,disp2s,axial2s,lateral2s,pass,mode,slideOnsetStep");
        if (job.logDecisions)
        {
            var h = new StringBuilder("episode,seed,academyStep,agentStep,harnessStep"); int nObs = 22, nAct = ArmGraspAgent.ActionCount;
            for (int i = 0; i < nObs; i++) h.Append(",obs" + i); for (int i = 0; i < nAct; i++) h.Append(",act" + i);
            sbDec.AppendLine(h.ToString());
            Academy.Instance.AgentPreStep += OnAgentPreStep;   // fires before the agents step: (last collected vector observation, last decided action) is a consistent pair
        }
        Flush();
        Time.timeScale = job.timeScale;

        // Two resets: the first applies the link scales so MorphologyManager recomputes the nominal inertia at these scales,
        // the second starts the first evaluated episode with (k, b, I) derived from that nominal inertia.
        lastCompleted = agent.CompletedEpisodes;
        ApplyTheta(); agent.EndEpisode();
        phase = job.oracle ? Phase.Oracle : Phase.Policy;
        bp.BehaviorType = phase == Phase.Oracle ? BehaviorType.HeuristicOnly : BehaviorType.InferenceOnly;
        NextSeed(); agent.EndEpisode();
        Debug.Log("[BoEval] started seeds=" + job.seeds.Length + " mu=" + job.mu.Length + " oracle=" + job.oracle + " deterministic=" + job.deterministic + " out=" + job.outDir);
    }

    static string Validate(BoEvalJob j)
    {
        if (j.lengthScale == null || j.lengthScale.Length != MorphologyManager.FingerCount) return "lengthScale must have 5 entries";
        if (j.omega == null || j.omega.Length != MorphologyManager.GroupCount) return "omega must have 16 entries";
        if (j.zeta == null || j.zeta.Length != MorphologyManager.GroupCount) return "zeta must have 16 entries";
        if (j.inertiaScale == null || j.inertiaScale.Length != MorphologyManager.GroupCount) return "inertiaScale must have 16 entries";
        if (j.mask == null || j.mask.Length != MorphologyManager.FingerGroupCount) return "mask must have 14 entries";
        if (j.seeds == null) j.seeds = new int[0];
        if (j.mu == null || j.mu.Length == 0) return "mu must have at least one entry";
        for (int g = 0; g < MorphologyManager.GroupCount; g++) if (j.omega[g] <= 0f || j.zeta[g] < 0f || j.inertiaScale[g] <= 0f) return "omega and inertiaScale must be > 0, zeta >= 0";
        return null;
    }

    // Pin theta through the serialized fields (present env-param keys would win, but a player without a trainer has none).
    void ApplyTheta()
    {
        for (int f = 0; f < MorphologyManager.FingerCount; f++) mm.lengthScale[f] = job.lengthScale[f];
        for (int g = 0; g < MorphologyManager.FingerGroupCount; g++) mm.mask[g] = job.mask[g] > 0;
        for (int g = 0; g < MorphologyManager.GroupCount; g++)
        {
            float I = mm.NominalInertia[g] * job.inertiaScale[g], w = job.omega[g], z = job.zeta[g];
            mm.inertia[g] = I; mm.stiffness[g] = I * w * w; mm.damping[g] = 2f * z * I * w;
        }
    }

    void NextSeed()
    {
        ApplyTheta();
        if (phase == Phase.Oracle) { currentSeed = -1; return; }
        int i = episodeIndex;
        if (job.seeds != null && i < job.seeds.Length) { currentSeed = job.seeds[i]; UnityEngine.Random.InitState(currentSeed); } else currentSeed = -1;
    }

    void SnapshotTheta()
    {
        float lenMean = 0f; for (int f = 0; f < MorphologyManager.FingerCount; f++) lenMean += mm.lengthScale[f] / MorphologyManager.FingerCount;
        float wMin = float.MaxValue, wMax = 0f, wSum = 0f, zSum = 0f, wWrist = 0f; string m = "";
        for (int g = 0; g < MorphologyManager.FingerGroupCount; g++) { float w = mm.NaturalFrequency(g); wSum += w; wMin = Mathf.Min(wMin, w); wMax = Mathf.Max(wMax, w); zSum += mm.DampingRatio(g); m += mm.mask[g] ? "1" : "0"; }
        for (int g = MorphologyManager.FingerGroupCount; g < MorphologyManager.GroupCount; g++) wWrist += mm.NaturalFrequency(g) / MorphologyManager.WristAxisCount;
        thetaRow = "," + (mm.Randomizing ? "1" : "0") + "," + lenMean.ToString("F4") + "," + mm.lengthScale[0].ToString("F3") + "," + mm.lengthScale[1].ToString("F3") + "," + mm.lengthScale[2].ToString("F3") + "," + mm.lengthScale[3].ToString("F3") + "," + mm.lengthScale[4].ToString("F3")
            + "," + (wSum / MorphologyManager.FingerGroupCount).ToString("F2") + "," + wMin.ToString("F2") + "," + wMax.ToString("F2") + "," + (zSum / MorphologyManager.FingerGroupCount).ToString("F3") + "," + wWrist.ToString("F2")
            + "," + mm.ActiveCount + "," + m + "," + mm.HandSpan.ToString("F4") + "," + mm.FingerLengthRatio.ToString("F4");
    }

    void FixedUpdate()
    {
        if (phase == Phase.Done || job == null) return;
        int completed = agent.CompletedEpisodes;
        if (phase == Phase.Oracle)
        {
            if (completed != lastCompleted) { lastCompleted = completed; oracleStep = 0; OracleEpisodeStarted(); return; }
            OracleStep();
            return;
        }
        if (completed != lastCompleted)
        {
            lastCompleted = completed;
            if (recording)
            {   // the agent ended the episode itself (MaxStep); the episode now running was drawn from the unseeded stream -> log the failure, reseed, restart
                EndEpisode(false); NextSeed();
                if (episodeIndex >= job.seeds.Length) { Finish(); return; }
                discarded++; agent.EndEpisode(); return;
            }
            if (episodeIndex >= job.seeds.Length) { Finish(); return; }
            steps = 0; recording = true; dropDone = false; lastLoggedStep = -1; SnapshotTheta(); return;
        }
        if (!recording) return;
        steps++;
        lastShaping = agent.ShapingReturn; lastQuality = agent.QualityReturn; lastPenalty = agent.PenaltyReturn; lastPaid = agent.QualityStepsPaid;
        int hold = holdField != null ? (int)holdField.GetValue(agent) : 0;
        if (hold >= holdStepsRequired && !dropDone) { dropDone = true; EndEpisode(true); }
    }

    void OnAgentPreStep(int academyStep)
    {
        if (phase != Phase.Policy || !recording || decCount >= job.logDecisionsMax) return;
        int sc = agent.StepCount;
        if (sc == lastLoggedStep) return;
        lastLoggedStep = sc; decCount++;
        var obs = agent.GetObservations(); var act = agent.GetStoredActionBuffers().ContinuousActions;
        var sb = new StringBuilder(); sb.Append(episodeIndex + 1).Append(',').Append(currentSeed).Append(',').Append(academyStep).Append(',').Append(sc).Append(',').Append(steps);
        for (int i = 0; i < 22; i++) sb.Append(',').Append(i < obs.Count ? obs[i].ToString("R") : "");
        for (int i = 0; i < act.Length; i++) sb.Append(',').Append(act[i].ToString("R"));
        sbDec.AppendLine(sb.ToString());
    }

    /// <summary>Runs the deployed model on a fixed observation set through the Inference Engine CPU backend (the backend ML-Agents
    /// uses for InferenceDevice Default/Burst) and writes one row per set: deterministic head then sampled head (19 + 19).</summary>
    public static string InferenceCheck(ModelAsset asset, string inputsPath, string outCsv)
    {
        if (asset == null) throw new System.Exception("no model asset on the BehaviorParameters");
        var inp = JsonUtility.FromJson<BoEvalInferenceInputs>(File.ReadAllText(inputsPath));
        float[][] all = { inp.obs_0, inp.obs_1, inp.obs_2, inp.obs_3, inp.obs_4, inp.obs_5, inp.obs_6, inp.obs_7, inp.obs_8, inp.obs_9, inp.obs_10, inp.obs_11 };
        var model = ModelLoader.Load(asset);
        var sb = new StringBuilder("index");
        for (int i = 0; i < ArmGraspAgent.ActionCount; i++) sb.Append(",det" + i);
        for (int i = 0; i < ArmGraspAgent.ActionCount; i++) sb.Append(",samp" + i);
        sb.AppendLine();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        using (var worker = new Worker(model, BackendType.CPU))
        {
            for (int s = 0; s < inp.n; s++)
            {
                var tensors = new List<Tensor>();
                foreach (var mi in model.inputs)
                {
                    int k = int.Parse(mi.name.Substring(4));   // obs_k
                    int[] dims = mi.shape.ToIntArray(); dims[0] = 1; int len = 1; for (int i = 1; i < dims.Length; i++) len *= dims[i];
                    var data = new float[len]; System.Array.Copy(all[k], s * len, data, 0, len);
                    tensors.Add(new Tensor<float>(new TensorShape(dims), data));
                }
                worker.Schedule(tensors.ToArray());
                var det = (worker.PeekOutput("deterministic_continuous_actions") as Tensor<float>).DownloadToArray();
                var samp = (worker.PeekOutput("continuous_actions") as Tensor<float>).DownloadToArray();
                sb.Append(s); foreach (var v in det) sb.Append(',').Append(v.ToString("R")); foreach (var v in samp) sb.Append(',').Append(v.ToString("R")); sb.AppendLine();
                foreach (var t in tensors) t.Dispose();
            }
        }
        File.WriteAllText(outCsv, sb.ToString());
        return "n=" + inp.n + " model=" + asset.name + " ms=" + sw.ElapsedMilliseconds + " out=" + outCsv;
    }

    // ---- forced-close feasibility oracle (MorphVerify close mode) ----
    void OracleEpisodeStarted()
    {
        oracle.ran = true; oracle.activeGroups = mm.ActiveCount; oracle.maxContacts = 0; oracle.firstGateStep = -1; oracle.gateMet = false;
        cyl.SetPositionAndRotation(agent.GraspPoint, Quaternion.identity); Physics.SyncTransforms();
        var handCols = new List<Collider>(segCols); if (palmCol != null) handCols.Add(palmCol);
        Vector3 nrm = palmCol != null ? palmCol.transform.rotation * Vector3.right : Vector3.right; float pushed = 0f;
        for (int it = 0; it < 120; it++)
        {   // back the teleported object off along the palm normal in 1 mm steps until it is free of the open hand
            bool overlap = false;
            foreach (var hc in handCols) if (Physics.ComputePenetration(cylCol, cyl.position, cyl.rotation, hc, hc.transform.position, hc.transform.rotation, out _, out float dist) && dist > 1e-5f) { overlap = true; break; }
            if (!overlap) break;
            cyl.position += nrm * 0.001f; pushed += 0.001f; Physics.SyncTransforms();
        }
        if (cylRb != null) { cylRb.linearVelocity = Vector3.zero; cylRb.angularVelocity = Vector3.zero; }
        oracle.pushOutMm = pushed * 1000f;
    }

    void OracleStep()
    {
        oracleStep++;
        var acts = agent.heuristicActions; for (int i = 0; i < acts.Length; i++) acts[i] = 0f;
        acts[0] = acts[3] = acts[6] = acts[9] = -1f; if (oracleStep > 60) acts[1] = acts[4] = acts[7] = acts[10] = -1f; if (oracleStep > 120) acts[2] = acts[5] = acts[8] = acts[11] = -1f; acts[12] = 1f; acts[13] = 1f;
        int contacts = agent.CurrentContacts; bool gate = agent.LastHoldCriterionMet;
        if (contacts > oracle.maxContacts) oracle.maxContacts = contacts;
        if (gate) { oracle.gateMet = true; if (oracle.firstGateStep < 0) oracle.firstGateStep = oracleStep; }
        if (oracleStep >= job.oracleSteps)
        {
            oracle.finalContacts = contacts; oracle.gateHeldAtEnd = gate; oracle.steps = oracleStep;
            WriteJson("oracle.json", JsonUtility.ToJson(oracle, true));
            Debug.Log("[BoEval] oracle gateMet=" + oracle.gateMet + " firstGateStep=" + oracle.firstGateStep + " maxContacts=" + oracle.maxContacts + " finalContacts=" + oracle.finalContacts + " pushOut=" + oracle.pushOutMm.ToString("F1") + "mm");
            for (int i = 0; i < acts.Length; i++) acts[i] = 0f;
            phase = Phase.Policy; bp.BehaviorType = BehaviorType.InferenceOnly;
            if (job.seeds.Length == 0) { Finish(); return; }
            NextSeed(); agent.EndEpisode();
        }
    }

    // ---- policy episodes + drop test (GraspDiagnostic v5 mechanics) ----
    float ContactHeight(Collider col)
    {
        Vector3 p0 = cylCol.ClosestPoint(col.transform.position); Vector3 onSeg = col.ClosestPoint(p0);
        bool touching = Physics.ComputePenetration(col, col.transform.position, col.transform.rotation, cylCol, cyl.position, cyl.rotation, out _, out _) || Vector3.Distance(p0, onSeg) <= agent.contactDistance;
        if (!touching) return float.NaN;
        Vector3 onCyl = cylCol.ClosestPoint(onSeg); return Vector3.Dot(onCyl - cyl.position, cyl.up);
    }

    int TouchingCount()
    {
        int k = 0;
        foreach (var col in segCols)
        {
            if (Physics.ComputePenetration(col, col.transform.position, col.transform.rotation, cylCol, cyl.position, cyl.rotation, out _, out _)) { k++; continue; }
            Vector3 p0 = cylCol.ClosestPoint(col.transform.position); if (Vector3.Distance(p0, col.ClosestPoint(p0)) <= agent.contactDistance) k++;
        }
        return k;
    }

    void EndEpisode(bool holdReached)
    {
        episodeIndex++; recording = false; if (holdReached) holds++;
        float ret = lastShaping + lastQuality + lastPenalty + (holdReached ? agent.successBonus : 0f);
        float meanQ = lastPaid > 0 ? lastQuality / (lastPaid * agent.qualityPayPerStep) : 0f;
        float minH = float.NaN, palmH = float.NaN;
        foreach (var c in segCols) { float h = ContactHeight(c); if (!float.IsNaN(h) && (float.IsNaN(minH) || h < minH)) minH = h; }
        if (palmCol != null) { palmH = ContactHeight(palmCol); if (!float.IsNaN(palmH) && (float.IsNaN(minH) || palmH < minH)) minH = palmH; }
        sbE.AppendLine(string.Join(",", new string[] { episodeIndex.ToString(), currentSeed.ToString(), holdReached ? "1" : "0", steps.ToString(), ret.ToString("F4"), lastShaping.ToString("F4"), lastQuality.ToString("F4"), meanQ.ToString("F4"),
            agent.CurrentContacts.ToString(), agent.LastDistinctFingers.ToString(), agent.LastThumbTouching ? "1" : "0", agent.LastPalmTouching ? "1" : "0", agent.LastCoverageGapDeg.ToString("F1"), agent.LastAntipodality.ToString("F3"), agent.LastVerticalSpread.ToString("F4"), agent.LastWedge.ToString("F3"),
            agent.SpawnDistance.ToString("F3"), cyl.eulerAngles.y.ToString("F0"), cyl.position.x.ToString("F4"), cyl.position.y.ToString("F4"), cyl.position.z.ToString("F4"), minH.ToString("F4"), palmH.ToString("F4") }) + thetaRow);
        if (holdReached)
        {
            for (int m = 0; m < job.mu.Length; m++)
            {
                float mu = job.mu[m]; mat.dynamicFriction = mu; mat.staticFriction = mu; cylCol.sharedMaterial = mat;
                for (int r = 0; r < job.dropRepeats; r++) DropTest(mu, r);
            }
            cylCol.sharedMaterial = null;
        }
        Flush();
        Debug.Log("[BoEval] ep " + episodeIndex + " seed=" + currentSeed + " hold=" + holdReached + " steps=" + steps + " discarded=" + discarded + " theta=" + thetaRow);
        if (holdReached) { NextSeed(); agent.EndEpisode(); }
    }

    void DropTest(float mu, int repeat)
    {
        Vector3 pos0 = cyl.position; Quaternion rot0 = cyl.rotation; Vector3 up = cyl.up;
        var prevMode = Physics.simulationMode;
        bool armWasKinematic = armRb != null && armRb.isKinematic; bool platWasEnabled = platform != null && platform.enabled;
        var constraints0 = cylRb.constraints;
        Physics.simulationMode = SimulationMode.Script;
        if (armRb != null) armRb.isKinematic = true;
        if (platform != null) platform.enabled = false;
        cylRb.isKinematic = true; cyl.SetPositionAndRotation(pos0, rot0); Physics.SyncTransforms();
        cylRb.constraints = RigidbodyConstraints.None;
        cylRb.isKinematic = false; cylRb.useGravity = true; cylRb.linearVelocity = Vector3.zero; cylRb.angularVelocity = Vector3.zero; cylRb.WakeUp();
        float dt = Time.fixedDeltaTime; int n = Mathf.RoundToInt(dropSeconds / dt), nc = Mathf.RoundToInt(classifySeconds / dt);
        float d01 = 0f, d02 = 0f, d03 = 0f, ax03 = 0f, lat03 = 0f; int contacts03 = -1, slideOnset = -1;
        for (int i = 1; i <= n; i++)
        {
            Physics.Simulate(dt);
            Vector3 d = cylRb.position - pos0; float axial = -Vector3.Dot(d, up);
            if (slideOnset < 0 && axial > 0.0005f) slideOnset = i;
            if (i == Mathf.RoundToInt(0.1f / dt)) d01 = d.magnitude;
            if (i == Mathf.RoundToInt(0.2f / dt)) d02 = d.magnitude;
            if (i == nc) { d03 = d.magnitude; ax03 = axial; lat03 = (d - Vector3.Dot(d, up) * up).magnitude; contacts03 = TouchingCount(); }
        }
        Vector3 dEnd = cylRb.position - pos0; float disp = dEnd.magnitude; float axEnd = -Vector3.Dot(dEnd, up), latEnd = (dEnd - Vector3.Dot(dEnd, up) * up).magnitude;
        bool pass = disp < passDisplacement;
        float freeFall03 = 0.5f * 9.81f * classifySeconds * classifySeconds;
        string mode = pass ? "pass" : (d03 >= freeFallFraction * freeFall03 ? "freefall" : (d03 >= 0.01f ? (ax03 >= lat03 ? "axial" : "lateral") : (axEnd >= latEnd ? "slow-axial" : "slow-lateral")));
        sbD.AppendLine(episodeIndex + "," + currentSeed + "," + mu + "," + (repeat + 1) + "," + d01.ToString("F4") + "," + d02.ToString("F4") + "," + d03.ToString("F4") + "," + ax03.ToString("F4") + "," + lat03.ToString("F4") + "," + contacts03 + "," + disp.ToString("F4") + "," + axEnd.ToString("F4") + "," + latEnd.ToString("F4") + "," + (pass ? 1 : 0) + "," + mode + "," + slideOnset);
        cylRb.useGravity = false; cylRb.isKinematic = true; cylRb.constraints = constraints0; cyl.SetPositionAndRotation(pos0, rot0);
        if (platform != null) platform.enabled = platWasEnabled;
        if (armRb != null) armRb.isKinematic = armWasKinematic;
        Physics.SyncTransforms();
        Physics.simulationMode = prevMode;
    }

    void Flush()
    {
        try
        {
            Directory.CreateDirectory(job.outDir);
            File.WriteAllText(Path.Combine(job.outDir, "episodes.csv"), sbE.ToString()); File.WriteAllText(Path.Combine(job.outDir, "drops.csv"), sbD.ToString());
            if (job.logDecisions) File.WriteAllText(Path.Combine(job.outDir, "decisions.csv"), sbDec.ToString());
        }
        catch (System.Exception e) { Debug.LogWarning("[BoEval] flush: " + e.Message); }
    }

    void WriteJson(string name, string text)
    {
        try { Directory.CreateDirectory(job.outDir); File.WriteAllText(Path.Combine(job.outDir, name), text); }
        catch (System.Exception e) { Debug.LogWarning("[BoEval] write " + name + ": " + e.Message); }
    }

    void Fail(string message)
    {
        Debug.LogError("[BoEval] " + message);
        if (job != null && !string.IsNullOrEmpty(job.outDir)) WriteJson("done.json", JsonUtility.ToJson(new BoEvalDone { error = message }, true));
        phase = Phase.Done; Quit();
    }

    void Finish()
    {
        phase = Phase.Done; recording = false; Flush();
        if (cylCol != null) cylCol.sharedMaterial = null;
        var done = new BoEvalDone { episodes = episodeIndex, holds = holds, discarded = discarded, seedsRequested = job.seeds.Length, elapsedSec = Time.realtimeSinceStartup - t0, deterministic = job.deterministic };
        WriteJson("done.json", JsonUtility.ToJson(done, true));
        Debug.Log("[BoEval] done episodes=" + episodeIndex + " holds=" + holds + " discarded=" + discarded + " elapsed=" + done.elapsedSec.ToString("F0") + "s");
        Time.timeScale = 1f; enabled = false; Quit();
    }

    void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void OnDestroy()
    {
        Time.timeScale = 1f; if (cylCol != null) cylCol.sharedMaterial = null;
        if (job != null && job.logDecisions && Academy.IsInitialized) Academy.Instance.AgentPreStep -= OnAgentPreStep;
    }
}
