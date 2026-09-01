using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections.Generic;

public class ArmGraspAgent : Agent
{
    [Header("Rotation Settings")]
    [Tooltip("Degrees of rotation per action unit.")]
    public float rotationSpeed = 90f;
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
    [Tooltip("Consecutive decisions the grasp must be held before the episode ends successfully.")]
    public int requiredHoldDecisions = 10;
    [Tooltip("Per-step penalty = -existentialPenaltyScale / MaxStep (faster grasps score higher).")]
    public float existentialPenaltyScale = 1.0f;

    [Header("Observations")]
    [Tooltip("Joint-to-cylinder distances are divided by this (m) so they land roughly in [0, 1].")]
    public float workspaceScale = 0.4f;

    [Header("Testing")]
    [Tooltip("Per-joint-group action (-1..1) used when Behavior Type is Heuristic Only. Order: index B/M/E, middle B/M/E, ring B/M/E, pinky B/M/E, thumb B/E.")]
    public float[] heuristicActions = new float[GroupCount];

    /// <summary>Number of successful grasps (success bonus fired) since the component was created.</summary>
    public int SuccessCount { get; private set; }
    /// <summary>Segments touching the cylinder after the last action.</summary>
    public int CurrentContacts { get; private set; }
    /// <summary>Commanded angle (deg) of a joint group, for inspection.</summary>
    public float GetGroupAngle(int group) => m_Groups != null ? m_Groups[group].angle : 0f;

    public const int GroupCount = 14;
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

    JointGroup[] m_Groups;
    Transform[] palmJoints;

    // Cylinder references & reward bookkeeping
    private Transform cylinderTransform;
    private Collider cylinderCollider;
    private List<Collider> segmentColliders = new List<Collider>();
    private Dictionary<Collider, int> segmentFinger = new Dictionary<Collider, int>();
    private Dictionary<Collider, float> previousDistances = new Dictionary<Collider, float>();
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

        var requester = GetComponent<DecisionRequester>();
        m_DecisionPeriod = requester != null ? Mathf.Max(1, requester.DecisionPeriod) : 1;
        if (heuristicActions == null || heuristicActions.Length != GroupCount)
            heuristicActions = new float[GroupCount];
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

        // Zero out Z-rotation on all joints; that pose is 0 deg for every group
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

        // Initialize previousDistances for each segment
        previousDistances.Clear();
        foreach (var col in segmentColliders)
            previousDistances[col] = SegmentDistance(col);

        m_HoldSteps = 0;
        CurrentContacts = 0;
    }

    private void FixedUpdate()
    {
        // (no per-step guards needed with distance reward)
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // 1) Joint angles as sin/cos (continuous, no wrap) - 2 per group = 28
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
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var a = actionsOut.ContinuousActions;
        for (int g = 0; g < GroupCount && g < a.Length; g++)
            a[g] = heuristicActions != null && g < heuristicActions.Length ? Mathf.Clamp(heuristicActions[g], -1f, 1f) : 0f;
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        var a = actions.ContinuousActions;
        float dt = Time.deltaTime;

        // Rotate each joint group within its limits, stopping at contact with the cylinder
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

        if (MaxStep > 0)
            totalReward -= existentialPenaltyScale / MaxStep;

        AddReward(totalReward);

        // Success: grasp held for K consecutive decisions
        int distinctFingers = CountBits(fingerMask);
        bool grasp = contacts >= requiredContactSegments
                  && distinctFingers >= requiredDistinctFingers
                  && (!requireThumbContact || thumbTouching);
        m_HoldSteps = grasp ? m_HoldSteps + 1 : 0;
        if (grasp && m_HoldSteps >= requiredHoldDecisions * m_DecisionPeriod)
        {
            SuccessCount++;
            AddReward(successBonus);
            EndEpisode();
        }
    }

    // Rotate the group to 'target' degrees; if any of its segments would overlap the cylinder,
    // bisect back toward the previous angle so the finger stops at the surface.
    private void ApplyAngleWithContactClamp(JointGroup grp, float target)
    {
        float from = grp.angle;
        if (Mathf.Approximately(target, from)) return;

        SetGroupAngle(grp, target);
        if (!Penetrates(grp)) { grp.angle = target; return; }

        float lo = from, hi = target;
        for (int i = 0; i < contactSolveIterations; i++)
        {
            float mid = 0.5f * (lo + hi);
            SetGroupAngle(grp, mid);
            if (Penetrates(grp)) hi = mid; else lo = mid;
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

    private bool Penetrates(JointGroup grp)
    {
        if (cylinderCollider == null) return false;
        foreach (var col in grp.colliders)
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
