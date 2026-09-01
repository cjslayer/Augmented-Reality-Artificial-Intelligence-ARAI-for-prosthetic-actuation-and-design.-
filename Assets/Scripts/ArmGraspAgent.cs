using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections.Generic;

public class ArmGraspAgent : Agent
{
    [Header("Rotation Settings")]
    [Tooltip("Degrees of finger rotation per action unit per second.")]
    public float rotationSpeed = 90f;
    [Tooltip("Degrees of arm-joint rotation per action unit per second.")]
    public float armRotationSpeed = 90f;
    public GameObject arm;

    // Joint groups are rotated about their local Z axis. 0 deg is the open pose set in OnEpisodeBegin.
    // Fingers flex toward the palm/cylinder with negative Z; the thumb opposes them with positive Z.
    [Header("Joint Limits (deg about local Z; x = min, y = max; 0 = open pose)")]
    public Vector2 indexBaseLimits   = new Vector2(-90f, 10f);
    public Vector2 indexMiddleLimits = new Vector2(-100f, 5f);
    public Vector2 indexEndLimits    = new Vector2(-80f, 5f);
    public Vector2 middleBaseLimits   = new Vector2(-90f, 10f);
    public Vector2 middleMiddleLimits = new Vector2(-100f, 5f);
    public Vector2 middleEndLimits    = new Vector2(-80f, 5f);
    public Vector2 ringBaseLimits   = new Vector2(-90f, 10f);
    public Vector2 ringMiddleLimits = new Vector2(-100f, 5f);
    public Vector2 ringEndLimits    = new Vector2(-80f, 5f);
    public Vector2 pinkyBaseLimits   = new Vector2(-90f, 10f);
    public Vector2 pinkyMiddleLimits = new Vector2(-100f, 5f);
    public Vector2 pinkyEndLimits    = new Vector2(-80f, 5f);
    public Vector2 thumbBaseLimits = new Vector2(-15f, 60f);
    public Vector2 thumbEndLimits  = new Vector2(-10f, 80f);

    // Arm axes (Option A, 5 axes). Angles are relative to the scene pose captured in OnEpisodeBegin (0 deg).
    // Proximal (shoulder, elbow) emulate the user's gross reach; distal (wrist) belongs to the prosthesis.
    [Header("Arm Joints (deg relative to scene pose; x = min, y = max)")]
    [Tooltip("Bicep.r about local X: + raises the hand (shoulder flexion/elevation).")]
    public Vector2 shoulderFlexionLimits = new Vector2(-40f, 100f);
    [Tooltip("Bicep.r about local Z: swings the hand horizontally (shoulder ab/adduction).")]
    public Vector2 shoulderAbductionLimits = new Vector2(-60f, 60f);
    [Tooltip("forearm.r about local X: + flexes the elbow (scene pose is ~47 deg flexed).")]
    public Vector2 elbowFlexionLimits = new Vector2(-47f, 98f);
    [Tooltip("palm.r about local X: + tilts the fingers up (wrist flexion/extension).")]
    public Vector2 wristFlexionLimits = new Vector2(-70f, 80f);
    [Tooltip("palm.r about local Y: rolls the palm (pronation/supination).")]
    public Vector2 wristPronationLimits = new Vector2(-80f, 80f);
    [Tooltip("When off, shoulder and elbow actions are ignored (joints held at the scene pose) for the distal-only ablation. Action/observation sizes do not change.")]
    public bool actuateProximalJoints = true;
    [Tooltip("Hierarchy path of the shoulder bone (Bicep.r) relative to this agent.")]
    public string shoulderPath = "Armature/Bone/Bicep.r";
    [Tooltip("Hierarchy path of the elbow/forearm bone relative to this agent.")]
    public string forearmPath = "Armature/Bone/Bicep.r/forearm.r";
    [Tooltip("Hierarchy path of the wrist/palm bone relative to this agent.")]
    public string palmPath = "Armature/Bone/Bicep.r/forearm.r/palm.r";

