// Object bank (run 013 prep, 2026-09-27): a pre-cooked bank of convex superquadric shapes that replaces the object's mesh + convex
// collider per episode, and (since the deterministic-reset change) the object's base orientation per episode. Attached at scene load
// to the "Cylinder"-tagged object (no scene change). ArmGraspAgent.OnEpisodeBegin calls ApplyForEpisode after the object is made
// kinematic with this episode's mass and before SpawnCylinder places it; SpawnCylinder then applies the episode's yaw on top of
// SpawnBaseRotation and sets the position, so every episode's object pose is a function of the episode's random stream alone.
//
// Environment parameters (read every episode; the harness and the trainer side-channel both set them):
//   object/mode  0 anchorOnly (default, the scene's own mesh) | 1 trainUniform (indices 1-64, one draw) | 2 heldoutUniform (65-80, one draw) | 3 fixedIndex
//   object/index the bank index for fixedIndex
//   object/seed  the generator seed the caller expects (BANK_SEED.txt); a mismatch is logged once
//   object/anchorShare  trainUniform only: probability of drawing the anchor (index 0) instead of a train shape (default 0 = no extra draw)
//   (Editor harness only, Temp/gates.json key objectIndexStride, default 1 = unchanged): fixedIndex train indices 1-64 are remapped to
//   1 + ((i - 1) * s + s - 1) mod 64, so stride 2 maps a stratified 1..64 sweep onto the even train shapes 2, 4, ..., 64 (BO objective, run 013E brief 2b)
//   object/pose  0 canonical (default: the shape's canonical rest orientation = the scene object's rotation at load, length axis up)
//                | 1 randomStable (one of the shape's settled rest poses from stable_poses.json, drawn from the episode's stream)
// Exactly one UnityEngine.Random draw is consumed for the pose in BOTH pose modes (canonical ignores it), so canonical and randomStable
// passes on the same seeds share every later draw (yaw, spawn position, pulses).
//
// Bank files: Assets/Objects/Bank/bank.csv + obj_NNN.obj (tools/objects/superquadric_bank.py; vertices in metres, Unity frame, local Y =
// length axis, X / Z = the cross-section the hand closes on). Index 0 is the anchor: the scene's own Mesh (Unity's built-in cylinder) with
// the scene's transform scale, kept as a reference. stable_poses.json: per shape, the settled rest poses found by the cook (below).
//
// Stable-pose cook (once per bank, cached, deterministic): for every shape and each of the 6 body axes (+-X, +-Y, +-Z) placed up, the shape
// is dropped 1 mm above a flat ground in a hidden physics scene (same project physics settings, one job worker, friction 1.0 as the object
// material), with a 3 deg deterministic tilt about a fixed horizontal axis so that unstable equilibria (a pointed end balanced on its tip)
// fall instead of standing on a knife edge, and simulated for 400 steps of 10 ms. A candidate is "settled" when its angular speed stays
// below 0.05 rad/s and its centre moves less than 0.5 mm over the last 50 steps. The converged body-frame up axis identifies the pose
// (candidates within 10 deg of an earlier pose are merged); the stored rotation is the minimal rotation taking that body axis to world up
// (yaw-free; the spawn adds the yaw). Cache: stable_poses.json; Temp/recook_poses forces a re-cook into stable_poses.recook.json for a
// determinism diff without touching the committed cache.
//
// Swap (index != 0): MeshFilter + MeshCollider mesh <- the bank Mesh (convex, cooked once at load with Physics.BakeMesh), localScale <- 1,
// contactOffset re-applied, centre of mass / inertia reset. Index 0 restores the scene mesh + scale. Then, in every mode: rotation <- the
// pose (no yaw), velocities zeroed, body put to sleep, Physics.SyncTransforms, rest half-height agent.ObjectHalfHeight <- position.y -
// collider.bounds.min.y (the yaw about world Y keeps it), agent.SpawnBaseRotation <- the pose. Tag, layer, physics material, Rigidbody
// settings, friction, mass draw, observations, actions and rewards are not touched.
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.MLAgents;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ObjectBank : MonoBehaviour
{
    public const string BankDir = "Objects/Bank";          // under Application.dataPath (Editor / project runs; a player build would need StreamingAssets)
    public const int TrainFrom = 1, TrainTo = 64, HeldoutFrom = 65, HeldoutTo = 80;
    public enum DrawMode { anchorOnly = 0, trainUniform = 1, heldoutUniform = 2, fixedIndex = 3 }
    public enum PoseMode { canonical = 0, randomStable = 1 }
    // cook constants
    public const int CookSteps = 600, CookSettleWindow = 50; public const float CookDt = 0.01f, CookTiltDeg = 3f, CookAngSpeedMax = 0.05f, CookDriftMaxM = 0.001f, CookRotMaxDeg = 1f, CookMergeDeg = 10f, CookAngularDamping = 2f, CookLinearDamping = 0.5f;

    [System.Serializable]
    public struct Entry
    {
        public int index; public string split, file;
        public float a1mm, a2mm, a3mm, e1, e2, volumeCm3, extentXmm, extentYmm, extentZmm;
        public Mesh mesh;
    }
    public class StablePose
    {
        public Quaternion rotation;   // yaw-free: minimal rotation taking bodyUp to world up
        public Vector3 bodyUp;        // body-frame axis that ends up pointing up
        public int fromCandidate;     // 0..5 = +X -X +Y -Y +Z -Z
        public int classId = -1;      // symmetry class within the shape (ClassKey); -1 = unsettled (never drawn)
        public bool settled; public int settleStep; public float tiltFromUprightDeg; public float restHalfHeightM;
        public float finalAngSpeed, finalSpeed, lastDrift, lastRotDeg;   // cook diagnostics at the last step: |omega| (rad/s), |v| (m/s), centre drift (m) and orientation change (deg) over the last window
    }

    public static ObjectBank Instance { get; private set; }
    public readonly List<Entry> Entries = new List<Entry>();
    public readonly Dictionary<int, List<StablePose>> Poses = new Dictionary<int, List<StablePose>>();
    /// <summary>Settled poses of each shape grouped into symmetry classes (class id order = cache order): randomStable draws a class uniformly, then a pose
    /// uniformly within it, so a physical presentation is not over-weighted by how many equivalent orientations the cook happened to find.</summary>
    public readonly Dictionary<int, List<List<StablePose>>> Classes = new Dictionary<int, List<List<StablePose>>>();
    public int CurrentPoseClass { get; private set; } = -1;
    /// <summary>Probability of a canonical presentation this episode (pose/canonicalFraction; see ApplyForEpisode).</summary>
    public float CurrentCanonicalFraction { get; private set; } = 1f;
    public int BankSeed { get; private set; } = -1;
    public bool Loaded { get; private set; }
    public DrawMode CurrentMode { get; private set; } = DrawMode.anchorOnly;
    public PoseMode CurrentPoseMode { get; private set; } = PoseMode.canonical;
    public int CurrentIndex { get; private set; }             // bank index of the object in the current episode (0 = anchor)
    public int CurrentPoseIndex { get; private set; } = -1;   // index into Poses[CurrentIndex] for randomStable, -1 for canonical
    public Quaternion CurrentPoseRotation { get; private set; } = Quaternion.identity;
    public Entry Current => CurrentIndex >= 0 && CurrentIndex < Entries.Count ? Entries[CurrentIndex] : default;
    public int SwapCount { get; private set; }
    /// <summary>Tilt (deg) of the object's length axis from world up at the top of ApplyForEpisode, BEFORE the reset: what the old spawn rule would have carried.</summary>
    public float PreResetTiltDeg { get; private set; } = float.NaN;
    /// <summary>Tilt (deg) of the length axis from world up measured two physics steps after the spawn of the current episode.</summary>
    public float SpawnTiltDeg { get; private set; } = float.NaN;
    /// <summary>True if the object's rotation two physics steps after the spawn differs from (yaw x pose) by more than 0.5 deg: carry-over or an unexpected move. Should always be false.</summary>
    public bool SpawnCarryover { get; private set; }
    public Quaternion CanonicalRotation { get; private set; } = Quaternion.identity;

    MeshFilter mf; MeshCollider mc; Rigidbody rb; ArmGraspAgent agentRef;
    Mesh anchorMesh; Vector3 anchorScale; int applied = 0; bool seedWarned; int measureCountdown = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var go = GameObject.FindGameObjectWithTag("Cylinder");
        if (go == null) { Debug.LogWarning("[ObjectBank] no object tagged Cylinder; bank not attached"); return; }
        if (go.GetComponent<ObjectBank>() == null) go.AddComponent<ObjectBank>();
    }

    void Awake()
    {
        Instance = this;
        mf = GetComponent<MeshFilter>(); mc = GetComponent<MeshCollider>(); rb = GetComponent<Rigidbody>();
        if (mf == null || mc == null) { Debug.LogError("[ObjectBank] the object needs a MeshFilter and a MeshCollider (scene: convex MeshCollider on the built-in cylinder)"); return; }
        anchorMesh = mc.sharedMesh != null ? mc.sharedMesh : mf.sharedMesh; anchorScale = transform.localScale; CanonicalRotation = transform.rotation;
        LoadBank();
#if UNITY_EDITOR
        ExportAnchorIfMissing();
#endif
#if UNITY_EDITOR
        ReadHarnessFraction();
#endif
        LoadOrCookPoses();
        int nClasses = 0; foreach (var kv in Classes) nClasses += kv.Value.Count;
        Debug.Log("[ObjectBank] bank ready: " + Entries.Count + " shapes, " + nClasses + " pose classes over " + Classes.Count + " shapes, seed " + BankSeed + ", dir " + Dir + (Application.isEditor ? " (Editor)" : " (player)"));
        if (!Application.isEditor && (!Loaded || Classes.Count < Entries.Count))
        {   // a player must never train or evaluate on a silently reduced bank: refuse to start
            Debug.LogError("[ObjectBank] FATAL: bank or pose cache missing / incomplete in the player (" + Entries.Count + " shapes loaded, " + Classes.Count + " with pose classes; expected " + (HeldoutTo + 1) + ") under " + Dir + "; quitting");
            Application.Quit(1);
        }
    }

    // Editor: Assets/Objects/Bank. Player: <Prosthetic>_Data/StreamingAssets/ObjectBank, copied next to the player by ObjectBankBuild (post-build step);
    // the bank is not embedded as assets, so the player reads the same files (OBJ, CSV, JSON) the Editor reads.
    public const string PlayerBankDir = "ObjectBank";
    string Dir => Application.isEditor ? Path.Combine(Application.dataPath, BankDir) : Path.Combine(Application.streamingAssetsPath, PlayerBankDir);

    void LoadBank()
    {
        string csv = Path.Combine(Dir, "bank.csv");
        if (!File.Exists(csv)) { Debug.LogWarning("[ObjectBank] " + csv + " not found; only the anchor is available"); Entries.Add(AnchorEntry()); Loaded = false; return; }
        string seedFile = Path.Combine(Dir, "BANK_SEED.txt"); if (File.Exists(seedFile) && int.TryParse(File.ReadAllText(seedFile).Trim(), out int s)) BankSeed = s;
        var lines = File.ReadAllLines(csv); var head = lines[0].Split(','); int col(string n) => System.Array.IndexOf(head, n);
        int iIdx = col("index"), iSplit = col("split"), iFile = col("file"), iA1 = col("a1_mm"), iA2 = col("a2_mm"), iA3 = col("a3_mm"), iE1 = col("e1"), iE2 = col("e2"), iVol = col("volume_cm3"), iEx = col("extent_x_mm"), iEy = col("extent_y_mm"), iEz = col("extent_z_mm");
        var byIndex = new SortedDictionary<int, Entry>();
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var f = lines[i].Split(','); float P(int c) => float.Parse(f[c], CultureInfo.InvariantCulture);
            var e = new Entry { index = int.Parse(f[iIdx]), split = f[iSplit], file = f[iFile], a1mm = P(iA1), a2mm = P(iA2), a3mm = P(iA3), e1 = P(iE1), e2 = P(iE2), volumeCm3 = P(iVol), extentXmm = P(iEx), extentYmm = P(iEy), extentZmm = P(iEz) };
            if (e.index == 0) e.mesh = anchorMesh;
            else
            {
                e.mesh = LoadObj(Path.Combine(Dir, e.file), "bank_" + e.index);
                if (e.mesh == null) continue;
                Physics.BakeMesh(e.mesh.GetInstanceID(), true);   // cook the convex hull once per process
            }
            byIndex[e.index] = e;
        }
        if (!byIndex.ContainsKey(0)) byIndex[0] = AnchorEntry();
        Entries.Clear(); int expect = 0;
        foreach (var kv in byIndex) { if (kv.Key != expect) Debug.LogError("[ObjectBank] bank indices are not contiguous at " + expect); Entries.Add(kv.Value); expect = kv.Key + 1; }
        Loaded = Entries.Count >= HeldoutTo + 1;
        Debug.Log("[ObjectBank] loaded " + Entries.Count + " entries (seed " + BankSeed + ") from " + Dir + "; anchor mesh '" + (anchorMesh != null ? anchorMesh.name : "null") + "' scale " + anchorScale.ToString("F3") + " canonical rotation " + CanonicalRotation.eulerAngles.ToString("F1"));
    }

    Entry AnchorEntry() => new Entry { index = 0, split = "anchor", file = "obj_000.obj", a1mm = 25f, a2mm = 25f, a3mm = 140f, e1 = 0f, e2 = 1f, volumeCm3 = 549.779f, extentXmm = 50f, extentYmm = 280f, extentZmm = 50f, mesh = anchorMesh };

    static Mesh LoadObj(string path, string name)
    {
        if (!File.Exists(path)) { Debug.LogError("[ObjectBank] missing " + path); return null; }
        var v = new List<Vector3>(); var t = new List<int>();
        foreach (var raw in File.ReadLines(path))
        {
            var s = raw.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries); if (s.Length == 0) continue;
            if (s[0] == "v") v.Add(new Vector3(float.Parse(s[1], CultureInfo.InvariantCulture), float.Parse(s[2], CultureInfo.InvariantCulture), float.Parse(s[3], CultureInfo.InvariantCulture)));
            else if (s[0] == "f") for (int k = 1; k <= 3; k++) t.Add(int.Parse(s[k].Split('/')[0]) - 1);
        }
        var m = new Mesh { name = name }; m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        var c = m.bounds.center; var a = v[t[0]]; var n = Vector3.Cross(v[t[1]] - a, v[t[2]] - a);
        if (Vector3.Dot(n, (v[t[0]] + v[t[1]] + v[t[2]]) / 3f - c) < 0f) { for (int i = 0; i < t.Count; i += 3) { int tmp = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = tmp; } m.SetTriangles(t, 0); m.RecalculateNormals(); }
        return m;
    }

    // ---------------------------------------------------------------- episode reset
    /// <summary>Called by ArmGraspAgent.OnEpisodeBegin before SpawnCylinder. Reads object/mode, object/index, object/seed, object/pose.</summary>
    public void ApplyForEpisode(ArmGraspAgent agent)
    {
        agentRef = agent;
        PreResetTiltDeg = Vector3.Angle(transform.up, Vector3.up);
        var ep = Academy.Instance.EnvironmentParameters;
        int mode = Mathf.RoundToInt(ep.GetWithDefault("object/mode", 0f));
        int seed = Mathf.RoundToInt(ep.GetWithDefault("object/seed", -1f));
        int pose = Mathf.RoundToInt(ep.GetWithDefault("object/pose", 0f));
        if (seed >= 0 && BankSeed >= 0 && seed != BankSeed && !seedWarned) { seedWarned = true; Debug.LogError("[ObjectBank] object/seed " + seed + " requested but the loaded bank is seed " + BankSeed); }
        CurrentMode = (DrawMode)Mathf.Clamp(mode, 0, 3);
        // canonical fraction: pose/canonicalFraction when the trainer (or the harness) sets it; else the Editor harness config key poseCanonicalFraction
        // (Temp/gates.json, read once); else the legacy object/pose switch (0 canonical -> 1.0, 1 randomStable -> 0.0). Default 1.0 = canonical always.
        float frac = ep.GetWithDefault("pose/canonicalFraction", -1f);
        if (frac < 0f && s_HarnessFraction >= 0f) frac = s_HarnessFraction;
        if (frac < 0f) frac = Mathf.Clamp(pose, 0, 1) == 1 ? 0f : 1f;
        CurrentCanonicalFraction = Mathf.Clamp01(frac);
        int idx = 0;
        if (CurrentMode != DrawMode.anchorOnly)
        {
            if (!Loaded) { if (!seedWarned) { seedWarned = true; Debug.LogError("[ObjectBank] bank not loaded; staying on the anchor"); } }
            else switch (CurrentMode)
            {
                case DrawMode.trainUniform:
                    {   // run 013E: object/anchorShare (default 0 = unchanged: no extra draw) puts the anchor (index 0, the scene cylinder) into the draw with that
                        // probability; the Bernoulli is drawn from the episode's stream only when the share is > 0, so share 0 keeps the random stream byte for byte
                        float share = Mathf.Clamp01(ep.GetWithDefault("object/anchorShare", 0f));
                        if (share > 0f && Random.value < share) idx = 0;
                        else idx = TrainFrom + Random.Range(0, TrainTo - TrainFrom + 1);
                        break;
                    }
                case DrawMode.heldoutUniform: idx = HeldoutFrom + Random.Range(0, HeldoutTo - HeldoutFrom + 1); break;
                default:
                    idx = Mathf.Clamp(Mathf.RoundToInt(ep.GetWithDefault("object/index", 0f)), 0, Entries.Count - 1);
                    if (s_HarnessStride > 1 && idx >= TrainFrom && idx <= TrainTo) { int n = TrainTo - TrainFrom + 1; idx = TrainFrom + ((idx - TrainFrom) * s_HarnessStride + s_HarnessStride - 1) % n; }   // harness objectIndexStride (Editor only)
                    break;
            }
        }
        ApplyShape(idx);
        // pose: exactly one draw per episode in every mode (keeps passes at any canonical fraction paired on every later draw). Bits 15-29 of the draw give the
        // canonical-vs-randomStable Bernoulli (u in [0, 1), canonical if u < fraction: fraction 1 is always canonical, 0 never); bits 0-14 pick the class
        // uniformly, then the pose uniformly within the class.
        int poseDraw = Random.Range(0, 1 << 30);
        float u = (poseDraw >> 15) / 32768f; int low = poseDraw & 0x7FFF;
        CurrentPoseMode = u < CurrentCanonicalFraction ? PoseMode.canonical : PoseMode.randomStable;
        Quaternion poseRot = CanonicalRotation; CurrentPoseIndex = -1; CurrentPoseClass = -1;
        if (CurrentPoseMode == PoseMode.randomStable && Classes.TryGetValue(idx, out var cls) && cls.Count > 0)
        {
            CurrentPoseClass = low % cls.Count; var members = cls[CurrentPoseClass]; var chosen = members[(low / cls.Count) % members.Count];
            poseRot = chosen.rotation; CurrentPoseIndex = Poses[idx].IndexOf(chosen);
        }
        else if (CurrentPoseMode == PoseMode.randomStable) { CurrentPoseMode = PoseMode.canonical; if (!seedWarned) { seedWarned = true; Debug.LogError("[ObjectBank] randomStable requested but no stable poses for index " + idx + "; using canonical"); } }
        CurrentPoseRotation = poseRot;
        transform.rotation = poseRot;
        if (rb != null) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; rb.Sleep(); }
        Physics.SyncTransforms();
        if (agent != null) { agent.ObjectHalfHeight = transform.position.y - mc.bounds.min.y; agent.SpawnBaseRotation = poseRot; }
        measureCountdown = 2; SpawnTiltDeg = float.NaN; SpawnCarryover = false;
    }

    void ApplyShape(int idx)
    {
        if (mf == null || mc == null) return;
        if (idx == 0)
        {
            if (applied != 0) { float off = mc.contactOffset; mf.sharedMesh = anchorMesh; mc.sharedMesh = anchorMesh; transform.localScale = anchorScale; mc.contactOffset = off; if (rb != null) { rb.ResetCenterOfMass(); rb.ResetInertiaTensor(); } SwapCount++; }
            applied = 0; CurrentIndex = 0; return;
        }
        var e = Entries[idx];
        if (applied != idx)
        {
            float off = mc.contactOffset;
            mf.sharedMesh = e.mesh; mc.sharedMesh = e.mesh; mc.convex = true; transform.localScale = Vector3.one; mc.contactOffset = off;
            if (rb != null) { rb.ResetCenterOfMass(); rb.ResetInertiaTensor(); }
            SwapCount++;
        }
        applied = idx; CurrentIndex = idx;
    }

    void FixedUpdate()
    {
        if (measureCountdown < 0) return;
        if (--measureCountdown > 0) return;
        SpawnTiltDeg = Vector3.Angle(transform.up, Vector3.up);
        float yaw = agentRef != null ? agentRef.LastSpawnYawDeg : -1f;
        Quaternion expected = (yaw >= 0f ? Quaternion.Euler(0f, yaw, 0f) : Quaternion.identity) * CurrentPoseRotation;
        SpawnCarryover = Quaternion.Angle(transform.rotation, expected) > 0.5f;
        measureCountdown = -1;
    }

    // ---------------------------------------------------------------- Editor harness: canonical fraction from the harness config (no harness code change)
    static float s_HarnessFraction = -1f; static int s_HarnessStride = 1;
    [System.Serializable] class HarnessPoseCfg { public float poseCanonicalFraction = -1f; public int objectIndexStride = 1; }
