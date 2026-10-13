#if UNITY_EDITOR
// Morphology viewer (Editor only, compiled out of players): shows the run-014 hand compositions (3, 4, 5, 6a, 6b, 7 digits) and their
// actuation-mask states in Play mode, without a trainer or a policy. Launched only by the menu item ARAI/Morphology Viewer
// (Assets/Editor/MorphologyViewerMenu.cs), which sets a SessionState flag, opens Dynamic_Scene and enters Play; nothing in the scene is edited.
// The Editor resolves the legacy layout (no --topology slots on its command line, and Temp/gates.json would boot the harness), so the
// viewer disables the scene's ArmGraspAgent for the session and drives its ArticulatedHand directly: ArticulatedHand.Rebuild with the
// composition's digit existence at length / inertia scale 1, then the agent's own CacheLinks and ConfigureDrives (by reflection), so
// drive gains, joint limits and the masked-group stiff hold at the neutral pose are exactly the agent's. k = the class reference
// stiffness, zeta = referenceZeta (MorphologyManager.Initialize). The skeleton is drawn as its colliders (the skinned mesh has five
// fingers only and is hidden for the session); masked groups are tinted red. T runs a copy of the harness's close-until-contact driver
// (ArticulatedGates.ControllerStep, private there) with the harness's own Config defaults; it stays in Diagnostics and never touches
// ArmGraspAgent paths beyond the public setpoint setter the harness itself uses. Docs: results/014/VIEWER.md.
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

public class MorphologyViewer : MonoBehaviour
{
    public const string SessionKey = "ARAI.MorphologyViewer.Spawn";
    public static MorphologyViewer Instance { get; private set; }
    public static readonly (string name, int digits, bool secondThumb)[] Compositions = { ("3", 3, false), ("4", 4, false), ("5", 5, false), ("6a", 6, false), ("6b", 6, true), ("7", 7, false) };
    const float AutoPeriod = 3f;

    ArmGraspAgent agent; ArticulatedHand hand; MorphologyManager mm;
    MethodInfo miCacheLinks, miConfigureDrives;
    float[] refK, refB;
    readonly ArticulatedGates.Config drv = new ArticulatedGates.Config();   // the harness's close-until-contact defaults (rate, preload, caps)

    public int Composition { get; private set; } = 2;
    public int MaskState { get; private set; }   // 0 = all on, i = i-th present group off
    public readonly List<int> PresentGroups = new List<int>();
    bool autoCycle; float nextAuto; bool closing;
    readonly bool[] ctrlContact = new bool[HandLayout.MaxFingerGroups]; readonly float[] ctrlTarget = new float[HandLayout.MaxFingerGroups];