    [Header("Target Spawn (world space)")]
    [Tooltip("Center of the horizontal spawn disk. Default is ~1.3 m from the shoulder pivot, on the table.")]
    public Vector3 spawnCenter = new Vector3(0.48f, 0.488f, 5.57f);
    [Tooltip("Radius (m) of the horizontal spawn disk. Overridden by the 'spawn_radius' environment parameter when present.")]
    public float spawnRadius = 0.25f;
    [Tooltip("Cylinder height is raised by a uniform random amount in [0, spawnHeightRange] (m).")]
    public float spawnHeightRange = 0.10f;
    [Tooltip("Randomize the cylinder yaw about world Y.")]
    public bool randomizeYaw = true;
    [Tooltip("Accept only spawns whose distance from the shoulder pivot is within [x, y] (m) - reachable with margin.")]
    public Vector2 reachRange = new Vector2(1.1f, 1.5f);
    [Tooltip("Max rejection-sampling attempts before falling back to spawnCenter.")]
    public int spawnAttempts = 50;

    [Header("Contact / Penetration")]
    [Tooltip("Max overlap (m) a segment may have with the cylinder. Rotations are clamped so this is never exceeded.")]
    public float penetrationTolerance = 0.0005f;
    [Tooltip("Surface gap (m) at or below which a segment counts as touching the cylinder.")]
    public float contactDistance = 0.003f;
    [Tooltip("Bisection iterations used to find the contact angle when a rotation would penetrate.")]
    public int contactSolveIterations = 5;

    [Header("Reward")]
    [Tooltip("Scale of the per-step distance-delta shaping reward (sum over segments).")]
    public float distanceRewardScale = 1.0f;
    [Tooltip("Scale of the per-step palm-to-cylinder distance-delta shaping (grasp point to cylinder center).")]
    public float palmDistanceRewardScale = 1.0f;
    [Tooltip("Offset (m, palm rotation frame) from the palm pivot to where the cylinder sits in a good grasp; used for reach shaping.")]
    public Vector3 graspPointOffset = new Vector3(0.186f, 0.205f, 0.078f);
    [Tooltip("Reward per step for each segment touching the cylinder.")]
    public float contactRewardPerSegment = 0.00002f;
    [Tooltip("Terminal bonus when a grasp is held for requiredHoldDecisions decisions.")]
    public float successBonus = 1.0f;
    [Tooltip("Segments that must touch the cylinder simultaneously for a grasp.")]
    public int requiredContactSegments = 6;
    [Tooltip("Distinct non-thumb fingers that must be touching for a grasp.")]
    public int requiredDistinctFingers = 2;
    [Tooltip("Require at least one thumb segment in contact for a grasp.")]
    public bool requireThumbContact = true;
    [Tooltip("Consecutive decisions the grasp must be held before the episode ends successfully. Overridden by the 'hold_decisions' environment parameter when present.")]
    public int requiredHoldDecisions = 10;
    [Tooltip("Per-step penalty = -existentialPenaltyScale / MaxStep (faster grasps score higher).")]
    public float existentialPenaltyScale = 1.0f;

    [Header("Observations")]
    [Tooltip("Joint-to-cylinder distances are divided by this (m) so they land roughly in [0, 1].")]
    public float workspaceScale = 0.4f;
    [Tooltip("Cylinder position relative to the palm (palm frame) is divided by this (m). Reach-scale so values stay within [-1, 1.5] across the workspace.")]
    public float targetObsScale = 1.0f;

    [Header("Testing")]
    [Tooltip("Per-action value (-1..1) used when Behavior Type is Heuristic Only. Order: 14 finger groups (index B/M/E, middle B/M/E, ring B/M/E, pinky B/M/E, thumb B/E), then shoulderFlexion, shoulderAbduction, elbowFlexion, wristFlexion, wristPronation.")]
    public float[] heuristicActions = new float[ActionCount];

