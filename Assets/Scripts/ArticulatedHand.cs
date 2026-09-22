using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PhysX articulation for the arm and hand (run 011, branch articulated-hand). The imported armature (100x scale, a
/// non-uniform thumb, about 2.5x human size) is unusable as an articulation, so a unit-scale link skeleton is generated
/// from the tagged bones after the armature has been scaled to human size by the single constant modelScale:
/// root (immovable, shoulder pivot) -> bicep (spherical: twist = shoulder flexion about bone X, swing Z = abduction)
/// -> forearm (revolute X, elbow) -> palm (spherical: twist = wrist flexion, swing Y = pronation) -> 14 finger links
/// (revolute about the bone's local Z through a rotated anchor). Colliders are copied at world size; the group tags move
/// from the bones to the links so tag-based lookups keep working; the bones follow the links every LateUpdate for the
/// skinned mesh. The skeleton is rebuilt at each episode with the morphology's link-length scales (anchor offsets and
/// capsule lengths), masses from capsule volume x density x inertia scale, and drive gains from (omega, zeta).
/// Shoulder / elbow are Velocity drives (the biological arm); wrist and fingers are Force drives with the morphology's
/// physical (k, b) in N m/rad and N m s/rad, masked groups Acceleration drives holding the neutral pose.
/// </summary>

/// <summary>
/// Anthropometric hand specification (spike 5, 2026-09-22): the physics skeleton of the hand is generated from this table
/// instead of the imported armature's bones. All lengths in metres, angles in degrees, positions in the PALM FRAME
/// (origin = wrist pivot; along = toward the fingers, out = palm normal / palmar side, across = radial / thumb side).
/// Sources: Santoso et al. 2026 (phalanx lengths, doi:10.25259/JMSR_36_2026), ANSUR II / NASA RP-1024 (hand length 190,
/// breadth 85, thickness 28-33 mm), Buchholz & Armstrong 1992 (MCP spacing as % breadth, derived 21 mm), Ashraf 2009 /
/// Haughton 2012 (Lister cascade, half-value tilts), AAOS ROM tables, Cheema 2006 / Hollister 1992 (thumb opposition),
/// see results/011_artic2/hand_axes_survey.md and hand_anthropometry_survey.md. Every field is a future theta candidate.
/// </summary>
[System.Serializable]
public class HandSpec
{
    [System.Serializable]
    public class Finger
    {
        public string name = "index";
        [Tooltip("Proximal / middle / distal phalanx bone lengths (m). Santoso 2026, n = 384.")]
        public float proximal = 0.0392f, middle = 0.0213f, distal = 0.0153f;
        [Tooltip("MCP joint centre in the palm frame (m): along from the wrist pivot (metacarpal head arch: middle most distal, little ~13 mm proximal; derived from metacarpal lengths), across (21 mm spacing, derived from breadth 85 mm), out (0 = metacarpal plane).")]
        public Vector3 mcp = new Vector3(0.099f, 0f, 0.0315f);
        [Tooltip("Rest abduction in the palm plane (deg, + = toward the thumb); no measured rest-splay source, inside the max-abduction ranges.")]
        public float restAbductionDeg = 8f;
        [Tooltip("Flexion-plane tilt from the palm's long axis (deg, + = flexed finger drifts toward the thumb): half the derived Lister cascade.")]
        public float flexionPlaneTiltDeg = 2f;
        [Tooltip("Joint limits (deg about the flexion axis; negative = flexion): MCP -90/+10, PIP -100/+5, DIP -80/+5 (AAOS 90 / 100 / 90 plus hyperextension).")]
        public Vector2 mcpLimits = new Vector2(-90f, 10f), pipLimits = new Vector2(-100f, 5f), dipLimits = new Vector2(-80f, 5f);
        [Tooltip("Capsule radii of the three phalanges (m): finger depth ~17-19 mm proximal tapering to ~13-15 mm distal.")]
        public float radiusProximal = 0.009f, radiusMiddle = 0.008f, radiusDistal = 0.0075f;
    }
    [System.Serializable]
    public class Thumb
    {
        [Tooltip("CMC joint centre (trapezium) in the palm frame (m): ~25 mm distal of the wrist pivot, ~32 mm radial, ~5 mm palmar (trapezium sits volar-radial in the distal carpal row; derived).")]
        public Vector3 cmc = new Vector3(0.025f, 0.005f, 0.032f);
        [Tooltip("First metacarpal / proximal phalanx / distal phalanx lengths (m): 46 / 30 / 21 (Santoso 2026 thumb PP 29.8, DP 20.9; MC1 ~46).")]
        public float metacarpal = 0.046f, proximal = 0.030f, distal = 0.021f;
        [Tooltip("Rest (open) posture of MC1: palmar abduction (deg, out of the palm plane) and radial abduction (deg, from the finger direction toward the radial side). Relaxed thumb ~40 / 35; 60 palmar = the open, pre-grasp thumb, keeps the whole thumb above a 5 cm object resting on the palm (single opposition DoF, no separate abduction).")]
        public float restPalmarAbductionDeg = 60f, restRadialAbductionDeg = 35f;
        [Tooltip("Thumb MCP is not driven in this version: fixed flexion (deg) in the opposition plane; relaxed thumb MCP ~15-25.")]
        public float mcpFixedFlexionDeg = 20f;
        [Tooltip("Opposition target in the palm frame (m): the point on the outer-radial side of a 5 cm object resting on the palm that the thumb pad sweeps toward; the CMC axis is the normal of the plane through the MC1 rest direction and this point (single opposition DoF, Kapandji-style arc).")]
        public Vector3 oppositionTarget = new Vector3(0.080f, 0.062f, 0.040f);   // outer surface (out 56 + pad) of a 5 cm cylinder resting on the palm centre (along 80)
        [Tooltip("CMC (opposition arc) and IP limits (deg): CMC 0..60 toward the target, -15 back; IP -10..80 (AAOS 80).")]
        public Vector2 cmcLimits = new Vector2(-15f, 60f), ipLimits = new Vector2(-10f, 80f);
        [Tooltip("Capsule radii (m) of MC1 (thenar), proximal and distal phalanx.")]
        public float radiusMetacarpal = 0.011f, radiusProximal = 0.0095f, radiusDistal = 0.0085f;
    }
    [System.Serializable]
    public class Palm
    {
        [Tooltip("Palm width across the metacarpal heads (m): hand breadth 85 mm (ANSUR II).")]
        public float width = 0.085f;
        [Tooltip("Wrist pivot to middle-finger MCP (m): hand length 190 - middle finger 86 = 104 mm.")]
        public float length = 0.104f;
        [Tooltip("Palm thickness at MC III (m): 28 mm (NASA RP-1024 female 28 / male 33).")]
        public float thickness = 0.028f;
        [Tooltip("Palmar skin offset from the metacarpal plane (m): ~10 mm soft tissue over the metacarpal heads (derived).")]
        public float faceOffset = 0.010f;
        [Tooltip("Palm link mass (kg): set so the hand totals 0.40-0.45 kg (de Leva hand segment) with the finger links from volume.")]
        public float mass = 0.30f;
    }
    public Finger[] fingers = {
        new Finger { name = "index",  proximal = 0.0392f, middle = 0.0213f, distal = 0.0153f, mcp = new Vector3(0.099f, 0f,  0.0315f), restAbductionDeg = 8f,   flexionPlaneTiltDeg = 2f,  radiusProximal = 0.009f,  radiusMiddle = 0.008f,  radiusDistal = 0.0075f },
        new Finger { name = "middle", proximal = 0.0436f, middle = 0.0258f, distal = 0.0162f, mcp = new Vector3(0.104f, 0f,  0.0105f), restAbductionDeg = 0f,   flexionPlaneTiltDeg = 3f,  radiusProximal = 0.0095f, radiusMiddle = 0.0085f, radiusDistal = 0.0075f },
        new Finger { name = "ring",   proximal = 0.0410f, middle = 0.0245f, distal = 0.0166f, mcp = new Vector3(0.100f, 0f, -0.0105f), restAbductionDeg = -6f,  flexionPlaneTiltDeg = 8f,  radiusProximal = 0.009f,  radiusMiddle = 0.008f,  radiusDistal = 0.0075f },
        new Finger { name = "pinky",  proximal = 0.0319f, middle = 0.0171f, distal = 0.0149f, mcp = new Vector3(0.091f, 0f, -0.0315f), restAbductionDeg = -14f, flexionPlaneTiltDeg = 13f, radiusProximal = 0.008f,  radiusMiddle = 0.007f,  radiusDistal = 0.0065f },
    };
    public Thumb thumb = new Thumb();
    public Palm palm = new Palm();
    [Tooltip("Palmar soft-tissue depth over each phalanx (m): bone axis to palmar skin ~10 mm; the capsule centre is offset toward the palm by (this - radius).")]
    public float palmarFleshOffset = 0.010f;
    [Tooltip("Fingertip pad beyond the distal phalanx bone (m).")]
    public float tipPad = 0.004f;
    [Tooltip("Link density (kg/m^3) for the finger and thumb capsules.")]
    public float density = 1000f;

    public string Dump()
    {
        var s = new System.Text.StringBuilder("HandSpec (m, deg; palm frame along/out/across from the wrist pivot)\n");
        foreach (var f in fingers) s.Append(f.name).Append(": phalanges ").Append(f.proximal * 1000f).Append('/').Append(f.middle * 1000f).Append('/').Append(f.distal * 1000f).Append(" mm, MCP ").Append((f.mcp * 1000f).ToString("F1")).Append(" mm, rest abduction ").Append(f.restAbductionDeg).Append(", flexion-plane tilt ").Append(f.flexionPlaneTiltDeg).Append(", limits MCP ").Append(f.mcpLimits).Append(" PIP ").Append(f.pipLimits).Append(" DIP ").Append(f.dipLimits).Append(", radii ").Append(f.radiusProximal * 1000f).Append('/').Append(f.radiusMiddle * 1000f).Append('/').Append(f.radiusDistal * 1000f).Append(" mm\n");
        s.Append("thumb: CMC ").Append((thumb.cmc * 1000f).ToString("F1")).Append(" mm, MC1/PP/DP ").Append(thumb.metacarpal * 1000f).Append('/').Append(thumb.proximal * 1000f).Append('/').Append(thumb.distal * 1000f).Append(" mm, rest palmar/radial abduction ").Append(thumb.restPalmarAbductionDeg).Append('/').Append(thumb.restRadialAbductionDeg).Append(", MCP fixed ").Append(thumb.mcpFixedFlexionDeg).Append(", opposition target ").Append((thumb.oppositionTarget * 1000f).ToString("F1")).Append(" mm, limits CMC ").Append(thumb.cmcLimits).Append(" IP ").Append(thumb.ipLimits).Append(", radii ").Append(thumb.radiusMetacarpal * 1000f).Append('/').Append(thumb.radiusProximal * 1000f).Append('/').Append(thumb.radiusDistal * 1000f).Append(" mm\n");
        s.Append("palm: width ").Append(palm.width * 1000f).Append(" length ").Append(palm.length * 1000f).Append(" thickness ").Append(palm.thickness * 1000f).Append(" faceOffset ").Append(palm.faceOffset * 1000f).Append(" mm, mass ").Append(palm.mass).Append(" kg\n");
        s.Append("palmarFleshOffset ").Append(palmarFleshOffset * 1000f).Append(" mm, tipPad ").Append(tipPad * 1000f).Append(" mm, density ").Append(density).Append('\n');
        return s.ToString();
    }
}

public class ArticulatedHand : MonoBehaviour
{
    public const int GroupCount = 14;
    public static readonly string[] GroupTags = {
        "indexBase","indexMiddle","indexEnd","middleBase","middleMiddle","middleEnd","ringBase","ringMiddle","ringEnd",
        "pinkyBase","pinkyMiddle","pinkyEnd","thumbBase","thumbEnd" };
    static readonly int[] k_GroupParent = { -1, 0, 1, -1, 3, 4, -1, 6, 7, -1, 9, 10, -1, 12 };   // -1 = palm
    static readonly int[] k_GroupFinger = { 0,0,0, 1,1,1, 2,2,2, 3,3,3, 4,4 };

    [Header("Bones (visual armature)")]
    public string shoulderPath = "Armature/Bone/Bicep.r";
    public string forearmPath = "Armature/Bone/Bicep.r/forearm.r";
    public string palmPath = "Armature/Bone/Bicep.r/forearm.r/palm.r";

    [Header("Scale")]
    [Tooltip("THE model scale. The armature (100x import scale, about 2.5x human) is set to armatureBaseScale x modelScale before the bones are captured, so every link length, anchor offset, collider radius / length and capsule-volume mass follows this one constant. 0.36 = 19 cm open hand (wrist pivot to middle fingertip), 8.4 cm palm, 29 cm forearm.")]
    public float modelScale = 0.36f;
    [Tooltip("Import scale of the armature transform (the scene value before modelScale is applied).")]
    public float armatureBaseScale = 100f;
    public string armaturePath = "Armature";

    [Header("Physics")]
    [Tooltip("Fixed timestep (s) set at runtime by this rig; the project setting is left alone. DecisionRequester.DecisionPeriod must be 0.1 s / this.")]
    public float fixedTimestep = 0.01f;
    public int solverIterations = 16, solverVelocityIterations = 4;
    [Tooltip("Contact offset (m) of every link collider and of the object: the project default 0.01 m was set for the 2.5x hand; 0.0036 = 0.01 x modelScale.")]
    public float contactOffset = 0.0036f;
    [Tooltip("Max depenetration velocity (m/s) of every link; 0 = PhysX / project default (10).")]
    public float maxDepenetrationVelocity = 0f;
    public float density = 1000f;
    [Tooltip("Bicep link: no bone collider exists; a capsule of this radius (m, human scale) and mass (kg, human upper arm) gives it inertia.")]
    public float bicepRadius = 0.016f, bicepMass = 2f;
    [Tooltip("Palm link mass (kg): human metacarpus + soft tissue (de Leva hand segment ~0.45 kg minus the fingers). The thin palm box under-represents the palm volume, so this is set directly rather than from density.")]
    public float palmMass = 0.33f;
    public float fingerMaxAngularVelocity = 50f, armMaxAngularVelocity = 20f;
    [Tooltip("Arm servo (spike 5): shoulder / elbow are Force drives whose position target integrates the commanded velocity (the agent still commands velocities). A pure PhysX Velocity drive is implicit and cannot hold a static load: it only cancels each step's velocity gain, so a human-scale arm sags at g dt / I (~8 deg/s measured at the elbow). Stiffness N m/rad, damping N m s/rad, windup = max lead of the target over the joint (deg).")]
    public float armHoldStiffness = 2000f, armHoldDamping = 100f, armTargetWindupDeg = 5f;
    [Tooltip("Force limits (N m): base, middle, end, thumb base, thumb end, wrist, shoulder, elbow. Fingers = survey maxima (MCP 2.5, PIP 1.5, DIP 0.7); thumb and wrist are spike-2 tuning values (future theta candidates); shoulder / elbow human maxima (velocity drives).")]
    public float[] forceLimits = { 2.5f, 1.5f, 0.7f, 4f, 1.5f, 6f, 300f, 200f };   // shoulder / elbow back to the spike-1 values: the velocity drives deliver only ~10-15 % of forceLimit in practice (spike 5 trace: the elbow drifted 10 deg/s under 4 N m with a 60 N m limit)
    [Tooltip("Stiffness of the Target drive holding masked groups at the neutral pose (acceleration units, 1/s^2).")]
    public float maskedHoldStiffness = 4000f, maskedHoldDamping = 200f;
    public bool ignoreParentChildCollision = true, ignorePalmBaseCollision = true, ignoreForearmPalmCollision = true;

    [Header("Hand specification (spike 5): the finger, thumb and palm skeleton is generated from this table, not from the armature")]
    public HandSpec spec = new HandSpec();
    /// <summary>Links that are not impedance groups (the thumb proximal phalanx on its fixed MCP), with the group their contacts and mass count toward.</summary>
    public readonly List<(ArticulationBody body, int group)> ExtraLinks = new List<(ArticulationBody, int)>();
    /// <summary>Max distance (m) per finger (index..pinky, thumb) between the imported bone pivots and the generated link pivots (cosmetic: the mesh follows the links).</summary>
    public float[] MeshFollowDeviation { get; private set; } = new float[5];

    [Header("Anatomy (spike 3: anchor frames rebuilt from the palm frame instead of the armature's bone rolls)")]
    [Tooltip("Rebuild the finger chains straight in the palm plane with an anatomical rest splay, flexion axes perpendicular to each finger with a small radial convergence, the thumb axes set for a fixed opposition, and the wrist twist axis = flexion / extension (across the palm). Off = the armature's bone frames as imported.")]
    public bool anatomicalAxes = true;
    // (spike-3 rest splay / convergence / thumb rest fields replaced by HandSpec in spike 5)
    /// <summary>Opposition angle (deg) between the thumb flexion axis and the middle finger's flexion axis, after the build (report only).</summary>
    public float OppositionAngleDeg { get; private set; }
    [Tooltip("Ignore every collision pair within the hand and arm (spike-1 workaround; off since spike 2: only parent-child, palm-base and forearm-palm pairs are ignored, finger-finger, thumb-finger and fingertip-palm contacts are real).")]
    public bool ignoreAllSelfCollision = false;

    // ---- built skeleton ----
    public ArticulationBody Root { get; private set; }
    public ArticulationBody Bicep { get; private set; }
    public ArticulationBody Forearm { get; private set; }
    public ArticulationBody Palm { get; private set; }
    public ArticulationBody[] Groups { get; private set; } = new ArticulationBody[GroupCount];
    public Transform PalmLink => Palm != null ? Palm.transform : null;
    public Transform ForearmLink => Forearm != null ? Forearm.transform : null;
    public Transform ShoulderLink => Bicep != null ? Bicep.transform : null;
    public bool Built => Root != null;
    public int RebuildCount { get; private set; }

    // bone capture (unscaled reference, taken once at the open pose)
    class BoneInfo { public Transform bone; public Vector3 worldPos; public Quaternion worldRot; public CapsuleCollider capsule; public BoxCollider box; public float capRadiusW, capHeightW; public Vector3 capCenterW, boxSizeW, boxCenterW; }
    BoneInfo m_Shoulder, m_Forearm, m_Palm; BoneInfo[] m_Group = new BoneInfo[GroupCount];
    Transform[] m_BoneOfLink; ArticulationBody[] m_LinkOfBone;
    GameObject m_SkeletonRoot;
    bool m_Captured;
    float[] m_LengthScale = { 1f, 1f, 1f, 1f, 1f }, m_InertiaScale = new float[GroupCount + 2];
    readonly List<(ArticulationBody body, Transform bone)> m_Follow = new List<(ArticulationBody, Transform)>();
    static readonly Quaternion k_ZAxisAnchor = Quaternion.Euler(0f, -90f, 0f);   // anchor X -> body local Z

    /// <summary>Per-link contact buffer (filled by LinkContact callbacks during the physics step, read by the agent).</summary>
    public struct Contact { public int group; public bool isPalm, isForearm; public Vector3 point, normal; public float separation, impulse; }
    public readonly List<Contact> Contacts = new List<Contact>();
    public void ClearContacts() => Contacts.Clear();
    /// <summary>Diagnostics: collision callbacks between two links of this hand since the last reset (self-collision is real when this counts).</summary>
    public int SelfContactEvents { get; set; }

    void Awake()
    {
        Time.fixedDeltaTime = fixedTimestep;
        var req = GetComponent<Unity.MLAgents.DecisionRequester>();
        if (req != null) { int want = Mathf.Max(1, Mathf.RoundToInt(0.1f / fixedTimestep)); if (req.DecisionPeriod != want) Debug.LogWarning("[ArticulatedHand] DecisionPeriod " + req.DecisionPeriod + " at dt " + fixedTimestep + " is " + (req.DecisionPeriod * fixedTimestep).ToString("F3") + " s per decision (expected 0.1 s: " + want + ")"); }
    }

    /// <summary>Capture the bones at the open pose (finger Z rotations zeroed) once; the skeleton is generated from this capture.</summary>
    public void CaptureBones()
    {
        if (m_Captured) return;
        // human scale: the whole armature (mesh + bones) is scaled about its root, which sits on the shoulder pivot
        var armature = transform.Find(armaturePath);
        if (armature != null) { var want = Vector3.one * (armatureBaseScale * modelScale); if ((armature.localScale - want).sqrMagnitude > 1e-8f) armature.localScale = want; }
        m_Shoulder = Capture(transform.Find(shoulderPath)); m_Forearm = Capture(transform.Find(forearmPath)); m_Palm = Capture(transform.Find(palmPath));
        for (int g = 0; g < GroupCount; g++)
        {
            Transform t = null;
            foreach (var go in GameObject.FindGameObjectsWithTag(GroupTags[g])) if (go.transform.IsChildOf(transform) && go.GetComponent<ArticulationBody>() == null) { t = go.transform; break; }
            if (t == null) { Debug.LogError("[ArticulatedHand] no bone tagged " + GroupTags[g]); continue; }
            var e = t.localEulerAngles; t.localRotation = Quaternion.Euler(e.x, e.y, 0f);   // open pose
        }
        // second pass after zeroing: world poses
        for (int g = 0; g < GroupCount; g++)
        {
            Transform t = null;
            foreach (var go in GameObject.FindGameObjectsWithTag(GroupTags[g])) if (go.transform.IsChildOf(transform) && go.GetComponent<ArticulationBody>() == null) { t = go.transform; break; }
            m_Group[g] = Capture(t);
            // bone colliders are physics-irrelevant from now on
            var c = t.GetComponent<Collider>(); if (c != null) c.enabled = false;
            t.tag = "Untagged";
        }
        foreach (var b in new[] { m_Shoulder, m_Forearm, m_Palm }) { if (b == null) continue; var c = b.bone.GetComponent<Collider>(); if (c != null) c.enabled = false; }
        m_Captured = true;
    }

    static BoneInfo Capture(Transform t)
    {
        if (t == null) return null;
        var b = new BoneInfo { bone = t, worldPos = t.position, worldRot = t.rotation };
        b.capsule = t.GetComponent<CapsuleCollider>(); b.box = t.GetComponent<BoxCollider>();
        if (b.capsule != null)
        {
            b.capRadiusW = b.capsule.radius * Mathf.Max(t.lossyScale.x, t.lossyScale.z);
            b.capHeightW = b.capsule.height * t.lossyScale.y;
            b.capCenterW = Vector3.Scale(b.capsule.center, t.lossyScale);
        }
        if (b.box != null) { b.boxSizeW = Vector3.Scale(b.box.size, t.lossyScale); b.boxCenterW = Vector3.Scale(b.box.center, t.lossyScale); }
        return b;
    }

    /// <summary>(Re)build the link skeleton for the given per-finger length scales and per-group inertia scales (16 = 14 fingers + wrist flex + wrist pron).</summary>
    public void Rebuild(float[] lengthScale, float[] inertiaScale)
    {
        CaptureBones();
        if (lengthScale != null) for (int f = 0; f < 5; f++) m_LengthScale[f] = lengthScale[f];
        if (inertiaScale != null) for (int i = 0; i < m_InertiaScale.Length && i < inertiaScale.Length; i++) m_InertiaScale[i] = inertiaScale[i];
        else for (int i = 0; i < m_InertiaScale.Length; i++) m_InertiaScale[i] = 1f;
        if (m_SkeletonRoot != null) { DestroyImmediate(m_SkeletonRoot); }
        m_Follow.Clear(); Contacts.Clear(); for (int i = 0; i < 5; i++) MeshFollowDeviation[i] = 0f;
        m_SkeletonRoot = new GameObject("ArticulatedSkeleton");
        m_SkeletonRoot.transform.SetPositionAndRotation(m_Shoulder.worldPos, m_Shoulder.worldRot);
        Root = m_SkeletonRoot.AddComponent<ArticulationBody>();
        Root.immovable = true; Root.useGravity = false; Root.mass = 1f;
        Root.solverIterations = solverIterations; Root.solverVelocityIterations = solverVelocityIterations;

        // bicep: spherical (twist X = flexion, swing Z = abduction, swing Y locked); a capsule along the bone gives it inertia
        Bicep = MakeLink("L_Bicep", Root.transform, m_Shoulder.worldPos, m_Shoulder.worldRot, m_Shoulder.bone);
        Bicep.jointType = ArticulationJointType.SphericalJoint;
        Bicep.anchorPosition = Vector3.zero; Bicep.anchorRotation = Quaternion.identity; Bicep.matchAnchors = true;
        Bicep.twistLock = ArticulationDofLock.LimitedMotion; Bicep.swingYLock = ArticulationDofLock.LockedMotion; Bicep.swingZLock = ArticulationDofLock.LimitedMotion;
        {
            var cap = Bicep.gameObject.AddComponent<CapsuleCollider>(); Vector3 toElbow = Bicep.transform.InverseTransformPoint(m_Forearm.worldPos);
            float len = toElbow.magnitude; cap.radius = bicepRadius; cap.height = len; cap.center = toElbow * 0.5f;
            int dir = 1; float ax = Mathf.Abs(toElbow.x), ay = Mathf.Abs(toElbow.y), az = Mathf.Abs(toElbow.z); if (ax > ay && ax > az) dir = 0; else if (az > ay) dir = 2; cap.direction = dir;
            Bicep.mass = bicepMass;
        }
        Bicep.maxAngularVelocity = armMaxAngularVelocity;

        // forearm: revolute about bone X (elbow)
        Forearm = MakeLink("L_Forearm", Bicep.transform, m_Forearm.worldPos, m_Forearm.worldRot, m_Forearm.bone);
        Forearm.jointType = ArticulationJointType.RevoluteJoint; Forearm.anchorPosition = Vector3.zero; Forearm.anchorRotation = Quaternion.identity; Forearm.matchAnchors = true;
        Forearm.twistLock = ArticulationDofLock.LimitedMotion;
        AddCapsule(Forearm.gameObject, m_Forearm, 1f); Forearm.mass = CapsuleMass(m_Forearm, 1f) ; Forearm.maxAngularVelocity = armMaxAngularVelocity;

        // palm: spherical (twist X = wrist flexion, swing Y = pronation, swing Z locked)
        Palm = MakeLink("L_Palm", Forearm.transform, m_Palm.worldPos, m_Palm.worldRot, m_Palm.bone);
        Palm.jointType = ArticulationJointType.SphericalJoint; Palm.anchorPosition = Vector3.zero; Palm.anchorRotation = anatomicalAxes ? k_ZAxisAnchor : Quaternion.identity; Palm.matchAnchors = true;
        Palm.twistLock = ArticulationDofLock.LimitedMotion; Palm.swingYLock = ArticulationDofLock.LimitedMotion; Palm.swingZLock = ArticulationDofLock.LockedMotion;
        {   // palm box from the spec: metacarpal plane at out = 0, palmar face at +faceOffset, dorsal face at faceOffset - thickness (link local X = out, Y = along, Z = across)
            var box = Palm.gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(spec.palm.thickness, spec.palm.length, spec.palm.width);
            box.center = new Vector3(spec.palm.faceOffset - 0.5f * spec.palm.thickness, 0.5f * spec.palm.length, 0f);
            Palm.mass = spec.palm.mass;
        }
        Palm.maxAngularVelocity = armMaxAngularVelocity;

        // ---- hand skeleton from the HandSpec (palm frame: along = palm.up toward the fingers, out = palm.right = palmar side, across = palm.forward = radial / thumb side) ----
        Vector3 pAlong = Palm.transform.up, pOut = Palm.transform.right, pAcross = Palm.transform.forward; Vector3 wrist = Palm.transform.position;
        Vector3 W(Vector3 pf) => wrist + pAlong * pf.x + pOut * pf.y + pAcross * pf.z;   // palm-frame (along, out, across) -> world
        ExtraLinks.Clear();
        bool radialIsPlusAcross = Vector3.Dot(Quaternion.AngleAxis(1f, pOut) * pAlong, pAcross) >= 0f;   // sign of a rotation about out that turns along toward across
        float SignedAbout(float deg) => radialIsPlusAcross ? deg : -deg;
        for (int f = 0; f < 4; f++)
        {
            var fs = spec.fingers[f]; float sc = m_LengthScale[f];
            Vector3 dir = Quaternion.AngleAxis(SignedAbout(fs.restAbductionDeg), pOut) * pAlong;                 // rest direction in the palm plane
            Vector3 z = Vector3.Cross(pOut, dir).normalized; Quaternion rot = Quaternion.LookRotation(z, dir);       // local X = out (palmar), Y = finger, Z = nominal flexion axis
            Vector3 sag = Vector3.Cross(pOut, pAlong).normalized; float tilt = fs.flexionPlaneTiltDeg;
            Vector3 axisW = Quaternion.AngleAxis(tilt, pOut) * sag; if (Vector3.Dot(-Vector3.Cross(axisW, pAlong), pAcross) * tilt < 0f) axisW = Quaternion.AngleAxis(-tilt, pOut) * sag;
            float[] L = { fs.proximal * sc, fs.middle * sc, fs.distal * sc }; float[] R = { fs.radiusProximal, fs.radiusMiddle, fs.radiusDistal };
            Vector2[] lim = { fs.mcpLimits, fs.pipLimits, fs.dipLimits };
            Vector3 pivot = W(fs.mcp); Transform parentLink = Palm.transform;
            for (int seg = 0; seg < 3; seg++)
            {
                int g = f * 3 + seg;
                var link = MakeLink("L_" + GroupTags[g], parentLink, pivot, rot, m_Group[g] != null ? m_Group[g].bone : null);
                link.jointType = ArticulationJointType.RevoluteJoint; link.anchorPosition = Vector3.zero; link.matchAnchors = true;
                link.anchorRotation = Quaternion.AngleAxis(Vector3.SignedAngle(Vector3.forward, link.transform.InverseTransformDirection(axisW), Vector3.right), Vector3.right) * k_ZAxisAnchor;
                link.twistLock = ArticulationDofLock.LimitedMotion;
                float len = L[seg] + (seg == 2 ? spec.tipPad : 0f);
                SpecCapsule(link.gameObject, R[seg], len, spec.palmarFleshOffset); link.mass = SpecMass(R[seg], len) * m_InertiaScale[g];
                link.maxAngularVelocity = fingerMaxAngularVelocity; link.gameObject.tag = GroupTags[g]; Groups[g] = link;
                if (m_Group[g] != null) MeshFollowDeviation[f] = Mathf.Max(MeshFollowDeviation[f], Vector3.Distance(m_Group[g].worldPos, pivot));
                parentLink = link.transform; pivot += dir * L[seg];
            }
        }
        {   // thumb: CMC (driven, opposition arc) -> MC1 -> MCP (fixed flexion) -> PP -> IP (driven) -> DP; all in the plane through the MC1 rest direction and the opposition target
            var ts = spec.thumb; float sc = m_LengthScale[4];
            float pa = ts.restPalmarAbductionDeg * Mathf.Deg2Rad, ra = ts.restRadialAbductionDeg * Mathf.Deg2Rad;
            Vector3 radial = Quaternion.AngleAxis(SignedAbout(ts.restRadialAbductionDeg), pOut) * pAlong;
            Vector3 dir = (radial * Mathf.Cos(pa) + pOut * Mathf.Sin(pa)).normalized;
            Vector3 cmc = W(ts.cmc), target = W(ts.oppositionTarget);
            Vector3 axisW = Vector3.Cross(dir, (target - cmc).normalized).normalized;             // positive angles sweep the thumb toward the target
            Vector3 padSide = Vector3.Cross(axisW, dir).normalized;                                 // in-plane direction toward the target: the pad faces it
            Quaternion rot = Quaternion.LookRotation(Vector3.Cross(padSide, dir).normalized, dir); // local X = pad side, Y = thumb, Z = axis
            Quaternion mcpFlex = Quaternion.AngleAxis(ts.mcpFixedFlexionDeg, axisW);
            // MC1 (group 12, CMC joint)
            var mc1 = MakeLink("L_thumbBase", Palm.transform, cmc, rot, null);
            mc1.jointType = ArticulationJointType.RevoluteJoint; mc1.anchorPosition = Vector3.zero; mc1.matchAnchors = true;
            mc1.anchorRotation = Quaternion.FromToRotation(Vector3.right, mc1.transform.InverseTransformDirection(axisW)); mc1.twistLock = ArticulationDofLock.LimitedMotion;
            SpecCapsule(mc1.gameObject, ts.radiusMetacarpal, ts.metacarpal, spec.palmarFleshOffset); mc1.mass = SpecMass(ts.radiusMetacarpal, ts.metacarpal) * m_InertiaScale[12];
            mc1.maxAngularVelocity = fingerMaxAngularVelocity; mc1.gameObject.tag = GroupTags[12]; Groups[12] = mc1;
            // PP on a fixed MCP (extra link counted with group 12)
            Vector3 mcp = cmc + dir * ts.metacarpal; Vector3 dirPP = mcpFlex * dir; Quaternion rotPP = mcpFlex * rot;
            var pp = MakeLink("L_thumbProximal", mc1.transform, mcp, rotPP, m_Group[12] != null ? m_Group[12].bone : null);
            pp.jointType = ArticulationJointType.FixedJoint; pp.anchorPosition = Vector3.zero; pp.matchAnchors = true;
            SpecCapsule(pp.gameObject, ts.radiusProximal, ts.proximal * sc, spec.palmarFleshOffset); pp.mass = SpecMass(ts.radiusProximal, ts.proximal * sc) * m_InertiaScale[12];
            pp.maxAngularVelocity = fingerMaxAngularVelocity; ExtraLinks.Add((pp, 12));
            if (m_Group[12] != null) MeshFollowDeviation[4] = Mathf.Max(MeshFollowDeviation[4], Vector3.Distance(m_Group[12].worldPos, mcp));
            // DP (group 13, IP joint)
            Vector3 ip = mcp + dirPP * ts.proximal * sc;
            var dp = MakeLink("L_thumbEnd", pp.transform, ip, rotPP, m_Group[13] != null ? m_Group[13].bone : null);
            dp.jointType = ArticulationJointType.RevoluteJoint; dp.anchorPosition = Vector3.zero; dp.matchAnchors = true;
            dp.anchorRotation = Quaternion.FromToRotation(Vector3.right, dp.transform.InverseTransformDirection(axisW)); dp.twistLock = ArticulationDofLock.LimitedMotion;
            SpecCapsule(dp.gameObject, ts.radiusDistal, ts.distal * sc + spec.tipPad, spec.palmarFleshOffset); dp.mass = SpecMass(ts.radiusDistal, ts.distal * sc + spec.tipPad) * m_InertiaScale[13];
            dp.maxAngularVelocity = fingerMaxAngularVelocity; dp.gameObject.tag = GroupTags[13]; Groups[13] = dp;
            if (m_Group[13] != null) MeshFollowDeviation[4] = Mathf.Max(MeshFollowDeviation[4], Vector3.Distance(m_Group[13].worldPos, ip));
            OppositionAngleDeg = Vector3.Angle(axisW, Groups[3].transform.TransformDirection(Groups[3].anchorRotation * Vector3.right));
            ThumbAxisPalm = new Vector3(Vector3.Dot(axisW, pAlong), Vector3.Dot(axisW, pOut), Vector3.Dot(axisW, pAcross));
        }
        // self-collision policy
        var all = new List<ArticulationBody> { Bicep, Forearm, Palm }; all.AddRange(Groups); foreach (var (eb, _) in ExtraLinks) all.Add(eb);
        foreach (var a in all) foreach (var b in all)
        {
            if (a == b || a == null || b == null) continue;
            bool ignore = false;
            if (ignoreParentChildCollision && (a.transform.parent == b.transform || b.transform.parent == a.transform)) ignore = true;
            if (ignorePalmBaseCollision && ((a == Palm && IsBase(b)) || (b == Palm && IsBase(a)))) ignore = true;
            if (ignoreForearmPalmCollision && ((a == Forearm && b == Palm) || (a == Palm && b == Forearm))) ignore = true;
            if (a == Bicep || b == Bicep) ignore = true;   // the bicep never reaches the hand; keep it out of the pair set
            if (ignoreAllSelfCollision) ignore = true;
            if (ignore) { var ca = a.GetComponent<Collider>(); var cb = b.GetComponent<Collider>(); if (ca != null && cb != null) Physics.IgnoreCollision(ca, cb, true); }
        }
        foreach (var a in all) { if (a == null) continue; var col = a.GetComponent<Collider>(); if (col != null) col.contactOffset = contactOffset; if (maxDepenetrationVelocity > 0f) a.maxDepenetrationVelocity = maxDepenetrationVelocity; a.solverIterations = solverIterations; a.solverVelocityIterations = solverVelocityIterations; a.useGravity = true; a.jointFriction = 0f; a.linearDamping = 0f; a.angularDamping = 0.05f; a.sleepThreshold = 0f; a.ResetInertiaTensor(); a.ResetCenterOfMass(); }
        Physics.SyncTransforms();
        RebuildCount++;
        if (RebuildCount == 1)
        {
            Debug.Log("[ArticulatedHand] " + spec.Dump() + Anthropometrics());
#if UNITY_EDITOR
            try { System.IO.Directory.CreateDirectory("results/011_artic2"); System.IO.File.WriteAllText("results/011_artic2/handspec_reference.txt", spec.Dump() + Anthropometrics() + "\n"); } catch (System.Exception e) { Debug.LogWarning(e.Message); }
#endif
        }
    }

    /// <summary>Open-pose anthropometrics of the built skeleton (m, kg): hand length wrist pivot to middle fingertip, palm width across the index / pinky base capsules, forearm, per-link lengths, masses.</summary>
    public string Anthropometrics()
    {
        if (!Built) return "not built";
        float LinkLen(int g)
        {
            var b = Groups[g]; if (b == null) return 0f;
            foreach (Transform c in b.transform) if (c.GetComponent<ArticulationBody>() != null) return Vector3.Distance(b.transform.position, c.position);
            var cap = b.GetComponent<CapsuleCollider>(); return cap != null ? cap.center.y + 0.5f * cap.height : 0f;
        }
        float palmToMiddle = Vector3.Distance(Palm.transform.position, Groups[3].transform.position);
        float handLength = palmToMiddle + LinkLen(3) + LinkLen(4) + LinkLen(5);
        var ci = Groups[0].GetComponent<CapsuleCollider>(); var cp = Groups[9].GetComponent<CapsuleCollider>();
        float palmWidth = Vector3.Distance(Groups[0].transform.position, Groups[9].transform.position) + (ci != null ? ci.radius : 0f) + (cp != null ? cp.radius : 0f);
        float forearm = Vector3.Distance(Forearm.transform.position, Palm.transform.position);
        float handMass = Palm.mass; for (int g = 0; g < GroupCount; g++) if (Groups[g] != null) handMass += Groups[g].mass; foreach (var (eb, _) in ExtraLinks) handMass += eb.mass;
        var s = new System.Text.StringBuilder();
        s.Append("anthropometrics: modelScale(armature)=").Append(modelScale).Append(" specPalmWidth=").Append((spec.palm.width * 1000f).ToString("F0")).Append("mm thumbCMCaxis(palm)=").Append(ThumbAxisPalm.ToString("F2")).Append(" oppositionAngle=").Append(OppositionAngleDeg.ToString("F1")).Append(" meshFollowDev(mm idx/mid/ring/pnk/thb)=").Append((MeshFollowDeviation[0] * 1000f).ToString("F0")).Append('/').Append((MeshFollowDeviation[1] * 1000f).ToString("F0")).Append('/').Append((MeshFollowDeviation[2] * 1000f).ToString("F0")).Append('/').Append((MeshFollowDeviation[3] * 1000f).ToString("F0")).Append('/').Append((MeshFollowDeviation[4] * 1000f).ToString("F0")).Append(" handLength=").Append((handLength * 1000f).ToString("F1")).Append("mm (palm->middleBase ").Append((palmToMiddle * 1000f).ToString("F1")).Append(") palmWidth=").Append((palmWidth * 1000f).ToString("F1")).Append("mm forearm=").Append((forearm * 1000f).ToString("F1")).Append("mm handMass=").Append(handMass.ToString("F3")).Append("kg (palm ").Append(Palm.mass.ToString("F3")).Append(") forearmMass=").Append(Forearm.mass.ToString("F3")).Append(" bicepMass=").Append(Bicep.mass.ToString("F2")).Append(" links(mm,r mm,g):");
        for (int g = 0; g < GroupCount; g++) { var cap = Groups[g] != null ? Groups[g].GetComponent<CapsuleCollider>() : null; s.Append(' ').Append(GroupTags[g]).Append('=').Append((LinkLen(g) * 1000f).ToString("F1")).Append('/').Append(cap != null ? (cap.radius * 1000f).ToString("F1") : "-").Append('/').Append(Groups[g] != null ? (Groups[g].mass * 1000f).ToString("F1") : "-"); }
        return s.ToString();
    }

    bool IsBase(ArticulationBody b) { for (int g = 0; g < GroupCount; g++) if (Groups[g] == b && k_GroupParent[g] < 0) return true; return false; }
    /// <summary>Thumb CMC axis in the palm frame (along, out, across), after the build (report only).</summary>
    public Vector3 ThumbAxisPalm { get; private set; }
    /// <summary>Capsule along local Y spanning [-r, length] (the cap overlaps the joint), centred toward the palmar side so the skin lies fleshOffset from the bone axis.</summary>
    static void SpecCapsule(GameObject go, float r, float length, float fleshOffset)
    {
        var cap = go.AddComponent<CapsuleCollider>(); cap.direction = 1; cap.radius = r; cap.height = length + r;
        cap.center = new Vector3(Mathf.Max(0f, fleshOffset - r), 0.5f * (length - r), 0f);
    }
    float SpecMass(float r, float length) { float cyl = Mathf.Max(length + r - 2f * r, 0f); return spec.density * (Mathf.PI * r * r * cyl + 4f / 3f * Mathf.PI * r * r * r); }

    ArticulationBody MakeLink(string name, Transform parent, Vector3 worldPos, Quaternion worldRot, Transform bone)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.SetPositionAndRotation(worldPos, worldRot);
        var ab = go.AddComponent<ArticulationBody>();
        var lc = go.AddComponent<LinkContact>(); lc.hand = this;
        if (bone != null) m_Follow.Add((ab, bone));
        return ab;
    }

    static void AddCapsule(GameObject go, BoneInfo bi, float lengthScale)
    {
        var cap = go.AddComponent<CapsuleCollider>();
        if (bi.capsule != null) { cap.radius = bi.capRadiusW; cap.height = bi.capHeightW * lengthScale; var c = bi.capCenterW; c.y *= lengthScale; cap.center = c; cap.direction = bi.capsule.direction; }
        else { cap.radius = 0.008f; cap.height = 0.024f; cap.center = new Vector3(0f, 0.012f, 0f); cap.direction = 1; }
    }
    float CapsuleMass(BoneInfo bi, float lengthScale)
    {
        float r = bi.capsule != null ? bi.capRadiusW : 0.008f, h = (bi.capsule != null ? bi.capHeightW : 0.024f) * lengthScale;
        float cyl = Mathf.Max(h - 2f * r, 0f);
        return density * (Mathf.PI * r * r * cyl + 4f / 3f * Mathf.PI * r * r * r);
    }

    /// <summary>Group index of a link body (-1 if not a finger link).</summary>
    public int GroupOf(ArticulationBody b) { for (int g = 0; g < GroupCount; g++) if (Groups[g] == b) return g; foreach (var (eb, eg) in ExtraLinks) if (eb == b) return eg; return -1; }
    public int FingerOfGroup(int g) => k_GroupFinger[g];
    public int ParentOfGroup(int g) => k_GroupParent[g];
    public float LengthScaleOfGroup(int g) => m_LengthScale[k_GroupFinger[g]];

    // ---- drives ----
    static ArticulationDrive Drive(ArticulationBody b, int axis) => axis == 0 ? b.xDrive : (axis == 1 ? b.yDrive : b.zDrive);
    static void SetDrive(ArticulationBody b, int axis, ArticulationDrive d) { if (axis == 0) b.xDrive = d; else if (axis == 1) b.yDrive = d; else b.zDrive = d; }

    /// <summary>
    /// Configure a finger group's drive as a force-mode spring-damper with the morphology's k (N m/rad) and b (N m s/rad),
    /// i.e. k = I omega^2 and b = 2 zeta I omega with I the geometric subtree inertia (the run-010 impedance semantics; the
    /// drive then works against real gravity and contact loads). Limits in degrees, force limit in N m; masked = stiff hold at neutral.
    /// </summary>
    public void ConfigureGroup(int g, float k, float b, Vector2 limitsDeg, bool masked, float neutralDeg)
    {
        var body = Groups[g]; if (body == null) return;
        var d = body.xDrive;
        d.lowerLimit = limitsDeg.x; d.upperLimit = limitsDeg.y;
        d.forceLimit = forceLimits[g < 12 ? g % 3 : (g == 12 ? 3 : 4)];
        if (masked) { d.driveType = ArticulationDriveType.Acceleration; d.stiffness = maskedHoldStiffness; d.damping = maskedHoldDamping; d.target = Mathf.Clamp(neutralDeg, limitsDeg.x, limitsDeg.y); d.forceLimit = Mathf.Max(d.forceLimit, forceLimits[3]); }
        else { d.driveType = ArticulationDriveType.Force; d.stiffness = k; d.damping = b; d.target = 0f; }
        d.targetVelocity = 0f;
        body.xDrive = d;
    }
    /// <summary>Wrist axis (0 flexion = palm twist X, 1 pronation = palm swing Y) as a force-mode spring-damper (k in N m/rad, b in N m s/rad).</summary>
    public void ConfigureWrist(int axis, float k, float b, Vector2 limitsDeg)
    {
        int ax = axis == 0 ? 0 : 1; var d = Drive(Palm, ax);
        d.lowerLimit = limitsDeg.x; d.upperLimit = limitsDeg.y; d.forceLimit = forceLimits[5];
        d.driveType = ArticulationDriveType.Force; d.stiffness = k; d.damping = b; d.target = 0f; d.targetVelocity = 0f;
        SetDrive(Palm, ax, d);
    }
    /// <summary>Arm axis (0 shoulder flexion = bicep twist X, 1 abduction = bicep swing Z, 2 elbow = forearm X) as a velocity drive.</summary>
    public void ConfigureArm(int axis, Vector2 limitsDeg)
    {
        ArticulationBody b = axis == 2 ? Forearm : Bicep; int ax = axis == 1 ? 2 : 0;
        var d = Drive(b, ax);
        d.lowerLimit = limitsDeg.x; d.upperLimit = limitsDeg.y; d.forceLimit = forceLimits[axis == 2 ? 7 : 6];
        d.driveType = ArticulationDriveType.Force; d.stiffness = armHoldStiffness; d.damping = armHoldDamping; d.target = ArmAngle(axis); d.targetVelocity = 0f;
        SetDrive(b, ax, d);
    }
    public void SetGroupTarget(int g, float deg) { var b = Groups[g]; if (b == null) return; var d = b.xDrive; d.target = deg; b.xDrive = d; }
    public void SetWristTarget(int axis, float deg) { int ax = axis == 0 ? 0 : 1; var d = Drive(Palm, ax); d.target = deg; SetDrive(Palm, ax, d); }
    /// <summary>Command an arm axis velocity (deg/s): the servo target integrates it, clamped to the joint limits and to armTargetWindupDeg around the current angle so a blocked arm does not wind up.</summary>
    public void SetArmVelocity(int axis, float degPerSec)
    {
        ArticulationBody b = axis == 2 ? Forearm : Bicep; int ax = axis == 1 ? 2 : 0; var d = Drive(b, ax);
        float ang = ArmAngle(axis);
        d.targetVelocity = degPerSec;
        d.target = Mathf.Clamp(Mathf.Clamp(d.target + degPerSec * Time.fixedDeltaTime, ang - armTargetWindupDeg, ang + armTargetWindupDeg), d.lowerLimit, d.upperLimit);
        SetDrive(b, ax, d);
    }

    // ---- state (degrees, deg/s) ----
    public float GroupAngle(int g) { var b = Groups[g]; return b != null && b.jointPosition.dofCount > 0 ? b.jointPosition[0] * Mathf.Rad2Deg : 0f; }
    public float GroupVelocity(int g) { var b = Groups[g]; return b != null && b.jointVelocity.dofCount > 0 ? b.jointVelocity[0] * Mathf.Rad2Deg : 0f; }
    public float GroupTarget(int g) { var b = Groups[g]; return b != null ? b.xDrive.target : 0f; }
    /// <summary>Drive torque (N m) of a finger group this step.</summary>
    public float GroupDriveForce(int g) { var b = Groups[g]; return b != null && b.driveForce.dofCount > 0 ? b.driveForce[0] : 0f; }
    /// <summary>Arm axis angle (deg): 0 shoulder flexion, 1 abduction, 2 elbow, 3 wrist flexion, 4 pronation.</summary>
    public float ArmAngle(int axis)
    {
        switch (axis)
        {
            case 0: return Dof(Bicep, 0) * Mathf.Rad2Deg;
            case 1: return Dof(Bicep, Bicep != null && Bicep.jointPosition.dofCount > 1 ? 1 : 0) * Mathf.Rad2Deg;
            case 2: return Dof(Forearm, 0) * Mathf.Rad2Deg;
            case 3: return Dof(Palm, 0) * Mathf.Rad2Deg;
            default: return Dof(Palm, Palm != null && Palm.jointPosition.dofCount > 1 ? 1 : 0) * Mathf.Rad2Deg;
        }
    }
    public float ArmVelocity(int axis)
    {
        switch (axis)
        {
            case 0: return DofV(Bicep, 0) * Mathf.Rad2Deg;
            case 1: return DofV(Bicep, Bicep != null && Bicep.jointVelocity.dofCount > 1 ? 1 : 0) * Mathf.Rad2Deg;
            case 2: return DofV(Forearm, 0) * Mathf.Rad2Deg;
            case 3: return DofV(Palm, 0) * Mathf.Rad2Deg;
            default: return DofV(Palm, Palm != null && Palm.jointVelocity.dofCount > 1 ? 1 : 0) * Mathf.Rad2Deg;
        }
    }
    public float WristTarget(int axis) { int ax = axis == 0 ? 0 : 1; return Palm != null ? Drive(Palm, ax).target : 0f; }
    static float Dof(ArticulationBody b, int i) => b != null && b.jointPosition.dofCount > i ? b.jointPosition[i] : 0f;
    static float DofV(ArticulationBody b, int i) => b != null && b.jointVelocity.dofCount > i ? b.jointVelocity[i] : 0f;

    /// <summary>Largest joint speed (deg/s) over all bodies and whether any state is non-finite (diagnostics).</summary>
    public (float maxJointSpeedDeg, bool finite) Health()
    {
        float m = 0f; bool ok = true;
        foreach (var (b, _) in m_Follow)
        {
            if (b == null) continue;
            var v = b.jointVelocity; for (int i = 0; i < v.dofCount; i++) { float s = Mathf.Abs(v[i]) * Mathf.Rad2Deg; if (float.IsNaN(s) || float.IsInfinity(s)) ok = false; else if (s > m) m = s; }
            var p = b.transform.position; if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z)) ok = false;
        }
        return (m, ok);
    }

    void LateUpdate()
    {   // the visual armature follows the physics skeleton
        foreach (var (b, bone) in m_Follow) if (b != null && bone != null) bone.SetPositionAndRotation(b.transform.position, b.transform.rotation);
    }
}