    readonly List<GameObject> drawn = new List<GameObject>();
    readonly Dictionary<int, List<Renderer>> groupRenderers = new Dictionary<int, List<Renderer>>();
    Material matLink, matMasked, matPalm;
    string info = ""; GUIStyle style;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (!UnityEditor.SessionState.GetBool(SessionKey, false)) return;
        UnityEditor.SessionState.SetBool(SessionKey, false);   // one launch per menu click; an ordinary Play does not spawn the viewer
        if (File.Exists("Temp/gates.json")) { Debug.LogError("[MorphViewer] Temp/gates.json present (harness pass): viewer not spawned"); return; }
        new GameObject("MorphologyViewer (runtime only)").AddComponent<MorphologyViewer>();
    }

    void Start()
    {
        Instance = this;
        foreach (var a in FindObjectsByType<ArmGraspAgent>(FindObjectsSortMode.None)) if (a.isActiveAndEnabled) { agent = a; break; }
        if (agent == null || agent.Hand == null || agent.Morphology == null) { Debug.LogError("[MorphViewer] no active ArmGraspAgent with an ArticulatedHand and a MorphologyManager"); enabled = false; return; }
        hand = agent.Hand; mm = agent.Morphology;
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        miCacheLinks = typeof(ArmGraspAgent).GetMethod("CacheLinks", flags); miConfigureDrives = typeof(ArmGraspAgent).GetMethod("ConfigureDrives", flags);
        if (miCacheLinks == null || miConfigureDrives == null) { Debug.LogError("[MorphViewer] ArmGraspAgent.CacheLinks / ConfigureDrives not found"); enabled = false; return; }

        // the viewer owns the hand for the session: no episodes, no policy, no overlay reading the (now stale) agent state
        var req = agent.GetComponent<Unity.MLAgents.DecisionRequester>(); if (req != null) req.enabled = false;
        agent.enabled = false;
        var watch = GameObject.Find("MorphologyWatchOverlay (runtime only)"); if (watch != null) watch.SetActive(false);
        foreach (var smr in FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None)) if (smr.transform.IsChildOf(agent.transform) || smr.transform.IsChildOf(hand.transform)) smr.enabled = false;

        // nominal theta: length / inertia scale 1, every group actuated, class reference k with zeta = referenceZeta
        mm.randomizeByDefault = false;
        var joints = new Transform[HandLayout.MaxFingerGroups][];
        for (int g = 0; g < HandLayout.MaxFingerGroups; g++) joints[g] = hand.Groups[g] != null ? new[] { hand.Groups[g].transform } : new Transform[0];
        mm.Initialize(joints, hand.PalmLink);
        refK = (float[])mm.stiffness.Clone(); refB = (float[])mm.damping.Clone();
        // the legacy layout computes nominal inertia for the five-digit groups only: the extra finger / second thumb take the damping of their
        // analogue group (pinky / thumb), which is what their inertia would be at equal link geometry
        for (int g = HandLayout.LegacyFingerGroups; g < HandLayout.MaxFingerGroups; g++)
        {
            int mg = HandLayout.MorphIndex(g), analogue = g < 17 ? 9 + (g - 14) : 12 + (g - 17);
            float I = mm.NominalInertia[analogue]; refB[mg] = 2f * mm.referenceZeta * Mathf.Sqrt(Mathf.Max(refK[mg] * I, 0f));
        }

        matLink = MakeMat(new Color(0.72f, 0.80f, 0.92f)); matPalm = MakeMat(new Color(0.55f, 0.62f, 0.75f)); matMasked = MakeMat(new Color(0.90f, 0.15f, 0.12f));
        Debug.Log("[MorphViewer] started: agent " + agent.name + " disabled for the session; layout " + HandLayout.Selected + " (the viewer builds slot compositions directly)");
        Select(2);
    }

    static Material MakeMat(Color c) { var sh = Shader.Find("Standard") ?? Shader.Find("Sprites/Default"); return new Material(sh) { color = c }; }

    // ---- composition / mask ----
    public void Select(int c)
    {
        Composition = Mathf.Clamp(c, 0, Compositions.Length - 1); var (name, digits, sixB) = Compositions[Composition];
        var exists = HandLayout.Composition(digits, sixB);
        var ones7 = new float[HandLayout.MaxDigits]; for (int i = 0; i < ones7.Length; i++) ones7[i] = 1f;
        var ones21 = new float[HandLayout.MaxMorphGroups]; for (int i = 0; i < ones21.Length; i++) ones21[i] = 1f;
        try
        {
            hand.Rebuild(ones7, ones21, exists);
            miCacheLinks.Invoke(agent, null);
        }
        catch (System.Exception e) { Debug.LogError("[MorphViewer] rebuild of composition " + name + " failed: " + (e.InnerException ?? e)); return; }
        PresentGroups.Clear(); for (int g = 0; g < HandLayout.MaxFingerGroups; g++) if (hand.Present(g)) PresentGroups.Add(g);
        closing = false; for (int g = 0; g < HandLayout.MaxFingerGroups; g++) { ctrlContact[g] = false; ctrlTarget[g] = 0f; }
        MaskState = 0; ApplyMask(false);
        Draw(); Frame();
        float mass = HandMass(); int links = LinkCount();
        Debug.Log("[MorphViewer] composition " + name + " built: slots " + HandLayout.SlotString(exists) + ", links " + links + ", hand mass " + (mass * 1000f).ToString("F0") + " g, palm width " + (hand.PalmWidth * 1000f).ToString("F1")
                  + " mm, groups " + PresentGroups.Count + " (" + GroupList(PresentGroups) + "), rebuild " + hand.RebuildCount);
    }

    public void CycleMask() { MaskState = (MaskState + 1) % (PresentGroups.Count + 1); ApplyMask(true); }

    void ApplyMask(bool log)
    {
        for (int g = 0; g < mm.mask.Length; g++) mm.mask[g] = true;
        if (MaskState > 0) mm.mask[PresentGroups[MaskState - 1]] = false;
        System.Array.Copy(refK, mm.stiffness, refK.Length); System.Array.Copy(refB, mm.damping, refB.Length);
        miConfigureDrives.Invoke(agent, null);   // masked group: stiff hold at the neutral pose (ArticulatedHand.ConfigureGroup)
        for (int g = 0; g < HandLayout.MaxFingerGroups; g++) { ctrlContact[g] = false; if (hand.Present(g) && mm.mask[g]) agent.SetGroupSetpoint(g, closing ? ctrlTarget[g] : 0f); }
        Tint();
        if (log) Debug.Log("[MorphViewer] composition " + Compositions[Composition].name + " mask state " + MaskState + "/" + PresentGroups.Count + ": " + (MaskState == 0 ? "all on" : HandLayout.GroupNames[PresentGroups[MaskState - 1]] + " off"));
    }

    public void ToggleDriver() { closing = !closing; for (int g = 0; g < HandLayout.MaxFingerGroups; g++) ctrlContact[g] = false; Debug.Log("[MorphViewer] driver " + (closing ? "close-until-contact" : "open")); }

    // ---- close-until-contact driver: copy of ArticulatedGates.ControllerStep (power grasp, thumb active), plus a re-open at the same rate ----
    float CapDeg(int g) { switch (HandLayout.GroupClass(g)) { case 3: return drv.capThumbBaseDeg; case 4: return drv.capThumbEndDeg; case 0: return drv.capBaseDeg; case 1: return drv.capMiddleDeg; default: return drv.capEndDeg; } }
    void FixedUpdate()
    {
        if (hand == null || !hand.Built) return;
        var touching = new bool[HandLayout.MaxFingerGroups];
        foreach (var c in hand.Contacts) if (c.group >= 0 && c.group < touching.Length) touching[c.group] = true;   // LinkContact: contacts with the Cylinder-tagged object
        hand.ClearContacts();
        float dt = Time.fixedDeltaTime;
        foreach (int g in PresentGroups)
        {
            if (!mm.mask[g]) continue;   // masked: held by its drive at the neutral pose
            float sign = HandLayout.IsThumbGroup(g) ? 1f : -1f;
            if (closing)
            {
                if (!ctrlContact[g] && touching[g]) { ctrlContact[g] = true; ctrlTarget[g] = hand.GroupAngle(g) + sign * drv.preloadDeg; }
                else if (!ctrlContact[g]) ctrlTarget[g] = Mathf.MoveTowards(ctrlTarget[g], sign * CapDeg(g), drv.closeRateDegPerSec * dt);
            }
            else ctrlTarget[g] = Mathf.MoveTowards(ctrlTarget[g], 0f, drv.closeRateDegPerSec * dt);
            agent.SetGroupSetpoint(g, ctrlTarget[g]);
        }
    }

    // ---- keys ----
    void Update()
    {
        if (hand == null) return;
        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) Select(0);
        if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) Select(1);
        if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5)) Select(2);
        if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6)) Select(Composition == 3 ? 4 : 3);
        if (Input.GetKeyDown(KeyCode.Alpha7) || Input.GetKeyDown(KeyCode.Keypad7)) Select(5);
        if (Input.GetKeyDown(KeyCode.A)) { autoCycle = !autoCycle; nextAuto = Time.unscaledTime + AutoPeriod; }
        if (Input.GetKeyDown(KeyCode.M)) CycleMask();
        if (Input.GetKeyDown(KeyCode.T)) ToggleDriver();
        if (Input.GetKeyDown(KeyCode.R)) { autoCycle = false; Select(2); }
        if (autoCycle && Time.unscaledTime >= nextAuto) { nextAuto = Time.unscaledTime + AutoPeriod; Select((Composition + 1) % Compositions.Length); }
    }

    // ---- drawing ----
    void Draw()
    {
        foreach (var go in drawn) if (go != null) Destroy(go); drawn.Clear(); groupRenderers.Clear();
        var bodies = new List<(ArticulationBody b, int g)> { (hand.Forearm, -2), (hand.Palm, -1) };
        for (int g = 0; g < HandLayout.MaxFingerGroups; g++) if (hand.Groups[g] != null) bodies.Add((hand.Groups[g], g));
        foreach (var (eb, eg) in hand.ExtraLinks) bodies.Add((eb, eg));
        foreach (var (b, g) in bodies)
        {
            if (b == null) continue;
            var cap = b.GetComponent<CapsuleCollider>(); var box = b.GetComponent<BoxCollider>(); GameObject p = null;
            if (cap != null)
            {
                p = GameObject.CreatePrimitive(PrimitiveType.Capsule); Destroy(p.GetComponent<Collider>());
                p.transform.SetParent(b.transform, false); p.transform.localPosition = cap.center;
                p.transform.localRotation = cap.direction == 0 ? Quaternion.Euler(0f, 0f, 90f) : cap.direction == 2 ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
                p.transform.localScale = new Vector3(2f * cap.radius, 0.5f * Mathf.Max(cap.height, 2f * cap.radius), 2f * cap.radius);
            }
            else if (box != null)
            {
                p = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(p.GetComponent<Collider>());
                p.transform.SetParent(b.transform, false); p.transform.localPosition = box.center; p.transform.localRotation = Quaternion.identity; p.transform.localScale = box.size;
            }
            if (p == null) continue;
            p.name = "viewer_" + b.name; var r = p.GetComponent<Renderer>(); r.sharedMaterial = g < 0 ? matPalm : matLink; drawn.Add(p);
            if (g >= 0) { if (!groupRenderers.TryGetValue(g, out var l)) groupRenderers[g] = l = new List<Renderer>(); l.Add(r); }
        }
        Tint();
    }

    void Tint() { foreach (var kv in groupRenderers) foreach (var r in kv.Value) if (r != null) r.sharedMaterial = mm.mask[kv.Key] ? matLink : matMasked; }

    void Frame()
    {
        var cam = Camera.main; if (cam == null || hand.Palm == null) return;
        var palm = hand.Palm.transform;
        var mid = hand.Groups[3] != null ? hand.Groups[3].transform.position : palm.position + palm.up * 0.1f;
        Vector3 fingers = (mid - palm.position).normalized, normal = palm.right;   // palm.right = palmar face normal (ArticulatedGates palmFace)
        Vector3 centre = palm.position + fingers * 0.10f;
        cam.nearClipPlane = 0.01f; cam.fieldOfView = 40f;
        // palmar view raised toward the fingertips: both thumbs (6b / 7 ulnar thumb) and every finger stay clear; the scene's object projects over the palm
        cam.transform.position = centre + (0.80f * normal + 0.60f * fingers).normalized * 0.55f;
        cam.transform.LookAt(centre, fingers);
    }

    // ---- readouts ----
    float HandMass() { float m = hand.Palm != null ? hand.Palm.mass : 0f; foreach (int g in PresentGroups) m += hand.Groups[g].mass; foreach (var (eb, _) in hand.ExtraLinks) if (eb != null) m += eb.mass; return m; }
    int LinkCount() => (hand.Palm != null ? 1 : 0) + PresentGroups.Count + hand.ExtraLinks.Count;
    static string GroupList(List<int> gs) { var s = new StringBuilder(); foreach (int g in gs) { if (s.Length > 0) s.Append(", "); s.Append(HandLayout.GroupNames[g]); } return s.ToString(); }

    void OnGUI()
    {
        if (hand == null || !hand.Built) return;
        if (style == null) { style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 14, richText = true, wordWrap = false }; style.normal.textColor = Color.white; }
        var masked = new List<int>(); foreach (int g in PresentGroups) if (!mm.mask[g]) masked.Add(g);
        var sb = new StringBuilder();
        sb.Append("<b>Morphology viewer</b>   composition <b>").Append(Compositions[Composition].name).Append("</b>  (slots ").Append(HandLayout.SlotString(hand.Exists)).Append(")\n");
        sb.Append("links ").Append(LinkCount()).Append("   hand mass ").Append((HandMass() * 1000f).ToString("F0")).Append(" g   palm width ").Append((hand.PalmWidth * 1000f).ToString("F1")).Append(" mm\n");
        sb.Append("groups ").Append(PresentGroups.Count - masked.Count).Append('/').Append(PresentGroups.Count).Append(" powered   masked: ")
          .Append(masked.Count == 0 ? "none" : "<color=#FF6060>" + GroupList(masked) + "</color>").Append("   (mask state ").Append(MaskState).Append('/').Append(PresentGroups.Count).Append(")\n");
        sb.Append("driver ").Append(closing ? "<color=#7CFC00>closing (until contact)</color>" : "open").Append("   auto-cycle ").Append(autoCycle ? "<color=#7CFC00>ON</color>" : "off").Append("\n\n");
        sb.Append("keys: 3 4 5 7 digits | 6 toggle 6a / 6b | A auto-cycle (3 s)\n      M next mask state | T close / open | R reset (5, all on)");
        var content = new GUIContent(sb.ToString()); var size = style.CalcSize(content);
        GUI.Box(new Rect(8f, 8f, size.x + 12f, size.y + 8f), content, style);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }
}
#endif