#if UNITY_EDITOR
    static void ReadHarnessFraction()
    {
        s_HarnessFraction = -1f; s_HarnessStride = 1;
        try { if (File.Exists("Temp/gates.json")) { var c = JsonUtility.FromJson<HarnessPoseCfg>(File.ReadAllText("Temp/gates.json")); if (c != null && c.poseCanonicalFraction >= 0f) { s_HarnessFraction = Mathf.Clamp01(c.poseCanonicalFraction); Debug.Log("[ObjectBank] harness poseCanonicalFraction " + s_HarnessFraction); } if (c != null && c.objectIndexStride > 1) { s_HarnessStride = c.objectIndexStride; Debug.Log("[ObjectBank] harness objectIndexStride " + s_HarnessStride); } } }
        catch (System.Exception ex) { Debug.LogWarning("[ObjectBank] harness config not readable: " + ex.Message); }
    }
#endif

    // ---------------------------------------------------------------- stable-pose cache
    string PoseCachePath => Path.Combine(Dir, "stable_poses.json");

    void LoadOrCookPoses()
    {
        bool recook = Application.isEditor && File.Exists("Temp/recook_poses");
        if (File.Exists(PoseCachePath) && !recook) { ReadPoses(PoseCachePath); return; }
        if (!Application.isEditor) { Debug.LogError("[ObjectBank] pose cache missing in the player: " + PoseCachePath); return; }   // the player never cooks
        string outPath = File.Exists(PoseCachePath) ? Path.Combine(Dir, "stable_poses.recook.json") : PoseCachePath;
        try { CookAll(outPath); } catch (System.Exception ex) { Debug.LogError("[ObjectBank] pose cook failed: " + ex); }
        if (File.Exists(PoseCachePath)) ReadPoses(PoseCachePath);
    }

    void ReadPoses(string path)
    {   // minimal JSON reader for the format written by WritePoses (one object per line inside "poses": {...})
        Poses.Clear(); Classes.Clear(); int nPoses = 0, nUnsettled = 0;
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim(); if (!line.StartsWith("{\"index\"")) continue;
            var p = ParsePoseLine(line); nPoses++;
            if (!p.Value.settled) { nUnsettled++; continue; }   // a candidate still turning at the end of the settle is recorded in the cache but is not a rest pose to draw from
            if (!Poses.ContainsKey(p.Key)) Poses[p.Key] = new List<StablePose>(); Poses[p.Key].Add(p.Value);
        }
        foreach (var kv in Poses)
        {   // classes: the cached class id when present (cook output), else computed with the same rule
            var e = kv.Key < Entries.Count ? Entries[kv.Key] : AnchorEntry(); bool cached = kv.Value.TrueForAll(p => p.classId >= 0);
            if (!cached) AssignClasses(kv.Value, e);
            var cl = new List<List<StablePose>>();
            foreach (var p in kv.Value) { while (cl.Count <= p.classId) cl.Add(new List<StablePose>()); cl[p.classId].Add(p); }
            cl.RemoveAll(c => c.Count == 0); Classes[kv.Key] = cl;
        }
        var hist = new SortedDictionary<int, int>(); foreach (var kv in Classes) { hist.TryGetValue(kv.Value.Count, out int c); hist[kv.Value.Count] = c + 1; }
        var hs = new List<string>(); foreach (var kv in hist) hs.Add(kv.Key + " classes: " + kv.Value + " shapes");
        int noPose = 0; foreach (var e in Entries) if (!Poses.ContainsKey(e.index)) { noPose++; Debug.LogError("[ObjectBank] shape " + e.index + " has no settled rest pose in the cache"); }
        Debug.Log("[ObjectBank] stable poses: " + (nPoses - nUnsettled) + " settled poses for " + Poses.Count + " shapes (" + nUnsettled + " unsettled candidates kept in the cache, " + noPose + " shapes without a settled pose); pose classes per shape: " + string.Join(", ", hs) + "; from " + path);
    }

    // symmetry classes of rest poses (0a): the superellipsoid is symmetric under each axis reflection, so an up axis and its sign flips are one presentation;
    // the two cross-section axes (local X, Z; the brief's "x-up and y-up") are interchangeable when |a1 - a2| < 2 % of max(a1, a2); and when additionally the
    // cross-section is round (|e2 - 1| <= RoundE2Tol, e.g. the anchor cylinder) every lying orientation is one presentation up to a roll about the length axis.
    public const float SymmetryA12Tol = 0.02f, RoundE2Tol = 0.05f;
    public static Vector3 ClassKey(Vector3 bodyUp, Entry e)
    {
        float a = Mathf.Abs(bodyUp.x), b = Mathf.Abs(bodyUp.y), c = Mathf.Abs(bodyUp.z);
        bool swap = Mathf.Abs(e.a1mm - e.a2mm) < SymmetryA12Tol * Mathf.Max(e.a1mm, e.a2mm);
        if (swap && Mathf.Abs(e.e2 - 1f) <= RoundE2Tol) return new Vector3(Mathf.Sqrt(a * a + c * c), b, 0f).normalized;
        if (swap) return new Vector3(Mathf.Max(a, c), b, Mathf.Min(a, c)).normalized;
        return new Vector3(a, b, c).normalized;
    }
    static void AssignClasses(List<StablePose> poses, Entry e)
    {   // settled poses in cache order; a pose joins the first class whose representative key is within CookMergeDeg; unsettled poses get class -1
        var reps = new List<Vector3>();
        foreach (var p in poses)
        {
            if (!p.settled) { p.classId = -1; continue; }
            var k = ClassKey(p.bodyUp, e); int id = -1;
            for (int i = 0; i < reps.Count; i++) if (Vector3.Angle(reps[i], k) < CookMergeDeg) { id = i; break; }
            if (id < 0) { reps.Add(k); id = reps.Count - 1; }
            p.classId = id;
        }
    }

    static KeyValuePair<int, StablePose> ParsePoseLine(string line)
    {
        float F(string key) { int i = line.IndexOf("\"" + key + "\":") + key.Length + 3; int j = line.IndexOfAny(new[] { ',', '}', ']' }, i); return float.Parse(line.Substring(i, j - i), CultureInfo.InvariantCulture); }
        float[] A(string key) { int i = line.IndexOf("\"" + key + "\":[") + key.Length + 4; int j = line.IndexOf(']', i); var parts = line.Substring(i, j - i).Split(','); var r = new float[parts.Length]; for (int k = 0; k < parts.Length; k++) r[k] = float.Parse(parts[k], CultureInfo.InvariantCulture); return r; }
        var q = A("q"); var u = A("bodyUp");
        var sp = new StablePose { rotation = new Quaternion(q[0], q[1], q[2], q[3]), bodyUp = new Vector3(u[0], u[1], u[2]), fromCandidate = (int)F("from"), settled = line.Contains("\"settled\":true"), settleStep = (int)F("settleStep"), tiltFromUprightDeg = F("tiltDeg"), restHalfHeightM = F("halfH") };
        sp.classId = line.Contains("\"class\":") ? (int)F("class") : -1;
        return new KeyValuePair<int, StablePose>((int)F("index"), sp);
    }

    static readonly Vector3[] k_Candidates = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };

    void CookAll(string outPath)
    {
        int workers0 = Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount; Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount = 1;
        var scene = SceneManager.CreateScene("ObjectBankPoseCook", new CreateSceneParameters(LocalPhysicsMode.Physics3D)); var ps = scene.GetPhysicsScene();
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube); ground.name = "cook_ground"; Destroy(ground.GetComponent<MeshRenderer>()); ground.transform.localScale = new Vector3(4f, 0.1f, 4f); ground.transform.position = new Vector3(0f, -0.05f, 0f);
        var mat = new PhysicsMaterial("CookMu") { staticFriction = 1f, dynamicFriction = 1f, frictionCombine = PhysicsMaterialCombine.Maximum, bounciness = 0f, bounceCombine = PhysicsMaterialCombine.Minimum };
        ground.GetComponent<Collider>().sharedMaterial = mat; SceneManager.MoveGameObjectToScene(ground, scene);
        var body = new GameObject("cook_body"); var bmc = body.AddComponent<MeshCollider>(); bmc.convex = true; bmc.sharedMaterial = mat; var brb = body.AddComponent<Rigidbody>(); brb.mass = 0.5f; brb.interpolation = RigidbodyInterpolation.None; brb.sleepThreshold = 0f;
        brb.angularDamping = CookAngularDamping; brb.linearDamping = CookLinearDamping;   // cook only: PhysX has no rolling resistance, so a nudged round shape would roll forever; damping lets it come to rest on a face without changing which orientations are rest poses
        SceneManager.MoveGameObjectToScene(body, scene);
        var sb = new StringBuilder(); sb.Append("{\"cook\":{\"steps\":" + CookSteps + ",\"dt\":" + CookDt.ToString(CultureInfo.InvariantCulture) + ",\"tiltDeg\":" + CookTiltDeg.ToString(CultureInfo.InvariantCulture) + ",\"settleWindow\":" + CookSettleWindow + ",\"angSpeedMax\":" + CookAngSpeedMax.ToString(CultureInfo.InvariantCulture) + ",\"driftMaxM\":" + CookDriftMaxM.ToString(CultureInfo.InvariantCulture) + ",\"rotMaxDeg\":" + CookRotMaxDeg.ToString(CultureInfo.InvariantCulture) + ",\"classA12Tol\":" + SymmetryA12Tol.ToString(CultureInfo.InvariantCulture) + ",\"classRoundE2Tol\":" + RoundE2Tol.ToString(CultureInfo.InvariantCulture) + ",\"mergeDeg\":" + CookMergeDeg.ToString(CultureInfo.InvariantCulture) + ",\"angularDamping\":" + CookAngularDamping.ToString(CultureInfo.InvariantCulture) + ",\"linearDamping\":" + CookLinearDamping.ToString(CultureInfo.InvariantCulture) + ",\"bankSeed\":" + BankSeed + "},\n\"poses\":[\n");
        var counts = new List<string>(); int total = 0; bool first = true;
        Quaternion pert = Quaternion.AngleAxis(CookTiltDeg, new Vector3(1f, 0f, 1f).normalized);
        foreach (var e in Entries)
        {
            bmc.sharedMesh = e.mesh; body.transform.localScale = e.index == 0 ? anchorScale : Vector3.one; brb.ResetCenterOfMass(); brb.ResetInertiaTensor();
            var found = new List<StablePose>(); int unsettled = 0;
            for (int c = 0; c < 6; c++)
            {
                Quaternion q0 = pert * Quaternion.FromToRotation(k_Candidates[c], Vector3.up);
                body.transform.SetPositionAndRotation(new Vector3(0f, 1f, 0f), q0); brb.linearVelocity = Vector3.zero; brb.angularVelocity = Vector3.zero; Physics.SyncTransforms();
                float minY = bmc.bounds.min.y; body.transform.position = new Vector3(0f, 1f - minY + 0.001f, 0f); Physics.SyncTransforms(); brb.WakeUp();
                int settleStep = -1; Vector3 posWindow = body.transform.position; Quaternion rotWindow = body.transform.rotation; float lastDrift = float.NaN, lastRot = float.NaN;
                for (int s = 1; s <= CookSteps; s++)
                {
                    ps.Simulate(CookDt);
                    if (s % CookSettleWindow == 0)
                    {   // settled = over a whole window (0.5 s) the orientation changed by < CookRotMaxDeg and the centre moved by < CookDriftMaxM; the first such window is the
                        // settle step. Velocities are not used: PhysX reports a jittering angular velocity (0.05-0.4 rad/s) on a resting faceted hull whose pose does not change.
                        lastDrift = (body.transform.position - posWindow).magnitude; lastRot = Quaternion.Angle(body.transform.rotation, rotWindow);
                        if (lastRot < CookRotMaxDeg && lastDrift < CookDriftMaxM && settleStep < 0) settleStep = s;
                        posWindow = body.transform.position; rotWindow = body.transform.rotation;
                    }
                }
                float finalAng = brb.angularVelocity.magnitude, finalSpeed = brb.linearVelocity.magnitude;
                bool settled = settleStep >= 0;
                Quaternion qf = body.transform.rotation; Vector3 bodyUp = (Quaternion.Inverse(qf) * Vector3.up).normalized;
                bool merged = false; foreach (var p in found) if (Vector3.Angle(p.bodyUp, bodyUp) < CookMergeDeg) { merged = true; break; }
                if (!settled) unsettled++;
                if (merged) continue;
                Quaternion rot = Quaternion.FromToRotation(bodyUp, Vector3.up);   // yaw-free pose rotation
                body.transform.rotation = rot; Physics.SyncTransforms(); float halfH = body.transform.position.y - bmc.bounds.min.y;
                found.Add(new StablePose { rotation = rot, bodyUp = bodyUp, fromCandidate = c, settled = settled, settleStep = settleStep, tiltFromUprightDeg = Vector3.Angle(rot * Vector3.up, Vector3.up), restHalfHeightM = halfH, finalAngSpeed = finalAng, finalSpeed = finalSpeed, lastDrift = lastDrift, lastRotDeg = lastRot });
            }
            AssignClasses(found, e); int nCls = 0; foreach (var p in found) nCls = Mathf.Max(nCls, p.classId + 1);
            foreach (var p in found)
            {
                sb.Append(first ? "" : ",\n"); first = false; total++;
                sb.Append(string.Format(CultureInfo.InvariantCulture, "{{\"index\":{0},\"q\":[{1:F6},{2:F6},{3:F6},{4:F6}],\"bodyUp\":[{5:F6},{6:F6},{7:F6}],\"from\":{8},\"settled\":{9},\"settleStep\":{10},\"tiltDeg\":{11:F3},\"halfH\":{12:F6},\"finalAngSpeed\":{13:F5},\"finalSpeed\":{14:F5},\"lastDrift\":{15:F6},\"lastRotDeg\":{16:F4},\"class\":{17}",
                    e.index, p.rotation.x, p.rotation.y, p.rotation.z, p.rotation.w, p.bodyUp.x, p.bodyUp.y, p.bodyUp.z, p.fromCandidate, p.settled ? "true" : "false", p.settleStep, p.tiltFromUprightDeg, p.restHalfHeightM, p.finalAngSpeed, p.finalSpeed, p.lastDrift, p.lastRotDeg, p.classId) + "}");   // the closing brace outside the format string: "{12:F6}}}" is mis-parsed by .NET composite formatting
            }
            counts.Add(e.index + ":" + found.Count + "/" + nCls + "c" + (unsettled > 0 ? "(u" + unsettled + ")" : ""));
        }
        sb.Append("\n]}\n");
        Directory.CreateDirectory(Dir); File.WriteAllText(outPath, sb.ToString());
        Debug.Log("[ObjectBank] pose cook: " + total + " poses over " + Entries.Count + " shapes -> " + outPath + " | per shape (index:poses, u = unsettled candidates): " + string.Join(" ", counts));
        SceneManager.UnloadSceneAsync(scene);
        Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount = workers0;
    }

