using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Unity.MLAgents;

/// <summary>
/// Morphology vector theta for the prosthetic hand: per-finger link-length scales, per-joint-group impedance
/// (stiffness k, damping b, inertia I) for the 14 finger groups and the 2 wrist axes, and a 14-bit finger actuation mask.
/// Values are sampled per episode (when morph/randomize is 1) or read from Academy environment parameters
/// (keys listed below). On the articulated hand (branch articulated-hand) the (k, b) pairs are PhysX force-mode drives.
///
/// Stiffness is sampled in PHYSICAL units (spike 2, 2026-09-22): k in N m/rad per joint class, log-uniform inside the
/// human active-stiffness ranges of results/011_artic2/stiffness_survey.md (MCP 0.5-6, PIP 0.3-3, DIP 0.1-1; thumb and
/// wrist ranges are unverified extrapolations), zeta uniform in [0.3, 1.0], b = 2 zeta sqrt(k I) with I the geometric
/// subtree inertia x the sampled inertia scale (which acts on the link masses). The natural frequency omega = sqrt(k / I)
/// is DERIVED (hundreds of rad/s at human-scale inertia) and only logged. Externally supplied (k, b, I) are checked against
/// plausibility bounds (k in plausibleStiffness, zeta <= maxPlausibleZeta); out-of-range requests are logged and counted
/// (OutOfRangeEvents) but applied as requested; non-physical values (I <= 0, k < 0, b < 0) are sanitized.
///
/// Environment-parameter keys (all optional; absent keys keep the sampled / serialized value):
///   morph/randomize (1 = sample per episode, 0 = use serialized values + overrides)
///   morph/len_{index,middle,ring,pinky,thumb}
///   morph/k_{group}, morph/b_{group}, morph/I_{group}   group = indexBase ... thumbEnd, wristFlex, wristPron
///   morph/mask_{group}  (finger groups only; 1 = actuated, 0 = held at the neutral pose)
/// </summary>
public class MorphologyManager : MonoBehaviour
{
    public const int FingerCount = 5;
    public const int FingerGroupCount = 14;
    public const int WristAxisCount = 2;
    public const int GroupCount = FingerGroupCount + WristAxisCount;   // 16 impedance groups
    public static readonly string[] GroupNames = {
        "indexBase","indexMiddle","indexEnd","middleBase","middleMiddle","middleEnd","ringBase","ringMiddle","ringEnd",
        "pinkyBase","pinkyMiddle","pinkyEnd","thumbBase","thumbEnd","wristFlex","wristPron" };
    public static readonly string[] FingerNames = { "index", "middle", "ring", "pinky", "thumb" };
    static readonly int[] k_GroupFinger = { 0,0,0, 1,1,1, 2,2,2, 3,3,3, 4,4 };

    [Header("Randomization (used when morph/randomize = 1)")]
    public bool randomizeByDefault = true;
    [Tooltip("Per-finger link-length scale range (applied to every segment of the finger).")]
    public Vector2 lengthScaleRange = new Vector2(0.8f, 1.2f);
    [Tooltip("Stiffness k (N m/rad) range of the finger base joints (MCP): human active / co-contraction range, Hajian & Howe 1997, Milner & Franklin 1998, Jindrich 2004.")]
    public Vector2 stiffnessRangeBase = new Vector2(0.5f, 6f);
    [Tooltip("Stiffness range of the finger middle joints (PIP): Milner & Franklin 1998 upper bound, Jindrich 2004.")]
    public Vector2 stiffnessRangeMiddle = new Vector2(0.3f, 3f);
    [Tooltip("Stiffness range of the fingertip joints (DIP): scaled from PIP (no direct data, unverified).")]
    public Vector2 stiffnessRangeEnd = new Vector2(0.1f, 1f);
    [Tooltip("Stiffness range of the thumb base (CMC/MCP): 2-3x the finger MCP range (unverified).")]
    public Vector2 stiffnessRangeThumbBase = new Vector2(1f, 15f);
    [Tooltip("Stiffness range of the thumb end (IP): taken equal to PIP (unverified).")]
    public Vector2 stiffnessRangeThumbEnd = new Vector2(0.3f, 3f);
    [Tooltip("Stiffness range of the two wrist axes (no survey data; unverified).")]
    public Vector2 wristStiffnessRange = new Vector2(1f, 10f);
    [Tooltip("Stiffness is sampled log-uniformly inside its range (the ranges span more than a decade).")]
    public bool logUniformStiffness = true;
    [Tooltip("Finger damping ratio zeta range; b = 2 zeta sqrt(k I).")]
    public Vector2 fingerZetaRange = new Vector2(0.3f, 1.0f);
    [Tooltip("Wrist damping ratio range.")]
    public Vector2 wristZetaRange = new Vector2(0.3f, 1.0f);
    [Tooltip("Reference damping ratio (non-randomized episodes).")]
    public float referenceZeta = 0.7f;
    [Tooltip("Inertia scale range, multiplying the geometric nominal inertia of each group.")]
    public Vector2 inertiaScaleRange = new Vector2(0.5f, 2.0f);
    [Tooltip("Probability that each finger group is actuated when drawing a mask.")]
    [Range(0f, 1f)] public float maskActiveProbability = 0.8f;
    [Tooltip("Mask draws must keep at least this many finger groups active (gate needs 6 contacts).")]
    public int minActiveGroups = 6;
    [Tooltip("Mask draws must keep at least one thumb group active (gate needs the thumb).")]
    public bool requireThumbActive = true;