    /// <summary>Number of successful grasps (success bonus fired) since the component was created.</summary>
    public int SuccessCount { get; private set; }
    /// <summary>Hold requirement in effect this episode: the 'hold_decisions' environment parameter, or requiredHoldDecisions.</summary>
    public int HoldDecisions { get; private set; }
    /// <summary>Segments touching the cylinder after the last action.</summary>
    public int CurrentContacts { get; private set; }
    /// <summary>Commanded angle (deg) of a finger joint group, for inspection.</summary>
    public float GetGroupAngle(int group) => m_Groups != null ? m_Groups[group].angle : 0f;
    /// <summary>Commanded angle (deg) of an arm axis (0..4), for inspection.</summary>
    public float GetArmAngle(int axis) => m_ArmAxes != null ? m_ArmAxes[axis].angle : 0f;
    /// <summary>World position of the palm grasp point (palm pivot + graspPointOffset).</summary>
    public Vector3 GraspPoint => m_Palm != null ? m_Palm.position + m_Palm.TransformDirection(graspPointOffset) : Vector3.zero;
    /// <summary>Distance (m) from the grasp point to the cylinder center (0 at the reference grasp pose).</summary>
    public float GraspPointDistance => cylinderTransform != null ? Vector3.Distance(GraspPoint, cylinderTransform.position) : 0f;

    public const int GroupCount = 14;
    public const int ArmAxisCount = 5;
    public const int ActionCount = GroupCount + ArmAxisCount;
    static readonly string[] k_GroupTags = {
        "indexBase","indexMiddle","indexEnd",
        "middleBase","middleMiddle","middleEnd",
        "ringBase","ringMiddle","ringEnd",
        "pinkyBase","pinkyMiddle","pinkyEnd",
        "thumbBase","thumbEnd"
    };
    // Finger index per group: 0 index, 1 middle, 2 ring, 3 pinky, 4 thumb
    static readonly int[] k_GroupFinger = { 0,0,0, 1,1,1, 2,2,2, 3,3,3, 4,4 };
    const int k_ThumbFinger = 4;

    class JointGroup
    {
        public Transform[] joints;          // transforms carrying the tag
        public Quaternion[] baseRotations;  // local rotation at 0 deg (captured each episode)
        public Collider[] colliders;        // segment colliders moved by this group (own + descendants)
        public float angle;                 // commanded angle about local Z, degrees
    }

    // One rotational axis of an arm bone. Several axes may share a bone; the bone's local rotation is
    // baseRotation * Euler(xAngle, yAngle, zAngle) built from all of its axes.
    class ArmAxis
    {
        public ArmBone bone;
        public int axis;                    // 0 = X, 1 = Y, 2 = Z
        public bool proximal;               // shoulder/elbow (subject to actuateProximalJoints)
        public float angle;                 // commanded angle, degrees
    }

    class ArmBone
    {
        public Transform transform;
        public Quaternion baseRotation;     // local rotation at 0 deg (scene pose, captured in Initialize)
        public Vector3 angles;              // current commanded x/y/z angles
        public Collider[] colliders;        // every arm/hand collider moved by this bone
    }

    JointGroup[] m_Groups;
    ArmAxis[] m_ArmAxes;
    ArmBone[] m_ArmBones;
    Transform[] palmJoints;
    Transform m_Shoulder, m_Forearm, m_Palm;

    // Cylinder references & reward bookkeeping
    private Transform cylinderTransform;
    private Collider cylinderCollider;
    private List<Collider> segmentColliders = new List<Collider>();
    private List<Collider> armColliders = new List<Collider>();   // forearm + palm colliders
    private Dictionary<Collider, int> segmentFinger = new Dictionary<Collider, int>();
    private Dictionary<Collider, float> previousDistances = new Dictionary<Collider, float>();
    private float previousGraspPointDistance;
    private int m_HoldSteps;
    private int m_DecisionPeriod = 1;