#if UNITY_EDITOR
    void ExportAnchorIfMissing()
    {
        try
        {
            string path = Path.Combine(Dir, "obj_000.obj"); if (File.Exists(path) || anchorMesh == null) return;
            Directory.CreateDirectory(Dir);
            var v = anchorMesh.vertices; var t = anchorMesh.triangles; var sb = new StringBuilder();
            sb.Append("# anchor: the scene object's mesh '" + anchorMesh.name + "' (" + v.Length + " vertices, " + t.Length / 3 + " triangles) x transform scale " + anchorScale.ToString("F4") + ", exported by ObjectBank\n# units: metres, Unity frame\n");
            foreach (var p in v) sb.Append(string.Format(CultureInfo.InvariantCulture, "v {0:F7} {1:F7} {2:F7}\n", p.x * anchorScale.x, p.y * anchorScale.y, p.z * anchorScale.z));
            for (int i = 0; i < t.Length; i += 3) sb.Append("f " + (t[i] + 1) + " " + (t[i + 1] + 1) + " " + (t[i + 2] + 1) + "\n");
            File.WriteAllText(path, sb.ToString()); Debug.Log("[ObjectBank] anchor exported to " + path);
        }
        catch (System.Exception ex) { Debug.LogError("[ObjectBank] anchor export failed: " + ex.Message); }
    }
#endif
}