    [Header("Plausibility bounds (out-of-range springs are logged, not rejected)")]
    [Tooltip("Plausibility bounds on k (N m/rad) for an externally supplied spring; outside them logs a warning.")]
    public Vector2 plausibleStiffness = new Vector2(0.01f, 30f);
    [Tooltip("Upper plausibility bound on zeta for an externally supplied spring; exceeding it logs a warning.")]
    [FormerlySerializedAs("maxStableZeta")] public float maxPlausibleZeta = 1.2f;
    [Tooltip("Material density (kg/m^3) used for the geometric nominal inertia of segments and palm.")]
    public float density = 1000f;

    [Header("Current theta (serialized defaults = reference hand; overwritten per episode when randomizing)")]
    public float[] lengthScale = { 1f, 1f, 1f, 1f, 1f };
    public float[] stiffness = new float[GroupCount];
    public float[] damping = new float[GroupCount];
    public float[] inertia = new float[GroupCount];
    public bool[] mask = { true, true, true, true, true, true, true, true, true, true, true, true, true, true };
    /// <summary>Per-group inertia scale of this episode (multiplies the geometric nominal inertia; link mass on the articulation).</summary>
    [System.NonSerialized] public float[] inertiaScale = { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };
    /// <summary>Articulated rig (branch articulated-hand): length scales rebuild the link skeleton instead of moving bone transforms.</summary>
    [System.NonSerialized] public ArticulatedHand rig;

    /// <summary>Number of externally supplied springs this episode that fell outside the plausibility bounds or had to be sanitized.</summary>
    public int OutOfRangeEvents { get; private set; }
    const int k_MaxLoggedOutOfRange = 10;   // warnings after this many are counted silently
    static int s_LoggedOutOfRange;
    public float[] NominalInertia { get; private set; } = new float[GroupCount];
    public float[] LinkLength { get; private set; } = new float[FingerGroupCount];   // world metres, after scaling
    public float HandSpan { get; private set; }
    public float HandSpanRef { get; private set; }
    /// <summary>Sum of the 14 finger link lengths (world metres) at the reference scales, measured once at Initialize.</summary>
    public float FingerLengthRef { get; private set; }
    /// <summary>Sum of the current finger link lengths over FingerLengthRef (1 for the reference hand).</summary>
    public float FingerLengthRatio { get { if (FingerLengthRef <= 1e-6f) return 1f; float t = 0f; for (int g = 0; g < FingerGroupCount; g++) t += LinkLength[g]; return t / FingerLengthRef; } }
    public bool Randomizing { get; private set; }
    public int ActiveCount { get { int n = 0; for (int g = 0; g < FingerGroupCount; g++) if (mask[g]) n++; return n; } }

    // geometry captured once at Initialize (unscaled)
    class Segment { public Transform joint; public CapsuleCollider capsule; public float capHeight0, capCenterY0, capRadius; public Transform[] children; public Vector3[] childPos0; }
    Segment[] m_Segments = new Segment[FingerGroupCount];
    Transform m_Palm; BoxCollider m_PalmBox; float m_PalmLength;
    bool m_Initialized;