    public override void Initialize()
    {
        m_Groups = new JointGroup[GroupCount];
        for (int g = 0; g < GroupCount; g++)
        {
            var joints = FindTransformsWithTags(k_GroupTags[g]);
            m_Groups[g] = new JointGroup { joints = joints, baseRotations = new Quaternion[joints.Length] };
        }
        palmJoints = FindTransformsWithTags("Palm");

        // Cache cylinder Transform & Collider
        var cylObj = GameObject.FindGameObjectWithTag("Cylinder");
        if (cylObj != null)
        {
            cylinderTransform = cylObj.transform;
            cylinderCollider = cylObj.GetComponent<Collider>();
        }

        // Collect all segment colliders for nearest-point / contact queries
        segmentColliders.Clear();
        segmentFinger.Clear();
        for (int g = 0; g < GroupCount; g++)
            foreach (var go in GameObject.FindGameObjectsWithTag(k_GroupTags[g]))
                if (go.TryGetComponent<Collider>(out var col))
                {
                    segmentColliders.Add(col);
                    segmentFinger[col] = k_GroupFinger[g];
                }

        // Colliders moved by each group: its own segments plus every segment further down the finger
        for (int g = 0; g < GroupCount; g++)
        {
            var moved = new List<Collider>();
            foreach (var col in segmentColliders)
                foreach (var joint in m_Groups[g].joints)
                    if (col.transform == joint || col.transform.IsChildOf(joint)) { moved.Add(col); break; }
            m_Groups[g].colliders = moved.ToArray();
        }

        // Arm bones and axes
        m_Shoulder = transform.Find(shoulderPath);
        m_Forearm = transform.Find(forearmPath);
        m_Palm = transform.Find(palmPath);
        armColliders.Clear();
        if (m_Forearm != null && m_Forearm.TryGetComponent<Collider>(out var foreCol)) armColliders.Add(foreCol);
        if (m_Palm != null && m_Palm.TryGetComponent<Collider>(out var palmCol)) armColliders.Add(palmCol);

        var allHand = new List<Collider>(armColliders); allHand.AddRange(segmentColliders);
        var palmAndFingers = new List<Collider>(segmentColliders);
        if (m_Palm != null && m_Palm.TryGetComponent<Collider>(out var pc)) palmAndFingers.Insert(0, pc);
        // Capture the scene pose once: it is 0 deg for every arm axis
        var shoulderBone = new ArmBone { transform = m_Shoulder, colliders = allHand.ToArray(),        baseRotation = m_Shoulder != null ? m_Shoulder.localRotation : Quaternion.identity };
        var forearmBone  = new ArmBone { transform = m_Forearm,  colliders = allHand.ToArray(),        baseRotation = m_Forearm  != null ? m_Forearm.localRotation  : Quaternion.identity };
        var palmBone     = new ArmBone { transform = m_Palm,     colliders = palmAndFingers.ToArray(), baseRotation = m_Palm     != null ? m_Palm.localRotation     : Quaternion.identity };
        m_ArmBones = new[] { shoulderBone, forearmBone, palmBone };
        m_ArmAxes = new[]
        {
            new ArmAxis { bone = shoulderBone, axis = 0, proximal = true  },   // shoulder flexion   (Bicep.r X)
            new ArmAxis { bone = shoulderBone, axis = 2, proximal = true  },   // shoulder abduction (Bicep.r Z)
            new ArmAxis { bone = forearmBone,  axis = 0, proximal = true  },   // elbow flexion      (forearm.r X)
            new ArmAxis { bone = palmBone,     axis = 0, proximal = false },   // wrist flexion      (palm.r X)
            new ArmAxis { bone = palmBone,     axis = 1, proximal = false },   // wrist pronation    (palm.r Y)
        };

        var requester = GetComponent<DecisionRequester>();
        m_DecisionPeriod = requester != null ? Mathf.Max(1, requester.DecisionPeriod) : 1;
        if (heuristicActions == null || heuristicActions.Length != ActionCount)
            heuristicActions = new float[ActionCount];
    }

    private Transform[] FindTransformsWithTags(params string[] tags)
    {
        var list = new List<Transform>();
        foreach (var t in tags)
            foreach (var go in GameObject.FindGameObjectsWithTag(t))
                list.Add(go.transform);
        return list.ToArray();
    }

