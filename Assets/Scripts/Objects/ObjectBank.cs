// Object bank (run 013 prep, 2026-09-27): a pre-cooked bank of convex superquadric shapes that replaces the object's mesh + convex
// collider per episode. Attached at scene load to the "Cylinder"-tagged object (no scene change). The agent calls ApplyForEpisode
// from OnEpisodeBegin, after the object is made kinematic with this episode's mass and before SpawnCylinder places it.
//
// Draw mode = environment parameter "object/mode": 0 anchorOnly (default: NOTHING is touched, the scene behaves exactly as before),
// 1 trainUniform (bank indices 1-64, one UnityEngine.Random draw), 2 heldoutUniform (65-80, one draw), 3 fixedIndex ("object/index").
// "object/seed" names the bank the caller expects (BANK_SEED.txt of the generated bank); a mismatch is logged once.
//
// Bank files: Assets/Objects/Bank/bank.csv + obj_NNN.obj written by tools/objects/superquadric_bank.py (vertices in metres, Unity frame,
// local Y = length axis, X / Z = the cross-section the hand closes on). Index 0 is the anchor: the scene's own Mesh (Unity's built-in
// cylinder) with the scene's transform scale, kept as a Mesh reference rather than reloaded, so the anchor path is the untouched object;
// in the Editor the anchor is exported once to obj_000.obj (mesh vertices x scale) for the vertex-diff proof against the generator.
//
// What a swap does (index != 0): MeshFilter.sharedMesh and MeshCollider.sharedMesh <- the bank Mesh (convex, cooked once at load with
// Physics.BakeMesh), transform.localScale <- 1 (bank meshes are in metres), the collider's contactOffset re-applied, the Rigidbody's
// centre of mass and inertia tensor reset (PhysX recomputes them from the new convex shape under uniform density; the mass draw is the
// agent's), then the rest half-height agent.ObjectHalfHeight <- position.y - collider.bounds.min.y at the object's current rotation
// (the spawn keeps the current X/Z tilt and randomizes only the yaw about world Y, which preserves the vertical extent), so the shape
// rests 0.5 mm above the pedestal on its lowest point, length axis vertical like the anchor. Tag, layer, physics material, Rigidbody
// settings, friction, mass draw, observations, actions and rewards are not touched. Swapping back to index 0 restores the scene Mesh,
// the scene scale and the Initialize half-height, so fixedIndex 0 is the anchor through the swap path.
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Unity.MLAgents;
using UnityEngine;

public class ObjectBank : MonoBehaviour
{
    public const string BankDir = "Objects/Bank";          // under Application.dataPath (Editor / project runs; a player build would need StreamingAssets)
    public const int TrainFrom = 1, TrainTo = 64, HeldoutFrom = 65, HeldoutTo = 80;
    public enum DrawMode { anchorOnly = 0, trainUniform = 1, heldoutUniform = 2, fixedIndex = 3 }

    [System.Serializable]
    public struct Entry
    {
        public int index; public string split, file;
        public float a1mm, a2mm, a3mm, e1, e2, volumeCm3, extentXmm, extentYmm, extentZmm;
        public Mesh mesh;
    }

    public static ObjectBank Instance { get; private set; }
    public readonly List<Entry> Entries = new List<Entry>();
    public int BankSeed { get; private set; } = -1;
    public bool Loaded { get; private set; }
    public DrawMode CurrentMode { get; private set; } = DrawMode.anchorOnly;
    public int CurrentIndex { get; private set; }             // bank index of the object in the current episode (0 = anchor)
    public Entry Current => CurrentIndex >= 0 && CurrentIndex < Entries.Count ? Entries[CurrentIndex] : default;
    public int SwapCount { get; private set; }