    void CaptureSegments(Transform[][] groupJoints, Transform palm)
    {
        m_Palm = palm; m_PalmBox = palm != null ? palm.GetComponent<BoxCollider>() : null;
        for (int g = 0; g < FingerGroupCount; g++)
        {
            var joint = groupJoints[g].Length > 0 ? groupJoints[g][0] : null;
            var seg = new Segment { joint = joint };
            if (joint != null)
            {
                seg.capsule = joint.GetComponent<CapsuleCollider>();
                if (seg.capsule != null) { seg.capHeight0 = seg.capsule.height; seg.capCenterY0 = seg.capsule.center.y; seg.capRadius = seg.capsule.radius * Mathf.Max(joint.lossyScale.x, joint.lossyScale.z); }
                var kids = new List<Transform>(); foreach (Transform c in joint) kids.Add(c);
                seg.children = kids.ToArray(); seg.childPos0 = new Vector3[kids.Count];
                for (int i = 0; i < kids.Count; i++) seg.childPos0[i] = kids[i].localPosition;
            }
            m_Segments[g] = seg;
        }
        // palm length along the finger direction = the largest horizontal box extent (world), measured once
        m_PalmLength = m_PalmBox != null ? Mathf.Max(m_PalmBox.size.y * palm.lossyScale.y, m_PalmBox.size.z * palm.lossyScale.z) : 0.07f;
    }

    public void Initialize(Transform[][] groupJoints, Transform palm)
    {
        CaptureSegments(groupJoints, palm);
        for (int g = 0; g < GroupCount; g++) inertiaScale[g] = 1f;
        ApplyLengthScales(new float[] { 1f, 1f, 1f, 1f, 1f });
        HandSpanRef = ComputeHandSpan();
        FingerLengthRef = 0f; for (int g = 0; g < FingerGroupCount; g++) FingerLengthRef += LinkLength[g];
        ComputeNominalInertia();
        if (stiffness == null || stiffness.Length != GroupCount) stiffness = new float[GroupCount];
        if (damping == null || damping.Length != GroupCount) damping = new float[GroupCount];
        if (inertia == null || inertia.Length != GroupCount) inertia = new float[GroupCount];
        for (int g = 0; g < GroupCount; g++)   // reference hand: geometric mean of each class range, zeta = referenceZeta
        { inertia[g] = NominalInertia[g]; stiffness[g] = ReferenceStiffness(g); damping[g] = 2f * referenceZeta * Mathf.Sqrt(stiffness[g] * inertia[g]); }
        m_Initialized = true;
    }

    /// <summary>Samples or reads theta for the new episode and applies link scales. Call from OnEpisodeBegin before spawning.</summary>
    public void ApplyForEpisode()
    {
        if (!m_Initialized) return;
        var ep = Academy.Instance.EnvironmentParameters;
        Randomizing = ep.GetWithDefault("morph/randomize", randomizeByDefault ? 1f : 0f) > 0.5f;
        OutOfRangeEvents = 0;
        // (k, zeta) of this episode: sampled, or the values carried by the current (k, b, I) arrays (reference hand at Initialize)
        float[] k = new float[GroupCount], zeta = new float[GroupCount];
        for (int g = 0; g < GroupCount; g++) { k[g] = stiffness[g] > 0f ? stiffness[g] : ReferenceStiffness(g); zeta[g] = DampingRatio(g); if (zeta[g] <= 0f) zeta[g] = referenceZeta; }
        if (Randomizing) Sample(k, zeta);
        else for (int g = 0; g < GroupCount; g++) inertiaScale[g] = 1f;
        // env-param overrides (present keys win)
        for (int f = 0; f < FingerCount; f++) lengthScale[f] = ep.GetWithDefault("morph/len_" + FingerNames[f], lengthScale[f]);
        ApplyLengthScales(lengthScale);   // rebuilds the articulation when a rig is attached (link masses use inertiaScale)
        ComputeNominalInertia();
        for (int g = 0; g < GroupCount; g++)
        {
            float I0 = NominalInertia[g] * inertiaScale[g];
            float kg = ep.GetWithDefault("morph/k_" + GroupNames[g], k[g]);
            float bg = ep.GetWithDefault("morph/b_" + GroupNames[g], 2f * zeta[g] * Mathf.Sqrt(kg * I0));
            float I = ep.GetWithDefault("morph/I_" + GroupNames[g], I0);
            CheckPlausibility(GroupNames[g], ref kg, ref bg, ref I);
            stiffness[g] = kg; damping[g] = bg; inertia[g] = I;
            if (NominalInertia[g] > 1e-9f) inertiaScale[g] = I / NominalInertia[g];
        }
        for (int g = 0; g < FingerGroupCount; g++) mask[g] = ep.GetWithDefault("morph/mask_" + GroupNames[g], mask[g] ? 1f : 0f) > 0.5f;
        HandSpan = ComputeHandSpan();
    }