    public override void OnEpisodeBegin()
    {
        // Reset rigidbody orientation
        Rigidbody rb = arm.GetComponent<Rigidbody>();
        rb.centerOfMass = Vector3.zero;
        rb.inertiaTensorRotation = Quaternion.identity;

        // Arm bones back to the scene pose captured in Initialize (0 deg for every arm axis)
        foreach (var bone in m_ArmBones)
        {
            if (bone.transform == null) continue;
            bone.angles = Vector3.zero;
            bone.transform.localRotation = bone.baseRotation;
        }
        foreach (var ax in m_ArmAxes) ax.angle = 0f;

        // Zero out Z-rotation on all finger joints; that pose is 0 deg for every group
        void ZeroZ(Transform[] group)
        {
            if (group == null) return;
            foreach (var t in group)
            {
                var e = t.localEulerAngles;
                t.localRotation = Quaternion.Euler(e.x, e.y, 0f);
            }
        }

        foreach (var grp in m_Groups)
        {
            ZeroZ(grp.joints);
            for (int j = 0; j < grp.joints.Length; j++) grp.baseRotations[j] = grp.joints[j].localRotation;
            grp.angle = 0f;
        }
        ZeroZ(palmJoints);
        Physics.SyncTransforms();

        SpawnCylinder();

        // Initialize potentials for the shaping rewards
        previousDistances.Clear();
        foreach (var col in segmentColliders)
            previousDistances[col] = SegmentDistance(col);
        previousGraspPointDistance = GraspPointDistance;

        m_HoldSteps = 0;
        CurrentContacts = 0;
        HoldDecisions = Mathf.Max(1, Mathf.RoundToInt(Academy.Instance.EnvironmentParameters.GetWithDefault(
            "hold_decisions", requiredHoldDecisions)));
    }

    // Teleport the cylinder to a random reachable pose: uniform in a horizontal disk around spawnCenter,
    // raised by a random height, random yaw; rejected when outside reachRange from the shoulder pivot or
    // overlapping the arm/hand in its reset pose.
    private void SpawnCylinder()
    {
        if (cylinderTransform == null) return;
        float radius = Academy.Instance.EnvironmentParameters.GetWithDefault("spawn_radius", spawnRadius);
        Vector3 shoulder = m_Shoulder != null ? m_Shoulder.position : transform.position;
        Vector3 chosen = spawnCenter;
        Quaternion chosenRot = cylinderTransform.rotation;
        Quaternion baseRot = Quaternion.Euler(cylinderTransform.eulerAngles.x, 0f, cylinderTransform.eulerAngles.z);
        for (int attempt = 0; attempt < Mathf.Max(1, spawnAttempts); attempt++)
        {
            Vector2 disk = Random.insideUnitCircle * radius;
            Vector3 candidate = spawnCenter + new Vector3(disk.x, Random.Range(0f, spawnHeightRange), disk.y);
            float reach = Vector3.Distance(shoulder, candidate);
            if (reach < reachRange.x || reach > reachRange.y) continue;
            Quaternion rot = randomizeYaw ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * baseRot : baseRot;
            if (OverlapsHand(candidate, rot)) continue;
            chosen = candidate; chosenRot = rot;
            break;
        }
        cylinderTransform.SetPositionAndRotation(chosen, chosenRot);
        Physics.SyncTransforms();
    }

    private bool OverlapsHand(Vector3 position, Quaternion rotation)
    {
        foreach (var col in armColliders)
            if (Physics.ComputePenetration(col, col.transform.position, col.transform.rotation, cylinderCollider, position, rotation, out _, out _)) return true;
        foreach (var col in segmentColliders)
            if (Physics.ComputePenetration(col, col.transform.position, col.transform.rotation, cylinderCollider, position, rotation, out _, out _)) return true;
        return false;
    }

