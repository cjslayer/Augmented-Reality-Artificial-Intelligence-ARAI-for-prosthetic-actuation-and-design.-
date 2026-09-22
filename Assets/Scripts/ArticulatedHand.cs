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
/// Shoulder / elbow are Velocity drives (the biological arm); wrist and fingers are Acceleration drives (stiffness =
/// omega^2, damping = 2 zeta omega, mass-independent), masked groups Target drives at the neutral pose.
/// </summary>
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
    public float density = 1000f;
    [Tooltip("Bicep link: no bone collider exists; a capsule of this radius (m, human scale) and mass (kg, human upper arm) gives it inertia.")]
    public float bicepRadius = 0.016f, bicepMass = 2f;
    [Tooltip("Palm link mass (kg): human metacarpus + soft tissue (de Leva hand segment ~0.45 kg minus the fingers). The thin palm box under-represents the palm volume, so this is set directly rather than from density.")]
    public float palmMass = 0.33f;
    public float fingerMaxAngularVelocity = 50f, armMaxAngularVelocity = 20f;
    [Tooltip("Force limits (N m): base, middle, end, thumb base, thumb end, wrist, shoulder, elbow. Shoulder / elbow are human maxima (velocity drives).")]
    public float[] forceLimits = { 4f, 2.5f, 1.2f, 6f, 3f, 10f, 100f, 60f };
    [Tooltip("Stiffness of the Target drive holding masked groups at the neutral pose (acceleration units, 1/s^2).")]
    public float maskedHoldStiffness = 4000f, maskedHoldDamping = 200f;
    public bool ignoreParentChildCollision = true, ignorePalmBaseCollision = true, ignoreForearmPalmCollision = true;
    [Tooltip("Ignore every collision pair within the hand and arm (the imported capsules are 21-35 mm in radius at 5-7 cm knuckle spacing, so neighbouring fingers overlap by construction and jam).")]
    public bool ignoreAllSelfCollision = true;

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
        m_Follow.Clear(); Contacts.Clear();
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
        Palm.jointType = ArticulationJointType.SphericalJoint; Palm.anchorPosition = Vector3.zero; Palm.anchorRotation = Quaternion.identity; Palm.matchAnchors = true;
        Palm.twistLock = ArticulationDofLock.LimitedMotion; Palm.swingYLock = ArticulationDofLock.LimitedMotion; Palm.swingZLock = ArticulationDofLock.LockedMotion;
        { var box = Palm.gameObject.AddComponent<BoxCollider>(); box.size = m_Palm.boxSizeW; box.center = m_Palm.boxCenterW; Palm.mass = palmMass; }
        Palm.maxAngularVelocity = armMaxAngularVelocity;

        // fingers: revolute about the bone's local Z (anchor X rotated onto Z); link origin at the (scaled) joint pivot
        for (int g = 0; g < GroupCount; g++)
        {
            var bi = m_Group[g]; if (bi == null) continue;
            int p = k_GroupParent[g]; Transform parentLink = p < 0 ? Palm.transform : Groups[p].transform;
            Vector3 parentBoneOrigin = p < 0 ? m_Palm.worldPos : m_Group[p].worldPos; Quaternion parentBoneRot = p < 0 ? m_Palm.worldRot : m_Group[p].worldRot;
            float sc = m_LengthScale[k_GroupFinger[g]];
            // the pivot offset from the parent bone is scaled along the finger only for child segments (base pivots stay on the palm)
            Vector3 offsetW = bi.worldPos - parentBoneOrigin; if (p >= 0) offsetW *= sc;
            Vector3 pivotW = parentLink.TransformPoint(Quaternion.Inverse(parentBoneRot) * offsetW);
            Quaternion rotW = parentLink.rotation * (Quaternion.Inverse(parentBoneRot) * bi.worldRot);
            var link = MakeLink("L_" + GroupTags[g], parentLink, pivotW, rotW, bi.bone);
            link.jointType = ArticulationJointType.RevoluteJoint; link.anchorPosition = Vector3.zero; link.anchorRotation = k_ZAxisAnchor; link.matchAnchors = true;
            link.twistLock = ArticulationDofLock.LimitedMotion;
            AddCapsule(link.gameObject, bi, sc); link.mass = CapsuleMass(bi, sc) * m_InertiaScale[g];
            link.maxAngularVelocity = fingerMaxAngularVelocity;
            link.gameObject.tag = GroupTags[g];
            Groups[g] = link;
        }
        Forearm.mass *= 1f; Palm.mass *= 1f;
        // self-collision policy
        var all = new List<ArticulationBody> { Bicep, Forearm, Palm }; all.AddRange(Groups);
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
        foreach (var a in all) { if (a == null) continue; a.solverIterations = solverIterations; a.solverVelocityIterations = solverVelocityIterations; a.useGravity = true; a.jointFriction = 0f; a.linearDamping = 0f; a.angularDamping = 0.05f; a.sleepThreshold = 0f; a.ResetInertiaTensor(); a.ResetCenterOfMass(); }
        Physics.SyncTransforms();
        RebuildCount++;
        if (RebuildCount == 1) Debug.Log("[ArticulatedHand] " + Anthropometrics());
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
        float handMass = Palm.mass; for (int g = 0; g < GroupCount; g++) if (Groups[g] != null) handMass += Groups[g].mass;
        var s = new System.Text.StringBuilder();
        s.Append("anthropometrics: modelScale=").Append(modelScale).Append(" handLength=").Append((handLength * 1000f).ToString("F1")).Append("mm (palm->middleBase ").Append((palmToMiddle * 1000f).ToString("F1")).Append(") palmWidth=").Append((palmWidth * 1000f).ToString("F1")).Append("mm forearm=").Append((forearm * 1000f).ToString("F1")).Append("mm handMass=").Append(handMass.ToString("F3")).Append("kg (palm ").Append(Palm.mass.ToString("F3")).Append(") forearmMass=").Append(Forearm.mass.ToString("F3")).Append(" bicepMass=").Append(Bicep.mass.ToString("F2")).Append(" links(mm,r mm,g):");
        for (int g = 0; g < GroupCount; g++) { var cap = Groups[g] != null ? Groups[g].GetComponent<CapsuleCollider>() : null; s.Append(' ').Append(GroupTags[g]).Append('=').Append((LinkLen(g) * 1000f).ToString("F1")).Append('/').Append(cap != null ? (cap.radius * 1000f).ToString("F1") : "-").Append('/').Append(Groups[g] != null ? (Groups[g].mass * 1000f).ToString("F1") : "-"); }
        return s.ToString();
    }

    bool IsBase(ArticulationBody b) { for (int g = 0; g < GroupCount; g++) if (Groups[g] == b && k_GroupParent[g] < 0) return true; return false; }

    ArticulationBody MakeLink(string name, Transform parent, Vector3 worldPos, Quaternion worldRot, Transform bone)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.SetPositionAndRotation(worldPos, worldRot);
        var ab = go.AddComponent<ArticulationBody>();
        var lc = go.AddComponent<LinkContact>(); lc.hand = this;
        m_Follow.Add((ab, bone));
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
    public int GroupOf(ArticulationBody b) { for (int g = 0; g < GroupCount; g++) if (Groups[g] == b) return g; return -1; }
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
        d.driveType = ArticulationDriveType.Velocity; d.stiffness = 0f; d.damping = 1e6f; d.target = 0f; d.targetVelocity = 0f;
        SetDrive(b, ax, d);
    }
    public void SetGroupTarget(int g, float deg) { var b = Groups[g]; if (b == null) return; var d = b.xDrive; d.target = deg; b.xDrive = d; }
    public void SetWristTarget(int axis, float deg) { int ax = axis == 0 ? 0 : 1; var d = Drive(Palm, ax); d.target = deg; SetDrive(Palm, ax, d); }
    public void SetArmVelocity(int axis, float degPerSec) { ArticulationBody b = axis == 2 ? Forearm : Bicep; int ax = axis == 1 ? 2 : 0; var d = Drive(b, ax); d.targetVelocity = degPerSec; SetDrive(b, ax, d); }

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
        if (hand == null || !c.gameObject.CompareTag("Cylinder")) return;
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