    MeshFilter mf; MeshCollider mc; Rigidbody rb;
    Mesh anchorMesh; Vector3 anchorScale; float anchorHalfHeight = float.NaN; int applied = 0; bool seedWarned;

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
        anchorMesh = mc.sharedMesh != null ? mc.sharedMesh : mf.sharedMesh; anchorScale = transform.localScale;
        LoadBank();
#if UNITY_EDITOR
        ExportAnchorIfMissing();
#endif
    }

    string Dir => Path.Combine(Application.dataPath, BankDir);

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
        Debug.Log("[ObjectBank] loaded " + Entries.Count + " entries (seed " + BankSeed + ") from " + Dir + "; anchor mesh '" + (anchorMesh != null ? anchorMesh.name : "null") + "' scale " + anchorScale.ToString("F3"));
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
        // orientation guard for the renderer only (the convex hull ignores winding): flip if the first face normal points toward the centroid
        var c = m.bounds.center; var a = v[t[0]]; var n = Vector3.Cross(v[t[1]] - a, v[t[2]] - a);
        if (Vector3.Dot(n, (v[t[0]] + v[t[1]] + v[t[2]]) / 3f - c) < 0f) { for (int i = 0; i < t.Count; i += 3) { int tmp = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = tmp; } m.SetTriangles(t, 0); m.RecalculateNormals(); }
        return m;
    }

    /// <summary>Called by ArmGraspAgent.OnEpisodeBegin before SpawnCylinder. Reads object/mode, object/index, object/seed.</summary>
    public void ApplyForEpisode(ArmGraspAgent agent)
    {
        if (float.IsNaN(anchorHalfHeight) && agent != null) anchorHalfHeight = agent.ObjectHalfHeight;   // the Initialize value of the untouched scene object
        var ep = Academy.Instance.EnvironmentParameters;
        int mode = Mathf.RoundToInt(ep.GetWithDefault("object/mode", 0f));
        int seed = Mathf.RoundToInt(ep.GetWithDefault("object/seed", -1f));
        if (seed >= 0 && BankSeed >= 0 && seed != BankSeed && !seedWarned) { seedWarned = true; Debug.LogError("[ObjectBank] object/seed " + seed + " requested but the loaded bank is seed " + BankSeed); }
        CurrentMode = (DrawMode)Mathf.Clamp(mode, 0, 3);
        if (CurrentMode == DrawMode.anchorOnly) { if (applied != 0) Apply(0, agent); CurrentIndex = 0; return; }   // default: no draw, no change (byte-identical path); restores the anchor only if a previous episode swapped
        if (!Loaded) { if (!seedWarned) { seedWarned = true; Debug.LogError("[ObjectBank] bank not loaded; staying on the anchor"); } CurrentIndex = 0; return; }
        int idx;
        switch (CurrentMode)
        {
            case DrawMode.trainUniform: idx = TrainFrom + Random.Range(0, TrainTo - TrainFrom + 1); break;
            case DrawMode.heldoutUniform: idx = HeldoutFrom + Random.Range(0, HeldoutTo - HeldoutFrom + 1); break;
            default: idx = Mathf.Clamp(Mathf.RoundToInt(ep.GetWithDefault("object/index", 0f)), 0, Entries.Count - 1); break;
        }
        Apply(idx, agent);
    }

    void Apply(int idx, ArmGraspAgent agent)
    {
        if (mf == null || mc == null) return;
        var e = Entries[idx];
        if (idx == 0)
        {
            if (applied != 0) { float off = mc.contactOffset; mf.sharedMesh = anchorMesh; mc.sharedMesh = anchorMesh; transform.localScale = anchorScale; mc.contactOffset = off; if (rb != null) { rb.ResetCenterOfMass(); rb.ResetInertiaTensor(); } Physics.SyncTransforms(); SwapCount++; }
            if (agent != null && !float.IsNaN(anchorHalfHeight)) agent.ObjectHalfHeight = anchorHalfHeight;
            applied = 0; CurrentIndex = 0; return;
        }
        if (applied != idx)
        {
            float off = mc.contactOffset;
            mf.sharedMesh = e.mesh; mc.sharedMesh = e.mesh; mc.convex = true; transform.localScale = Vector3.one; mc.contactOffset = off;
            if (rb != null) { rb.ResetCenterOfMass(); rb.ResetInertiaTensor(); }
            SwapCount++;
        }
        Physics.SyncTransforms();
        if (agent != null) agent.ObjectHalfHeight = transform.position.y - mc.bounds.min.y;   // lowest point at the current (carried) tilt; yaw about world Y keeps it
        applied = idx; CurrentIndex = idx;
    }

#if UNITY_EDITOR
    void ExportAnchorIfMissing()
    {
        try
        {
            string path = Path.Combine(Dir, "obj_000.obj"); if (File.Exists(path) || anchorMesh == null) return;
            Directory.CreateDirectory(Dir);
            var v = anchorMesh.vertices; var t = anchorMesh.triangles; var sb = new System.Text.StringBuilder();
            sb.Append("# anchor: the scene object's mesh '" + anchorMesh.name + "' (" + v.Length + " vertices, " + t.Length / 3 + " triangles) x transform scale " + anchorScale.ToString("F4") + ", exported by ObjectBank\n# units: metres, Unity frame\n");
            foreach (var p in v) sb.Append(string.Format(CultureInfo.InvariantCulture, "v {0:F7} {1:F7} {2:F7}\n", p.x * anchorScale.x, p.y * anchorScale.y, p.z * anchorScale.z));
            for (int i = 0; i < t.Length; i += 3) sb.Append("f " + (t[i] + 1) + " " + (t[i + 1] + 1) + " " + (t[i + 2] + 1) + "\n");
            File.WriteAllText(path, sb.ToString()); Debug.Log("[ObjectBank] anchor exported to " + path);
        }
        catch (System.Exception ex) { Debug.LogError("[ObjectBank] anchor export failed: " + ex.Message); }
    }
#endif
}