    /// <summary>Stiffness range (N m/rad) of a group's joint class.</summary>
    public Vector2 StiffnessRange(int g)
    {
        if (g >= FingerGroupCount) return wristStiffnessRange;
        if (g == 12) return stiffnessRangeThumbBase; if (g == 13) return stiffnessRangeThumbEnd;
        switch (g % 3) { case 0: return stiffnessRangeBase; case 1: return stiffnessRangeMiddle; default: return stiffnessRangeEnd; }
    }
    /// <summary>Reference-hand stiffness (N m/rad): geometric mean of the class range.</summary>
    public float ReferenceStiffness(int g) { var r = StiffnessRange(g); return Mathf.Sqrt(Mathf.Max(r.x, 1e-6f) * Mathf.Max(r.y, 1e-6f)); }
    float SampleStiffness(int g)
    {
        var r = StiffnessRange(g);
        if (!logUniformStiffness || r.x <= 0f) return Random.Range(r.x, r.y);
        return Mathf.Exp(Random.Range(Mathf.Log(r.x), Mathf.Log(r.y)));
    }

    /// <summary>Draw this episode's theta: link scales, inertia scales, (k, zeta) per group, actuation mask. Applied by ApplyForEpisode.</summary>
    void Sample(float[] k, float[] zeta)
    {
        for (int f = 0; f < FingerCount; f++) lengthScale[f] = Random.Range(lengthScaleRange.x, lengthScaleRange.y);
        for (int g = 0; g < GroupCount; g++)
        {
            bool wrist = g >= FingerGroupCount;
            k[g] = SampleStiffness(g);
            zeta[g] = wrist ? Random.Range(wristZetaRange.x, wristZetaRange.y) : Random.Range(fingerZetaRange.x, fingerZetaRange.y);
            inertiaScale[g] = Random.Range(inertiaScaleRange.x, inertiaScaleRange.y);
        }
        for (int attempt = 0; attempt < 100; attempt++)
        {
            int active = 0; bool thumb = false;
            for (int g = 0; g < FingerGroupCount; g++) { mask[g] = Random.value < maskActiveProbability; if (mask[g]) { active++; if (k_GroupFinger[g] == 4) thumb = true; } }
            if (active >= minActiveGroups && (!requireThumbActive || thumb)) return;
        }
        for (int g = 0; g < FingerGroupCount; g++) mask[g] = true;   // fallback: fully actuated
    }

    /// <summary>
    /// Plausibility check of an externally supplied (k, b, I): nothing is rejected, out-of-range k / zeta are logged and
    /// counted but applied as requested; non-physical values (I <= 0, k < 0, b < 0) are sanitized. The derived omega is logged.
    /// </summary>
    public void CheckPlausibility(string group, ref float k, ref float b, ref float I)
    {
        bool sanitized = false;
        if (I <= 1e-10f) { I = 1e-8f; sanitized = true; }
        if (k < 0f) { k = 0f; sanitized = true; }
        if (b < 0f) { b = 0f; sanitized = true; }
        if (sanitized || !IsPlausible(k, b, I, plausibleStiffness, maxPlausibleZeta))
        {
            OutOfRangeEvents++;
            if (s_LoggedOutOfRange < k_MaxLoggedOutOfRange)
            {
                s_LoggedOutOfRange++;
                float w = Mathf.Sqrt(k / I), z = w > 1e-6f ? b / (2f * I * w) : 0f;
                Debug.LogWarning("[MorphologyManager] " + (sanitized ? "non-physical spring sanitized" : "spring outside plausibility bounds") +
                    " for " + group + ": k=" + k.ToString("G4") + " N m/rad (bounds " + plausibleStiffness.x + ".." + plausibleStiffness.y + "), zeta=" + z.ToString("F2") +
                    " (bounds 0.." + maxPlausibleZeta + "), I=" + I.ToString("G4") + ", derived omega=" + w.ToString("F0") + " rad/s" + (sanitized ? "" : " - applied as requested") +
                    (s_LoggedOutOfRange == k_MaxLoggedOutOfRange ? " (further warnings suppressed; see OutOfRangeEvents)" : ""));
            }
        }
    }