/// <summary>Collision relay on a link: buffers contacts with the tagged object into the rig's contact list.</summary>
public class LinkContact : MonoBehaviour
{
    public ArticulatedHand hand;
    ArticulationBody m_Body;
    void Awake() { m_Body = GetComponent<ArticulationBody>(); }
    void OnCollisionStay(Collision c) { Report(c); }
    void OnCollisionEnter(Collision c) { Report(c); }
    void Report(Collision c)
    {
        if (hand == null) return;
        if (c.articulationBody != null && c.articulationBody.transform.IsChildOf(hand.Root.transform)) { hand.SelfContactEvents++; return; }
        if (!c.gameObject.CompareTag("Cylinder")) return;
        int g = hand.GroupOf(m_Body); bool palm = m_Body == hand.Palm, fore = m_Body == hand.Forearm;
        if (g < 0 && !palm && !fore) return;
        float imp = c.impulse.magnitude; int n = c.contactCount;
        for (int i = 0; i < n; i++)
        {
            var cp = c.GetContact(i);
            hand.Contacts.Add(new ArticulatedHand.Contact { group = g, isPalm = palm, isForearm = fore, point = cp.point, normal = -cp.normal, separation = cp.separation, impulse = n > 0 ? imp / n : imp });
        }
    }
}
