using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;

/// <summary>
/// Morphology vector theta for the prosthetic hand: per-finger link-length scales, per-joint-group impedance
/// (stiffness k, damping b, inertia I) for the 14 finger groups and the 2 wrist axes, and a 14-bit finger actuation mask.
/// Values are sampled per episode (when morph/randomize is 1) or read from Academy environment parameters
/// (keys listed below); any externally supplied (k, b, I) is projected into the stability region of the
/// semi-implicit Euler integrator used by ArmGraspAgent (h = fixed timestep): with a = h^2 k / I and c = h b / I the
/// update is stable iff 0 < c < 2 and a < 4 - 2c, i.e. zeta*omega*h < 1 and (omega*h)^2 < 4 (1 - zeta*omega*h).
/// Sampling is done in (omega, zeta, I-scale) so the drawn springs are always inside the region.
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
    [Tooltip("Finger natural frequency omega (rad/s) range; k = I omega^2.")]
    public Vector2 fingerOmegaRange = new Vector2(12f, 40f);
    [Tooltip("Finger damping ratio zeta range; b = 2 zeta I omega.")]
    public Vector2 fingerZetaRange = new Vector2(0.4f, 0.9f);
    [Tooltip("Wrist natural frequency omega (rad/s) range (heavier body, slower).")]
    public Vector2 wristOmegaRange = new Vector2(8f, 25f);
    [Tooltip("Wrist damping ratio range.")]
    public Vector2 wristZetaRange = new Vector2(0.5f, 0.9f);
    [Tooltip("Inertia scale range, multiplying the geometric nominal inertia of each group.")]
    public Vector2 inertiaScaleRange = new Vector2(0.5f, 2.0f);
    [Tooltip("Probability that each finger group is actuated when drawing a mask.")]
    [Range(0f, 1f)] public float maskActiveProbability = 0.8f;
    [Tooltip("Mask draws must keep at least this many finger groups active (gate needs 6 contacts).")]
    public int minActiveGroups = 6;
    [Tooltip("Mask draws must keep at least one thumb group active (gate needs the thumb).")]
    public bool requireThumbActive = true;

    [Header("Stability projection (semi-implicit Euler at the fixed timestep)")]
    [Tooltip("Upper bound on omega (rad/s) enforced on any externally supplied spring.")]
    public float maxStableOmega = 40f;
    [Tooltip("Upper bound on zeta enforced on any externally supplied spring.")]
    public float maxStableZeta = 0.9f;
    [Tooltip("Material density (kg/m^3) used for the geometric nominal inertia of segments and palm.")]
    public float density = 1000f;

    [Header("Current theta (serialized defaults = reference hand; overwritten per episode when randomizing)")]
    public float[] lengthScale = { 1f, 1f, 1f, 1f, 1f };
    public float[] stiffness = new float[GroupCount];
    public float[] damping = new float[GroupCount];
    public float[] inertia = new float[GroupCount];
    public bool[] mask = { true, true, true, true, true, true, true, true, true, true, true, true, true, true };

    /// <summary>Number of stability projections applied this episode (externally supplied springs outside the region).</summary>
    public int ProjectionEvents { get; private set; }
    public float[] NominalInertia { get; private set; } = new float[GroupCount];
    public float[] LinkLength { get; private set; } = new float[FingerGroupCount];   // world metres, after scaling
    public float HandSpan { get; private set; }
    public float HandSpanRef { get; private set; }
    public bool Randomizing { get; private set; }
    public int ActiveCount { get { int n = 0; for (int g = 0; g < FingerGroupCount; g++) if (mask[g]) n++; return n; } }

    // geometry captured once at Initialize (unscaled)
    class Segment { public Transform joint; public CapsuleCollider capsule; public float capHeight0, capCenterY0, capRadius; public Transform[] children; public Vector3[] childPos0; }
    Segment[] m_Segments = new Segment[FingerGroupCount];
    Transform m_Palm; BoxCollider m_PalmBox; float m_PalmLength;
    bool m_Initialized;

    public void Initialize(Transform[][] groupJoints, Transform palm)
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
        m_PalmLength = m_PalmBox != null ? Mathf.Max(m_PalmBox.size.y * palm.lossyScale.y, m_PalmBox.size.z * palm.lossyScale.z) : 0.19f;
        ApplyLengthScales(new float[] { 1f, 1f, 1f, 1f, 1f });
        HandSpanRef = ComputeHandSpan();
        ComputeNominalInertia();
        if (stiffness == null || stiffness.Length != GroupCount) stiffness = new float[GroupCount];
        if (damping == null || damping.Length != GroupCount) damping = new float[GroupCount];
        if (inertia == null || inertia.Length != GroupCount) inertia = new float[GroupCount];
        for (int g = 0; g < GroupCount; g++)
            if (inertia[g] <= 0f || stiffness[g] <= 0f)   // serialized defaults absent: reference spring omega = 25, zeta = 0.7
            { inertia[g] = NominalInertia[g]; float w = g < FingerGroupCount ? 25f : 15f; stiffness[g] = inertia[g] * w * w; damping[g] = 2f * 0.7f * inertia[g] * w; }
        m_Initialized = true;
    }

    /// <summary>Samples or reads theta for the new episode and applies link scales. Call from OnEpisodeBegin before spawning.</summary>
    public void ApplyForEpisode()
    {
        if (!m_Initialized) return;
        var ep = Academy.Instance.EnvironmentParameters;
        Randomizing = ep.GetWithDefault("morph/randomize", randomizeByDefault ? 1f : 0f) > 0.5f;
        ProjectionEvents = 0;
        if (Randomizing) Sample();
        // env-param overrides (present keys win)
        for (int f = 0; f < FingerCount; f++) lengthScale[f] = ep.GetWithDefault("morph/len_" + FingerNames[f], lengthScale[f]);
        ApplyLengthScales(lengthScale);
        ComputeNominalInertia();
        for (int g = 0; g < GroupCount; g++)
        {
            float k = ep.GetWithDefault("morph/k_" + GroupNames[g], stiffness[g]);
            float b = ep.GetWithDefault("morph/b_" + GroupNames[g], damping[g]);
            float I = ep.GetWithDefault("morph/I_" + GroupNames[g], inertia[g]);
            Project(ref k, ref b, ref I);
            stiffness[g] = k; damping[g] = b; inertia[g] = I;
        }
        for (int g = 0; g < FingerGroupCount; g++) mask[g] = ep.GetWithDefault("morph/mask_" + GroupNames[g], mask[g] ? 1f : 0f) > 0.5f;
        HandSpan = ComputeHandSpan();
    }

    void Sample()
    {
        for (int f = 0; f < FingerCount; f++) lengthScale[f] = Random.Range(lengthScaleRange.x, lengthScaleRange.y);
        ApplyLengthScales(lengthScale);
        ComputeNominalInertia();
        for (int g = 0; g < GroupCount; g++)
        {
            bool wrist = g >= FingerGroupCount;
            float w = wrist ? Random.Range(wristOmegaRange.x, wristOmegaRange.y) : Random.Range(fingerOmegaRange.x, fingerOmegaRange.y);
            float z = wrist ? Random.Range(wristZetaRange.x, wristZetaRange.y) : Random.Range(fingerZetaRange.x, fingerZetaRange.y);
            float I = NominalInertia[g] * Random.Range(inertiaScaleRange.x, inertiaScaleRange.y);
            inertia[g] = I; stiffness[g] = I * w * w; damping[g] = 2f * z * I * w;
        }
        for (int attempt = 0; attempt < 100; attempt++)
        {
            int active = 0; bool thumb = false;
            for (int g = 0; g < FingerGroupCount; g++) { mask[g] = Random.value < maskActiveProbability; if (mask[g]) { active++; if (k_GroupFinger[g] == 4) thumb = true; } }
            if (active >= minActiveGroups && (!requireThumbActive || thumb)) return;
        }
        for (int g = 0; g < FingerGroupCount; g++) mask[g] = true;   // fallback: fully actuated
    }

    /// <summary>Clamp (k, b, I) into the integrator's stability region: omega <= maxStableOmega, zeta <= maxStableZeta.</summary>
    public void Project(ref float k, ref float b, ref float I)
    {
        if (I <= 1e-9f) { I = 1e-6f; ProjectionEvents++; }
        float w = Mathf.Sqrt(Mathf.Max(k, 0f) / I); float z = w > 1e-6f ? b / (2f * I * w) : 0f;
        bool changed = false;
        if (w > maxStableOmega) { w = maxStableOmega; changed = true; }
        if (w < 1f) { w = 1f; changed = true; }
        if (z > maxStableZeta) { z = maxStableZeta; changed = true; }
        if (z < 0f) { z = 0f; changed = true; }
        if (changed) { k = I * w * w; b = 2f * z * I * w; ProjectionEvents++; }
    }

    /// <summary>Stability check of the semi-implicit Euler update for (k, b, I) at timestep h.</summary>
    public static bool IsStable(float k, float b, float I, float h)
    {
        float a = h * h * k / I, c = h * b / I;
        return c > 0f && c < 2f && a > 0f && a < 4f - 2f * c;
    }

    void ApplyLengthScales(float[] s)
    {
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
                : (seg.capsule != null ? (seg.capsule.center.y + 0.5f * seg.capsule.height) * axisScale : 0.05f);
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
            var seg = m_Segments[g]; if (seg.joint == null || seg.capsule == null) { mass[g] = 0.05f; centre[g] = seg.joint != null ? seg.joint.position : Vector3.zero; continue; }
            float r = seg.capRadius, h = seg.capsule.height * seg.joint.lossyScale.y;
            mass[g] = density * Mathf.PI * r * r * h + density * 4f / 3f * Mathf.PI * r * r * r;
            centre[g] = seg.joint.TransformPoint(seg.capsule.center);
        }
        for (int g = 0; g < FingerGroupCount; g++)
        {
            var seg = m_Segments[g]; if (seg.joint == null) { NominalInertia[g] = 1e-4f; continue; }
            float I = 0f; int f = k_GroupFinger[g];
            for (int j = 0; j < FingerGroupCount; j++)
            {
                if (k_GroupFinger[j] != f || j < g) continue;   // own segment and distal segments of the same finger
                float d = Vector3.Distance(centre[j], seg.joint.position);
                float L = m_Segments[j].capsule != null ? m_Segments[j].capsule.height * m_Segments[j].joint.lossyScale.y : 0.05f;
                I += mass[j] * (d * d + L * L / 12f);
            }
            NominalInertia[g] = Mathf.Max(I, 1e-6f);
        }
        // wrist: palm box + all fingers about the palm pivot
        float palmMass = m_PalmBox != null ? density * m_PalmBox.size.x * m_PalmBox.size.y * m_PalmBox.size.z * m_Palm.lossyScale.x * m_Palm.lossyScale.y * m_Palm.lossyScale.z : 1f;
        float Iw = 0f;
        if (m_Palm != null)
        {
            Vector3 pc = m_PalmBox != null ? m_Palm.TransformPoint(m_PalmBox.center) : m_Palm.position;
            float dp = Vector3.Distance(pc, m_Palm.position); Iw += palmMass * (dp * dp + m_PalmLength * m_PalmLength / 12f);
            for (int g = 0; g < FingerGroupCount; g++) { float d = Vector3.Distance(centre[g], m_Palm.position); Iw += mass[g] * d * d; }
        }
        NominalInertia[FingerGroupCount] = Mathf.Max(Iw, 1e-4f);        // wrist flexion
        NominalInertia[FingerGroupCount + 1] = Mathf.Max(Iw * 0.5f, 1e-4f);   // pronation: roughly half (mass closer to the axis)
    }

    public int FingerOfGroup(int g) => g < FingerGroupCount ? k_GroupFinger[g] : 5;
    public float LengthScaleOfGroup(int g) => g < FingerGroupCount ? lengthScale[k_GroupFinger[g]] : 1f;
    public float NaturalFrequency(int g) => inertia[g] > 0f ? Mathf.Sqrt(stiffness[g] / inertia[g]) : 0f;
    public float DampingRatio(int g) { float w = NaturalFrequency(g); return w > 1e-6f ? damping[g] / (2f * inertia[g] * w) : 0f; }
}