    /// <summary>True when k is within kBounds (N m/rad) and zeta = b / (2 sqrt(k I)) within [0, maxZeta].</summary>
    public static bool IsPlausible(float k, float b, float I, Vector2 kBounds, float maxZeta)
    {
        if (I <= 0f || k < 0f || b < 0f) return false;
        float z = k > 1e-9f ? b / (2f * Mathf.Sqrt(k * I)) : 0f;
        return k >= kBounds.x && k <= kBounds.y && z >= 0f && z <= maxZeta;
    }

    void ApplyLengthScales(float[] s)
    {
        if (rig != null)
        {   // articulated rig: rebuild the link skeleton (anchor offsets and capsule lengths scaled, masses x inertiaScale), then re-capture it
            rig.Rebuild(s, inertiaScale);
            var joints = new Transform[FingerGroupCount][];
            for (int g = 0; g < FingerGroupCount; g++) joints[g] = rig.Groups[g] != null ? new Transform[] { rig.Groups[g].transform } : new Transform[0];
            CaptureSegments(joints, rig.PalmLink);
            for (int g = 0; g < FingerGroupCount; g++)
            {
                var seg = m_Segments[g]; if (seg.joint == null) continue;
                Transform child = null; foreach (Transform c in seg.joint) if (c.GetComponent<ArticulationBody>() != null) { child = c; break; }
                LinkLength[g] = child != null ? Vector3.Distance(seg.joint.position, child.position)
                    : (seg.capsule != null ? seg.capsule.center.y + 0.5f * seg.capsule.height : 0.02f);
            }
            return;
        }
        for (int g = 0; g < FingerGroupCount; g++)
        {
            var seg = m_Segments[g]; if (seg.joint == null) continue;
            float sc = s[k_GroupFinger[g]];
            for (int i = 0; i < seg.children.Length; i++) seg.children[i].localPosition = seg.childPos0[i] * sc;
            if (seg.capsule != null)
            {
                seg.capsule.height = seg.capHeight0 * sc;
                var c = seg.capsule.center; c.y = seg.capCenterY0 * sc; seg.capsule.center = c;
            }
            // link length (world): distance from this pivot to the child pivot, or the capsule far end for a fingertip
            float axisScale = seg.joint.lossyScale.y;
            LinkLength[g] = seg.children.Length > 0 && seg.children[0].localPosition.sqrMagnitude > 1e-12f
                ? seg.children[0].localPosition.magnitude * axisScale
                : (seg.capsule != null ? (seg.capsule.center.y + 0.5f * seg.capsule.height) * axisScale : 0.02f);
        }
        Physics.SyncTransforms();
    }

    float ComputeHandSpan()
    {
        // palm length + index chain (base, middle, end) + fingertip radius, in world metres
        float span = m_PalmLength;
        for (int g = 0; g < 3; g++) span += LinkLength[g];
        if (m_Segments[2].capsule != null) span += m_Segments[2].capRadius;
        return span;
    }