    private void FixedUpdate()
    {
        // (no per-step guards needed with distance reward)
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // 1) Finger joint angles as sin/cos (continuous, no wrap) - 2 per group = 28
        foreach (var grp in m_Groups)
        {
            float rad = grp.angle * Mathf.Deg2Rad;
            sensor.AddObservation(Mathf.Sin(rad));
            sensor.AddObservation(Mathf.Cos(rad));
        }

        // 2) Joint-to-cylinder distance (center-to-center) scaled to ~[0, 1] - 1 per group = 14
        float scale = Mathf.Max(workspaceScale, 1e-4f);
        foreach (var grp in m_Groups)
        {
            float d = grp.joints.Length > 0 && cylinderTransform != null
                ? Vector3.Distance(grp.joints[0].position, cylinderTransform.position) / scale
                : 0f;
            sensor.AddObservation(Mathf.Clamp(d, 0f, 1.5f));
        }

        // 3) Arm axis angles as sin/cos - 2 per axis = 10
        foreach (var ax in m_ArmAxes)
        {
            float rad = ax.angle * Mathf.Deg2Rad;
            sensor.AddObservation(Mathf.Sin(rad));
            sensor.AddObservation(Mathf.Cos(rad));
        }

        // 4) Cylinder position relative to the palm, in the palm's rotation frame - 3
        Vector3 rel = (m_Palm != null && cylinderTransform != null)
            ? m_Palm.InverseTransformDirection(cylinderTransform.position - m_Palm.position) / Mathf.Max(targetObsScale, 1e-4f)
            : Vector3.zero;
        sensor.AddObservation(Vector3.ClampMagnitude(rel, 1.5f));
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var a = actionsOut.ContinuousActions;
        for (int i = 0; i < ActionCount && i < a.Length; i++)
            a[i] = heuristicActions != null && i < heuristicActions.Length ? Mathf.Clamp(heuristicActions[i], -1f, 1f) : 0f;
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        var a = actions.ContinuousActions;
        float dt = Time.deltaTime;

        // Arm axes first (proximal to distal), each within its limits and stopping at contact with the cylinder
        for (int i = 0; i < ArmAxisCount && GroupCount + i < a.Length; i++)
        {
            var ax = m_ArmAxes[i];
            if (ax.bone.transform == null) continue;
            if (ax.proximal && !actuateProximalJoints) continue;
            var lim = GetArmLimits(i);
            float target = Mathf.Clamp(ax.angle + a[GroupCount + i] * armRotationSpeed * dt, lim.x, lim.y);
            ApplyArmAngleWithContactClamp(ax, target);
        }

        // Rotate each finger group within its limits, stopping at contact with the cylinder
        for (int g = 0; g < GroupCount; g++)
        {
            var lim = GetLimits(g);
            float target = Mathf.Clamp(m_Groups[g].angle + a[g] * rotationSpeed * dt, lim.x, lim.y);
            ApplyAngleWithContactClamp(m_Groups[g], target);
        }

        // Reward
        float totalReward = 0f;
        int contacts = 0;
        bool thumbTouching = false;
        int fingerMask = 0;
        foreach (var col in segmentColliders)
        {
            // distance-based incremental shaping (kept from the original reward)
            float currDist = SegmentDistance(col);
            float delta = previousDistances[col] - currDist;
            totalReward += delta * distanceRewardScale;
            previousDistances[col] = currDist;

            if (IsTouching(col))
            {
                contacts++;
                totalReward += contactRewardPerSegment;
                int finger = segmentFinger[col];
                if (finger == k_ThumbFinger) thumbTouching = true;
                else fingerMask |= 1 << finger;
            }
        }
        CurrentContacts = contacts;

        // palm (grasp point) to cylinder potential-based shaping
        float graspDist = GraspPointDistance;
        totalReward += (previousGraspPointDistance - graspDist) * palmDistanceRewardScale;
        previousGraspPointDistance = graspDist;

        if (MaxStep > 0)
            totalReward -= existentialPenaltyScale / MaxStep;

        AddReward(totalReward);

        // Success: grasp held for K consecutive decisions
        int distinctFingers = CountBits(fingerMask);
        bool grasp = contacts >= requiredContactSegments
                  && distinctFingers >= requiredDistinctFingers
                  && (!requireThumbContact || thumbTouching);
        m_HoldSteps = grasp ? m_HoldSteps + 1 : 0;
        if (grasp && m_HoldSteps >= (HoldDecisions > 0 ? HoldDecisions : requiredHoldDecisions) * m_DecisionPeriod)
        {
            SuccessCount++;
            AddReward(successBonus);
            EndEpisode();
        }
    }

    // ---- finger groups ----