    void ComputeNominalInertia()
    {
        // segment mass from the capsule volume (cylinder part), inertia about the group's pivot including distal segments
        float[] mass = new float[FingerGroupCount]; Vector3[] centre = new Vector3[FingerGroupCount];
        for (int g = 0; g < FingerGroupCount; g++)
        {
            var seg = m_Segments[g]; if (seg.joint == null || seg.capsule == null) { mass[g] = 0.005f; centre[g] = seg.joint != null ? seg.joint.position : Vector3.zero; continue; }
            float r = seg.capRadius, h = seg.capsule.height * seg.joint.lossyScale.y;
            // rig: the PhysX link mass (capsule volume x density x inertiaScale) without the inertia scale, so NominalInertia stays geometric
            mass[g] = rig != null && rig.Groups[g] != null && inertiaScale[g] > 0f ? rig.Groups[g].mass / inertiaScale[g]
                    : density * Mathf.PI * r * r * h + density * 4f / 3f * Mathf.PI * r * r * r;
            centre[g] = seg.joint.TransformPoint(seg.capsule.center);
        }
        for (int g = 0; g < FingerGroupCount; g++)
        {
            var seg = m_Segments[g]; if (seg.joint == null) { NominalInertia[g] = 1e-6f; continue; }
            float I = 0f; int f = k_GroupFinger[g];
            for (int j = 0; j < FingerGroupCount; j++)
            {
                if (k_GroupFinger[j] != f || j < g) continue;   // own segment and distal segments of the same finger
                float d = Vector3.Distance(centre[j], seg.joint.position);
                float L = m_Segments[j].capsule != null ? m_Segments[j].capsule.height * m_Segments[j].joint.lossyScale.y : 0.02f;
                I += mass[j] * (d * d + L * L / 12f);
            }
            if (rig != null) foreach (var (eb, eg) in rig.ExtraLinks)   // links without a group (thumb proximal phalanx, fixed MCP): mass counts toward the groups proximal of it on the same finger
                if (k_GroupFinger[eg] == f && eg >= g && inertiaScale[eg] > 0f) { var ec = eb.GetComponent<CapsuleCollider>(); Vector3 c = ec != null ? eb.transform.TransformPoint(ec.center) : eb.transform.position; float d = Vector3.Distance(c, seg.joint.position); float L = ec != null ? ec.height : 0.02f; I += eb.mass / inertiaScale[eg] * (d * d + L * L / 12f); }
            NominalInertia[g] = Mathf.Max(I, 1e-8f);
        }
        // wrist: palm box + all fingers about the palm pivot
        float palmMass = rig != null && rig.Palm != null ? rig.Palm.mass : (m_PalmBox != null ? density * m_PalmBox.size.x * m_PalmBox.size.y * m_PalmBox.size.z * m_Palm.lossyScale.x * m_Palm.lossyScale.y * m_Palm.lossyScale.z : 0.3f);
        float Iw = 0f;
        if (m_Palm != null)
        {
            Vector3 pc = m_PalmBox != null ? m_Palm.TransformPoint(m_PalmBox.center) : m_Palm.position;
            float dp = Vector3.Distance(pc, m_Palm.position); Iw += palmMass * (dp * dp + m_PalmLength * m_PalmLength / 12f);
            for (int g = 0; g < FingerGroupCount; g++) { float d = Vector3.Distance(centre[g], m_Palm.position); Iw += mass[g] * d * d; }
            if (rig != null) foreach (var (eb, eg) in rig.ExtraLinks) { float d = Vector3.Distance(eb.transform.position, m_Palm.position); Iw += eb.mass / Mathf.Max(inertiaScale[eg], 1e-3f) * d * d; }
        }
        NominalInertia[FingerGroupCount] = Mathf.Max(Iw, 1e-6f);        // wrist flexion
        NominalInertia[FingerGroupCount + 1] = Mathf.Max(Iw * 0.5f, 1e-6f);   // pronation: roughly half (mass closer to the axis)
    }

    public int FingerOfGroup(int g) => g < FingerGroupCount ? k_GroupFinger[g] : 5;
    public float LengthScaleOfGroup(int g) => g < FingerGroupCount ? lengthScale[k_GroupFinger[g]] : 1f;
    /// <summary>Derived natural frequency omega = sqrt(k / I) (rad/s); logged only, not a sampled quantity.</summary>
    public float NaturalFrequency(int g) => inertia[g] > 0f ? Mathf.Sqrt(stiffness[g] / inertia[g]) : 0f;
    public float MeanStiffness(int from, int to) { float s = 0f; int n = 0; for (int g = from; g < to; g++) { s += stiffness[g]; n++; } return n > 0 ? s / n : 0f; }
    public float DampingRatio(int g) { float w = NaturalFrequency(g); return w > 1e-6f ? damping[g] / (2f * inertia[g] * w) : 0f; }
}