    // Rotate the group to 'target' degrees; if any of its segments would overlap the cylinder,
    // bisect back toward the previous angle so the finger stops at the surface.
    private void ApplyAngleWithContactClamp(JointGroup grp, float target)
    {
        float from = grp.angle;
        if (Mathf.Approximately(target, from)) return;

        SetGroupAngle(grp, target);
        if (!Penetrates(grp.colliders)) { grp.angle = target; return; }

        float lo = from, hi = target;
        for (int i = 0; i < contactSolveIterations; i++)
        {
            float mid = 0.5f * (lo + hi);
            SetGroupAngle(grp, mid);
            if (Penetrates(grp.colliders)) hi = mid; else lo = mid;
        }
        SetGroupAngle(grp, lo);
        grp.angle = lo;
    }

    private void SetGroupAngle(JointGroup grp, float angle)
    {
        var rot = Quaternion.Euler(0f, 0f, angle);
        for (int j = 0; j < grp.joints.Length; j++)
            grp.joints[j].localRotation = grp.baseRotations[j] * rot;
        Physics.SyncTransforms();
    }

    // ---- arm axes ----

    private void ApplyArmAngleWithContactClamp(ArmAxis ax, float target)
    {
        float from = ax.angle;
        if (Mathf.Approximately(target, from)) return;

        SetArmAngle(ax, target);
        if (!Penetrates(ax.bone.colliders)) { ax.angle = target; return; }

        float lo = from, hi = target;
        for (int i = 0; i < contactSolveIterations; i++)
        {
            float mid = 0.5f * (lo + hi);
            SetArmAngle(ax, mid);
            if (Penetrates(ax.bone.colliders)) hi = mid; else lo = mid;
        }
        SetArmAngle(ax, lo);
        ax.angle = lo;
    }

    private void SetArmAngle(ArmAxis ax, float angle)
    {
        var angles = ax.bone.angles;
        angles[ax.axis] = angle;
        ax.bone.angles = angles;
        ax.bone.transform.localRotation = ax.bone.baseRotation * Quaternion.Euler(angles);
        Physics.SyncTransforms();
    }

    private Vector2 GetArmLimits(int axis)
    {
        switch (axis)
        {
            case 0: return shoulderFlexionLimits;
            case 1: return shoulderAbductionLimits;
            case 2: return elbowFlexionLimits;
            case 3: return wristFlexionLimits;
            default: return wristPronationLimits;
        }
    }

    // ---- shared queries ----

    private bool Penetrates(Collider[] colliders)
    {
        if (cylinderCollider == null) return false;
        foreach (var col in colliders)
        {
            if (Physics.ComputePenetration(col, col.transform.position, col.transform.rotation,
                    cylinderCollider, cylinderTransform.position, cylinderTransform.rotation,
                    out _, out float depth) && depth > penetrationTolerance)
                return true;
        }
        return false;
    }

    private bool IsTouching(Collider col)
    {
        if (cylinderCollider == null) return false;
        if (Physics.ComputePenetration(col, col.transform.position, col.transform.rotation,
                cylinderCollider, cylinderTransform.position, cylinderTransform.rotation, out _, out _))
            return true;
        Vector3 onCylinder = cylinderCollider.ClosestPoint(col.transform.position);
        Vector3 onSegment = col.ClosestPoint(onCylinder);
        return Vector3.Distance(onCylinder, onSegment) <= contactDistance;
    }

    private float SegmentDistance(Collider col)
    {
        return Vector3.Distance(
            col.ClosestPoint(cylinderTransform.position),
            cylinderCollider.ClosestPoint(col.transform.position));
    }

    private Vector2 GetLimits(int group)
    {
        switch (group)
        {
            case 0: return indexBaseLimits;   case 1: return indexMiddleLimits;   case 2: return indexEndLimits;
            case 3: return middleBaseLimits;  case 4: return middleMiddleLimits;  case 5: return middleEndLimits;
            case 6: return ringBaseLimits;    case 7: return ringMiddleLimits;    case 8: return ringEndLimits;
            case 9: return pinkyBaseLimits;   case 10: return pinkyMiddleLimits;  case 11: return pinkyEndLimits;
            case 12: return thumbBaseLimits;  default: return thumbEndLimits;
        }
    }

    private static int CountBits(int v) { int c = 0; while (v != 0) { c += v & 1; v >>= 1; } return c; }
}
