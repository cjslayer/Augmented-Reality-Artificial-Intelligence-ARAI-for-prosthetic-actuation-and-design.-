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
    [Tooltip("Per-term multiplier of the normalized segment-distance shaping (delta / episode-initial distance).")]
    public float distanceRewardScale = 1.0f;
    [Tooltip("Per-term multiplier of the normalized grasp-point shaping (delta / episode-initial distance).")]
    public float palmDistanceRewardScale = 1.0f;
    [Tooltip("Scale applied to the SUM of the 15 normalized potentials (14 segments + grasp point). 1/15 caps the episode shaping budget at 1.0.")]
    public float shapingScale = 1f / 15f;
    [Tooltip("Floor (m) for the episode-initial distance each potential is normalized by.")]
    public float shapingFloorDistance = 0.05f;
    [Tooltip("Offset (m, palm rotation frame) from the palm pivot to where the cylinder sits in a good grasp; used for reach shaping.")]
    public Vector3 graspPointOffset = new Vector3(0.186f, 0.205f, 0.078f);
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

    [Header("Grasp Quality Q (paid while the hold criterion is met)")]
    [Tooltip("Weight of the saturating contact-count term (segments only; saturates at qualityContactSaturation).")]
    public float qualityContactWeight = 0.25f;
    [Tooltip("Weight of the azimuthal-coverage term: 0 while the largest angular gap between contacts is >= qualityCoverageRampStartDeg, ramping to 1 over qualityCoverageRampDeg.")]
    public float qualityCoverageWeight = 0.15f;
    [Tooltip("Largest-gap value (deg) at which coverage credit starts; recalibrated to the reachable range (observed gaps 186-220 deg).")]
    public float qualityCoverageRampStartDeg = 240f;
    [Tooltip("Coverage credit reaches 1 when the largest gap is qualityCoverageRampDeg below the ramp start.")]
    public float qualityCoverageRampDeg = 60f;
    [Tooltip("Weight of the thumb antipodality term: angle between the thumb contact azimuth and the mean finger azimuth, peak at 180 deg.")]
    public float qualityAntipodalWeight = 0.10f;
    [Tooltip("Weight of the binary palm-contact term (palm collider touching; never counts toward the success gate).")]
    public float qualityPalmWeight = 0.20f;
    [Tooltip("Weight of the wedge-posture term: vertical spread of the contact heights (palm-low / fingertips-high), paid only while the palm touches. From the drop-test analysis: spread predicts the sustained normal force.")]
    public float qualityWedgeWeight = 0.30f;
    [Tooltip("Vertical contact spread (m) at which the wedge term starts paying.")]
    public float qualityWedgeSpreadStart = 0.42f;
    [Tooltip("Wedge term reaches 1 at qualityWedgeSpreadStart + this ramp (m).")]
    public float qualityWedgeSpreadRamp = 0.03f;
    [Tooltip("Opposition gate on the wedge term: the wedge credit is multiplied by clamp(antipodality / this, 0, 1). The self-tightening mechanism needs opposed contacts; a one-sided contact set can relieve all depenetration by lateral translation, so spread without opposition earns nothing.")]
    public float qualityWedgeOppositionThreshold = 0.5f;
    [Tooltip("Contact count at which the contact term saturates; segments beyond this pay nothing.")]
    public int qualityContactSaturation = 8;
    [Tooltip("Maximum number of steps per episode on which Q is paid (anti-farming cap).")]
    public int qualityBudgetSteps = 50;
    [Tooltip("Q is multiplied by this on each paying step; 1/50 with a 50-step budget caps quality pay at 1.0 per episode.")]
    public float qualityPayPerStep = 1f / 50f;

    [Header("Impedance Actuation (14 finger groups + wrist flexion/pronation)")]
    [Tooltip("Actions move each joint's equilibrium setpoint by up to this many deg/s; the joint follows through its spring-damper.")]
    public float setpointRateDegPerSec = 180f;
    [Tooltip("Neutral pose (deg) held rigidly by masked (non-actuated) finger groups. Order: index B/M/E, middle B/M/E, ring B/M/E, pinky B/M/E, thumb B/E.")]
    public float[] neutralPoseDeg = { -30f, -20f, -10f, -30f, -20f, -10f, -30f, -20f, -10f, -30f, -20f, -10f, 20f, 20f };
    [Tooltip("Wedge thresholds scale with handSpan / this reference span (m); 0 = measure the reference from the unscaled rig at Initialize.")]
    public float wedgeHandSpanRef = 0f;

    [Header("Effort / Safety (placeholders for sEMG and device limits)")]
    [Tooltip("Per-step penalty = -effortWeight * sum(action^2) over all 19 actions.")]
    public float effortWeight = 2e-5f;
    [Tooltip("Per-step penalty = -safetyWeight * (number of joints whose setpoint or velocity command saturated at a joint limit this step). Penetration-clamp engagements are logged, not penalized.")]
    public float safetyWeight = 1e-4f;

    [Header("Episode Stats")]
    [Tooltip("If set, one CSV row per episode is appended to this file (Editor diagnostics). Stats are always sent to the ML-Agents StatsRecorder.")]
    public string statsCsvPath = "";

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
    /// <summary>graspPointOffset with its in-plane components (palm local X across the palm, Y along the fingers) scaled by handSpan / handSpanRef; the height above the palm (Z) is unchanged.</summary>
    public Vector3 EffectiveGraspPointOffset => new Vector3(graspPointOffset.x * m_HandSpanRatio, graspPointOffset.y * m_HandSpanRatio, graspPointOffset.z);
    public Vector3 GraspPoint => m_Palm != null ? m_Palm.position + m_Palm.TransformDirection(EffectiveGraspPointOffset) : Vector3.zero;
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
        public float angle;                 // realized angle about local Z, degrees
        public float vel;                   // angular velocity, deg/s (impedance state)
        public float setpoint;              // equilibrium setpoint, degrees (moved by the action)
        public bool masked;                 // held rigidly at the neutral pose; no token, no dynamics
    }

    // One rotational axis of an arm bone. Several axes may share a bone; the bone's local rotation is
    // baseRotation * Euler(xAngle, yAngle, zAngle) built from all of its axes.
    class ArmAxis
    {
        public ArmBone bone;
        public int axis;                    // 0 = X, 1 = Y, 2 = Z
        public bool proximal;               // shoulder/elbow (subject to actuateProximalJoints)
        public bool impedance;              // wrist axes: spring-damper on a setpoint; proximal axes: velocity command
        public float angle;                 // realized angle, degrees
        public float vel;                   // deg/s (impedance axes)
        public float setpoint;              // degrees (impedance axes)
    }

    // ---- morphology / impedance state ----
    MorphologyManager m_Morph;
    Unity.MLAgents.Sensors.BufferSensorComponent m_TokenSensor;
    public const int TokenSize = 13;
    public const int MaxTokens = GroupCount + 2;
    static readonly int[] k_GroupParent = { -1, 0, 1, -1, 3, 4, -1, 6, 7, -1, 9, 10, -1, 12 };   // -1 = palm
    static readonly int[] k_GroupSegment = { 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1 };
    float[] m_Token = new float[TokenSize];
    float m_HandSpanRatio = 1f;
    int m_ClampEvents, m_LimitEvents; float m_EffortReturn, m_SafetyReturn; int m_EpisodeClampEvents, m_EpisodeLimitEvents;
    public float EffortReturn => m_EffortReturn;
    public float SafetyReturn => m_SafetyReturn;
    public int EpisodeClampEvents => m_EpisodeClampEvents;
    public int EpisodeLimitEvents => m_EpisodeLimitEvents;
    public float HandSpanRatio => m_HandSpanRatio;
    public MorphologyManager Morphology => m_Morph;
    /// <summary>Impedance state of a finger group (angle deg, velocity deg/s, setpoint deg), for diagnostics.</summary>
    public Vector3 GetGroupState(int g) => m_Groups != null ? new Vector3(m_Groups[g].angle, m_Groups[g].vel, m_Groups[g].setpoint) : Vector3.zero;
    public bool IsGroupMasked(int g) => m_Groups != null && m_Groups[g].masked;
    public Vector3 GetArmState(int axis) => m_ArmAxes != null ? new Vector3(m_ArmAxes[axis].angle, m_ArmAxes[axis].vel, m_ArmAxes[axis].setpoint) : Vector3.zero;
    // Per-group one-step transition matrices [[x->x, v->x],[x->v, v->v]] of the spring-damper x'' = -w^2 x - 2 zeta w x'
    // over the fixed timestep, stored as Vector4 (Phi11, Phi12, Phi21, Phi22). Recomputed whenever (k, b, I) change.
    Vector4[] m_Phi = new Vector4[MorphologyManager.GroupCount];

    /// <summary>Closed-form transition matrix of the damped oscillator over h; branches on the damping regime.</summary>
    public static Vector4 TransitionMatrix(float k, float b, float I, float h)
    {
        I = Mathf.Max(I, 1e-9f);
        float w = Mathf.Sqrt(Mathf.Max(k, 0f) / I);
        if (w < 1e-6f) return new Vector4(1f, h, 0f, 1f);                       // no spring: free drift
        float zeta = b / (2f * I * w);
        float a = zeta * w;                                                     // decay rate
        float e = Mathf.Exp(-a * h);
        if (Mathf.Abs(zeta - 1f) < 1e-3f)                                       // critically damped (and the near-critical guard)
            return new Vector4(e * (1f + w * h), e * h, -e * w * w * h, e * (1f - w * h));
        if (zeta < 1f)                                                          // underdamped
        {
            float wd = w * Mathf.Sqrt(1f - zeta * zeta); float c = Mathf.Cos(wd * h), s = Mathf.Sin(wd * h);
            return new Vector4(e * (c + a / wd * s), e * s / wd, -e * (w * w / wd) * s, e * (c - a / wd * s));
        }
        {                                                                       // overdamped
            float beta = w * Mathf.Sqrt(zeta * zeta - 1f); float ch = (float)System.Math.Cosh(beta * h), sh = (float)System.Math.Sinh(beta * h);
            return new Vector4(e * (ch + a / beta * sh), e * sh / beta, -e * (w * w / beta) * sh, e * (ch - a / beta * sh));
        }
    }

    /// <summary>Recompute the per-group transition matrices from the MorphologyManager's current (k, b, I).</summary>
    public void RefreshImpedanceMatrices()
    {
        if (m_Morph == null) return;
        float h = Time.fixedDeltaTime;
        for (int g = 0; g < MorphologyManager.GroupCount; g++) m_Phi[g] = TransitionMatrix(m_Morph.stiffness[g], m_Morph.damping[g], m_Morph.inertia[g], h);
    }

    /// <summary>Diagnostics only: place a finger group's equilibrium setpoint (deg) directly, e.g. for a step-response test.</summary>
    public void SetGroupSetpoint(int g, float deg) { if (m_Groups != null) { var lim = GetLimits(g); m_Groups[g].setpoint = Mathf.Clamp(deg, lim.x, lim.y); } }
    public void SetArmSetpoint(int axis, float deg) { if (m_ArmAxes != null) { var lim = GetArmLimits(axis); m_ArmAxes[axis].setpoint = Mathf.Clamp(deg, lim.x, lim.y); } }

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
    private Dictionary<Collider, float> initialDistances = new Dictionary<Collider, float>();
    private float previousGraspPointDistance;
    private float initialGraspPointDistance;
    private Collider m_PalmCollider;
    private int m_HoldSteps;
    private int m_DecisionPeriod = 1;

    // One contact sample per touching collider (segments, plus the palm for quality only)
    struct ContactSample { public float azimuthDeg; public float height; public bool isThumb; public bool isPalm; }
    private List<ContactSample> m_Contacts = new List<ContactSample>();
    private List<float> m_Azimuths = new List<float>();

    // Per-episode bookkeeping (reset in OnEpisodeBegin, reported at episode end)
    private int m_QualityStepsPaid;
    private float m_ShapingReturn, m_QualityReturn, m_PenaltyReturn, m_BonusReturn;
    private int m_StepsToFirstSixContacts = -1, m_StepsToFirstHoldCriterion = -1;
    private float m_MinGraspPointDistance, m_MaxPenetration, m_SpawnDistance;
    private int m_HoldWindowCount; private double m_HoldWindowSum, m_HoldWindowSumSq;
    private bool m_EpisodeActive;

    /// <summary>Latest quality score Q in [0,1] (computed every step, paid only while the hold criterion is met).</summary>
    public float LastQuality { get; private set; }
    public float LastCoverageGapDeg { get; private set; }
    public float LastAntipodality { get; private set; }
    public float LastVerticalSpread { get; private set; }
    public float LastWedge { get; private set; }
    public bool LastPalmTouching { get; private set; }
    public int LastDistinctFingers { get; private set; }
    public bool LastThumbTouching { get; private set; }
    public bool LastHoldCriterionMet { get; private set; }
    public int QualityStepsPaid => m_QualityStepsPaid;
    public float ShapingReturn => m_ShapingReturn;
    public float QualityReturn => m_QualityReturn;
    public float PenaltyReturn => m_PenaltyReturn;
    public float BonusReturn => m_BonusReturn;
    public float MaxPenetration => m_MaxPenetration;
    public float SpawnDistance => m_SpawnDistance;
    /// <summary>Sum of the 14 segment distances after the last action.</summary>
    public float ResidualSegmentDistance { get; private set; }

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
        if (m_Palm != null && m_Palm.TryGetComponent<Collider>(out var pc)) { palmAndFingers.Insert(0, pc); m_PalmCollider = pc; }
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

        m_ArmAxes[3].impedance = true; m_ArmAxes[4].impedance = true;   // wrist flexion / pronation: transradial device joints

        // Morphology manager (link scales, impedance parameters, mask) and the per-joint token sensor
        m_Morph = GetComponent<MorphologyManager>();
        if (m_Morph != null)
        {
            var groupJoints = new Transform[GroupCount][];
            for (int g = 0; g < GroupCount; g++) groupJoints[g] = m_Groups[g].joints;
            m_Morph.Initialize(groupJoints, m_Palm);
            if (wedgeHandSpanRef <= 0f) wedgeHandSpanRef = m_Morph.HandSpanRef;
        }
        m_TokenSensor = GetComponent<Unity.MLAgents.Sensors.BufferSensorComponent>();
        if (neutralPoseDeg == null || neutralPoseDeg.Length != GroupCount) neutralPoseDeg = new float[GroupCount];

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
        foreach (var ax in m_ArmAxes) { ax.angle = 0f; ax.vel = 0f; ax.setpoint = 0f; }

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
            grp.angle = 0f; grp.vel = 0f; grp.setpoint = 0f; grp.masked = false;
        }
        ZeroZ(palmJoints);
        Physics.SyncTransforms();

        // Report the previous episode if it ended without success (MaxStep interruption)
        if (m_EpisodeActive) LogEpisode(false);

        // Morphology for this episode: link scales, spring parameters, actuation mask; masked groups hold the neutral pose
        m_HandSpanRatio = 1f;
        if (m_Morph != null)
        {
            m_Morph.ApplyForEpisode();
            m_HandSpanRatio = wedgeHandSpanRef > 0f ? m_Morph.HandSpan / wedgeHandSpanRef : 1f;
            RefreshImpedanceMatrices();
            for (int g = 0; g < GroupCount; g++)
            {
                m_Groups[g].masked = !m_Morph.mask[g];
                if (m_Groups[g].masked)
                {
                    var lim = GetLimits(g);
                    float neutral = Mathf.Clamp(neutralPoseDeg[g], lim.x, lim.y);
                    SetGroupAngle(m_Groups[g], neutral); m_Groups[g].angle = neutral; m_Groups[g].setpoint = neutral;
                }
            }
            Physics.SyncTransforms();
        }
        m_ClampEvents = m_LimitEvents = 0; m_EpisodeClampEvents = m_EpisodeLimitEvents = 0; m_EffortReturn = m_SafetyReturn = 0f;

        SpawnCylinder();

        // Initialize potentials for the shaping rewards; each is normalized by its episode-initial value
        previousDistances.Clear();
        initialDistances.Clear();
        foreach (var col in segmentColliders)
        {
            float d0 = SegmentDistance(col);
            previousDistances[col] = d0;
            initialDistances[col] = Mathf.Max(d0, shapingFloorDistance);
        }
        previousGraspPointDistance = GraspPointDistance;
        initialGraspPointDistance = Mathf.Max(previousGraspPointDistance, shapingFloorDistance);

        m_HoldSteps = 0;
        CurrentContacts = 0;
        m_QualityStepsPaid = 0;
        m_ShapingReturn = m_QualityReturn = m_PenaltyReturn = m_BonusReturn = 0f;
        m_StepsToFirstSixContacts = m_StepsToFirstHoldCriterion = -1;
        m_MinGraspPointDistance = previousGraspPointDistance;
        m_MaxPenetration = 0f;
        m_SpawnDistance = (m_Shoulder != null && cylinderTransform != null) ? Vector3.Distance(m_Shoulder.position, cylinderTransform.position) : 0f;
        m_HoldWindowCount = 0; m_HoldWindowSum = m_HoldWindowSumSq = 0;
        LastQuality = 0f; LastCoverageGapDeg = 360f; LastAntipodality = 0f; LastVerticalSpread = 0f; LastWedge = 0f; LastPalmTouching = false; LastDistinctFingers = 0; LastThumbTouching = false; LastHoldCriterionMet = false;
        m_EpisodeActive = true;
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

    // Fixed vector observation (22): palm-frame target 3, task progress 3, arm axes sin/cos 10, morphology summary 6.
    // Per-joint tokens (BufferSensor, up to 16 x 13): one per ACTIVE finger group plus the two wrist axes.
    public override void CollectObservations(VectorSensor sensor)
    {
        // 1) Cylinder position relative to the palm, in the palm's rotation frame - 3
        Vector3 rel = (m_Palm != null && cylinderTransform != null)
            ? m_Palm.InverseTransformDirection(cylinderTransform.position - m_Palm.position) / Mathf.Max(targetObsScale, 1e-4f)
            : Vector3.zero;
        sensor.AddObservation(Vector3.ClampMagnitude(rel, 1.5f));

        // 2) Task progress - 3: hold fraction, contacts / 14, quality budget spent
        int holdNeeded = Mathf.Max(1, (HoldDecisions > 0 ? HoldDecisions : requiredHoldDecisions) * m_DecisionPeriod);
        sensor.AddObservation(Mathf.Clamp01((float)m_HoldSteps / holdNeeded));
        sensor.AddObservation(CurrentContacts / (float)GroupCount);
        sensor.AddObservation(qualityBudgetSteps > 0 ? Mathf.Clamp01((float)m_QualityStepsPaid / qualityBudgetSteps) : 0f);

        // 3) Arm axis angles as sin/cos - 2 per axis = 10
        foreach (var ax in m_ArmAxes)
        {
            float rad = ax.angle * Mathf.Deg2Rad;
            sensor.AddObservation(Mathf.Sin(rad));
            sensor.AddObservation(Mathf.Cos(rad));
        }

        // 4) Morphology summary - 6: five link-length scales, active finger groups / 14
        for (int f = 0; f < MorphologyManager.FingerCount; f++) sensor.AddObservation(m_Morph != null ? m_Morph.lengthScale[f] : 1f);
        sensor.AddObservation(m_Morph != null ? m_Morph.ActiveCount / (float)GroupCount : 1f);

        // 5) Joint tokens (variable length): active finger groups, then the wrist axes
        if (m_TokenSensor == null) return;
        float scale = Mathf.Max(workspaceScale, 1e-4f);
        for (int g = 0; g < GroupCount; g++)
        {
            var grp = m_Groups[g];
            if (grp.masked) continue;
            var lim = GetLimits(g);
            float d = grp.joints.Length > 0 && cylinderTransform != null ? Vector3.Distance(grp.joints[0].position, cylinderTransform.position) / scale : 0f;
            FillToken(k_GroupParent[g] < 0 ? 0f : (k_GroupParent[g] + 1) / (float)MaxTokens, m_Morph != null ? m_Morph.LinkLength[g] : 0.08f,
                      (m_Morph != null ? m_Morph.FingerOfGroup(g) : k_GroupFinger[g]) / 4f, k_GroupSegment[g] / 2f, lim, g, grp.angle, grp.vel, grp.setpoint, Mathf.Clamp(d, 0f, 1.5f));
            m_TokenSensor.AppendObservation(m_Token);
        }
        for (int w = 0; w < 2; w++)
        {
            var ax = m_ArmAxes[3 + w]; var lim = GetArmLimits(3 + w);
            float d = m_Palm != null && cylinderTransform != null ? Vector3.Distance(m_Palm.position, cylinderTransform.position) / scale : 0f;
            FillToken(0f, m_Morph != null ? m_Morph.HandSpan : 0.45f, 5f / 4f, w / 2f, lim, GroupCount + w, ax.angle, ax.vel, ax.setpoint, Mathf.Clamp(d, 0f, 1.5f));
            m_TokenSensor.AppendObservation(m_Token);
        }
    }

    void FillToken(float parent, float length, float fingerCode, float segCode, Vector2 lim, int morphIndex, float angle, float vel, float setpoint, float dist)
    {
        float rad = angle * Mathf.Deg2Rad;
        float k = m_Morph != null ? m_Morph.stiffness[morphIndex] : 1f, I = m_Morph != null ? m_Morph.inertia[morphIndex] : 1e-3f;
        m_Token[0] = parent;
        m_Token[1] = length / 0.15f;
        m_Token[2] = fingerCode;
        m_Token[3] = segCode;
        m_Token[4] = lim.x / 90f;
        m_Token[5] = lim.y / 90f;
        m_Token[6] = Mathf.Log10(Mathf.Max(k, 1e-6f)) / 3f;      // k in N m/rad: ~1e-3 .. 1e1 -> -1 .. 0.33
        m_Token[7] = Mathf.Log10(Mathf.Max(I, 1e-9f)) / 3f;      // I in kg m^2: ~1e-5 .. 1e-1 -> -1.7 .. -0.33
        m_Token[8] = Mathf.Sin(rad);
        m_Token[9] = Mathf.Cos(rad);
        m_Token[10] = Mathf.Clamp(vel * Mathf.Deg2Rad / 10f, -2f, 2f);
        m_Token[11] = Mathf.Clamp((setpoint - angle) / 90f, -2f, 2f);
        m_Token[12] = dist;
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

        m_ClampEvents = 0; m_LimitEvents = 0;
        float sumA2 = 0f;
        for (int i = 0; i < a.Length; i++) sumA2 += a[i] * a[i];

        // Arm axes first (proximal to distal), each within its limits and stopping at contact with the cylinder.
        // Shoulder/elbow: velocity command (they emulate the user's arm). Wrist: impedance setpoint (transradial device).
        for (int i = 0; i < ArmAxisCount && GroupCount + i < a.Length; i++)
        {
            var ax = m_ArmAxes[i];
            if (ax.bone.transform == null) continue;
            if (ax.proximal && !actuateProximalJoints) continue;
            var lim = GetArmLimits(i);
            float target;
            if (ax.impedance && m_Morph != null)
            {
                float sp = ax.setpoint + a[GroupCount + i] * setpointRateDegPerSec * dt;
                if (sp < lim.x || sp > lim.y) { m_LimitEvents++; sp = Mathf.Clamp(sp, lim.x, lim.y); }
                ax.setpoint = sp;
                int mi = GroupCount + (i - 3);
                // exact one-step transition of the linear spring-damper about the setpoint
                float x = ax.angle - ax.setpoint, v = ax.vel; var P = m_Phi[mi];
                float nx = P.x * x + P.y * v; ax.vel = P.z * x + P.w * v;
                target = ax.setpoint + nx;
                if (target < lim.x || target > lim.y) { target = Mathf.Clamp(target, lim.x, lim.y); ax.vel = 0f; }
                ApplyArmAngleWithContactClamp(ax, target);
                if (!Mathf.Approximately(ax.angle, target)) { ax.vel = 0f; m_ClampEvents++; }
            }
            else
            {
                float raw = ax.angle + a[GroupCount + i] * armRotationSpeed * dt;
                if (raw < lim.x || raw > lim.y) m_LimitEvents++;
                target = Mathf.Clamp(raw, lim.x, lim.y);
                ApplyArmAngleWithContactClamp(ax, target);
                if (!Mathf.Approximately(ax.angle, target)) m_ClampEvents++;
            }
        }

        // Finger groups: impedance on the equilibrium setpoint; masked groups hold the neutral pose.
        // Integration: exact one-step transition of the linear spring-damper (closed-form matrix per (k, b, I), see
        // TransitionMatrix); the penetration clamp realizes the target exactly as before; a stopped joint loses its velocity (inelastic).
        for (int g = 0; g < GroupCount; g++)
        {
            var grp = m_Groups[g];
            if (grp.masked) continue;
            var lim = GetLimits(g);
            if (m_Morph != null)
            {
                float sp = grp.setpoint + a[g] * setpointRateDegPerSec * dt;
                if (sp < lim.x || sp > lim.y) { m_LimitEvents++; sp = Mathf.Clamp(sp, lim.x, lim.y); }
                grp.setpoint = sp;
                // exact one-step transition of the linear spring-damper about the setpoint
                float x = grp.angle - grp.setpoint, v = grp.vel; var P = m_Phi[g];
                float nx = P.x * x + P.y * v; grp.vel = P.z * x + P.w * v;
                float target = grp.setpoint + nx;
                if (target < lim.x || target > lim.y) { target = Mathf.Clamp(target, lim.x, lim.y); grp.vel = 0f; }
                ApplyAngleWithContactClamp(grp, target);
                if (!Mathf.Approximately(grp.angle, target)) { grp.vel = 0f; m_ClampEvents++; }
            }
            else
            {   // no MorphologyManager on the object: legacy velocity actuation
                float target = Mathf.Clamp(grp.angle + a[g] * rotationSpeed * dt, lim.x, lim.y);
                ApplyAngleWithContactClamp(grp, target);
            }
        }
        m_EpisodeClampEvents += m_ClampEvents; m_EpisodeLimitEvents += m_LimitEvents;

        // ---- Reward ----
        // 1) Shaping: 15 potentials (14 segments + grasp point), each normalized by its episode-initial distance,
        //    summed and scaled by shapingScale so the episode budget is at most ~1.0.
        float shapingSum = 0f;
        int contacts = 0;
        bool thumbTouching = false;
        int fingerMask = 0;
        float residual = 0f;
        m_Contacts.Clear();
        foreach (var col in segmentColliders)
        {
            float currDist = SegmentDistance(col);
            float delta = previousDistances[col] - currDist;
            shapingSum += distanceRewardScale * delta / initialDistances[col];
            previousDistances[col] = currDist;
            residual += currDist;

            if (TryGetContact(col, out ContactSample sample))
            {
                contacts++;
                int finger = segmentFinger[col];
                sample.isThumb = finger == k_ThumbFinger;
                if (sample.isThumb) thumbTouching = true;
                else fingerMask |= 1 << finger;
                m_Contacts.Add(sample);
            }
        }
        CurrentContacts = contacts;
        ResidualSegmentDistance = residual;

        // Palm contact: quality only, never part of the success gate
        bool palmTouching = false;
        if (m_PalmCollider != null && TryGetContact(m_PalmCollider, out ContactSample palmSample))
        {
            palmTouching = true;
            palmSample.isPalm = true;
            m_Contacts.Add(palmSample);
        }

        float graspDist = GraspPointDistance;
        shapingSum += palmDistanceRewardScale * (previousGraspPointDistance - graspDist) / initialGraspPointDistance;
        previousGraspPointDistance = graspDist;
        if (graspDist < m_MinGraspPointDistance) m_MinGraspPointDistance = graspDist;

        float shaping = shapingSum * shapingScale;
        float penalty = MaxStep > 0 ? -existentialPenaltyScale / MaxStep : 0f;
        m_ShapingReturn += shaping;
        m_PenaltyReturn += penalty;
        // Effort (placeholder for sEMG cost) and safety (joint-limit saturation events); clamp events are logged only
        float effort = -effortWeight * sumA2;
        float safety = -safetyWeight * m_LimitEvents;
        m_EffortReturn += effort; m_SafetyReturn += safety;
        AddReward(shaping + penalty + effort + safety);

        // 2) Hold criterion (unchanged): >= N segments, >= M distinct fingers, thumb touching
        int distinctFingers = CountBits(fingerMask);
        bool grasp = contacts >= requiredContactSegments
                  && distinctFingers >= requiredDistinctFingers
                  && (!requireThumbContact || thumbTouching);
        if (contacts >= requiredContactSegments && m_StepsToFirstSixContacts < 0) m_StepsToFirstSixContacts = StepCount;
        if (grasp && m_StepsToFirstHoldCriterion < 0) m_StepsToFirstHoldCriterion = StepCount;

        // 3) Quality Q (computed every step for diagnostics; paid only on held steps, capped per episode)
        float q = ComputeQuality(contacts, thumbTouching, palmTouching);
        LastQuality = q; LastPalmTouching = palmTouching; LastDistinctFingers = distinctFingers; LastThumbTouching = thumbTouching; LastHoldCriterionMet = grasp;
        if (grasp)
        {
            if (m_QualityStepsPaid < qualityBudgetSteps)
            {
                float pay = q * qualityPayPerStep;
                AddReward(pay);
                m_QualityReturn += pay;
                m_QualityStepsPaid++;
            }
            m_HoldWindowCount++; m_HoldWindowSum += contacts; m_HoldWindowSumSq += (double)contacts * contacts;
        }
        else
        {
            m_HoldWindowCount = 0; m_HoldWindowSum = m_HoldWindowSumSq = 0;
        }

        // 4) Success: grasp held for K consecutive decisions (unchanged)
        m_HoldSteps = grasp ? m_HoldSteps + 1 : 0;
        if (grasp && m_HoldSteps >= (HoldDecisions > 0 ? HoldDecisions : requiredHoldDecisions) * m_DecisionPeriod)
        {
            SuccessCount++;
            AddReward(successBonus);
            m_BonusReturn += successBonus;
            LogEpisode(true);
            EndEpisode();
        }
    }

    // ---- grasp quality ----

    /// <summary>Q in [0,1] from the contact samples gathered this step. Weights are serialized and sum to 1 by default.</summary>
    private float ComputeQuality(int contacts, bool thumbTouching, bool palmTouching)
    {
        float contactScore = qualityContactSaturation > 0 ? Mathf.Clamp01((float)Mathf.Min(contacts, qualityContactSaturation) / qualityContactSaturation) : 0f;

        // Azimuthal coverage: largest circular gap between contact azimuths (segments + palm)
        m_Azimuths.Clear();
        foreach (var c in m_Contacts) m_Azimuths.Add(c.azimuthDeg);
        float largestGap = 360f;
        if (m_Azimuths.Count >= 2)
        {
            m_Azimuths.Sort();
            largestGap = 0f;
            for (int i = 1; i < m_Azimuths.Count; i++) largestGap = Mathf.Max(largestGap, m_Azimuths[i] - m_Azimuths[i - 1]);
            largestGap = Mathf.Max(largestGap, 360f - (m_Azimuths[m_Azimuths.Count - 1] - m_Azimuths[0]));
        }
        float coverage = qualityCoverageRampDeg > 0f ? Mathf.Clamp01((qualityCoverageRampStartDeg - largestGap) / qualityCoverageRampDeg) : 0f;

        // Thumb antipodality: circular mean of thumb contacts vs circular mean of finger contacts, peak at 180 deg
        float antipodal = 0f;
        if (thumbTouching)
        {
            float ts = 0f, tc = 0f, fs = 0f, fc = 0f; int fingerCount = 0;
            foreach (var c in m_Contacts)
            {
                if (c.isPalm) continue;
                float r = c.azimuthDeg * Mathf.Deg2Rad;
                if (c.isThumb) { ts += Mathf.Sin(r); tc += Mathf.Cos(r); }
                else { fs += Mathf.Sin(r); fc += Mathf.Cos(r); fingerCount++; }
            }
            if (fingerCount > 0)
            {
                float thumbAz = Mathf.Atan2(ts, tc) * Mathf.Rad2Deg, fingerAz = Mathf.Atan2(fs, fc) * Mathf.Rad2Deg;
                float diff = Mathf.Abs(Mathf.DeltaAngle(thumbAz, fingerAz));   // [0, 180]
                antipodal = Mathf.Clamp01(diff / 180f);
            }
        }

        // Vertical spread of contacts along the cylinder axis (segments + palm)
        float minH = float.MaxValue, maxH = float.MinValue;
        foreach (var c in m_Contacts) { if (c.height < minH) minH = c.height; if (c.height > maxH) maxH = c.height; }
        LastVerticalSpread = m_Contacts.Count > 0 ? maxH - minH : 0f;
        LastCoverageGapDeg = largestGap;
        LastAntipodality = antipodal;

        // Wedge posture: palm-low / fingertips-high spread, paid only while the palm touches (the spread means something
        // else without a palm contact). Ramps from qualityWedgeSpreadStart to qualityWedgeSpreadStart + qualityWedgeSpreadRamp.
        // Opposition gate: the self-tightening mechanism requires opposed contacts. A one-sided contact set can relieve all of
        // its depenetration by lateral translation, so spread without opposition earns nothing: the credit is multiplied by
        // clamp(antipodality / qualityWedgeOppositionThreshold, 0, 1), saturating at the threshold.
        // Thresholds scale with hand size (handSpan / reference span) so the wedge term is comparable across morphologies
        float wedgeStart = qualityWedgeSpreadStart * m_HandSpanRatio, wedgeRamp = qualityWedgeSpreadRamp * m_HandSpanRatio;
        float wedgeCredit = (palmTouching && wedgeRamp > 0f) ? Mathf.Clamp01((LastVerticalSpread - wedgeStart) / wedgeRamp) : 0f;
        float oppositionGate = qualityWedgeOppositionThreshold > 0f ? Mathf.Clamp01(antipodal / qualityWedgeOppositionThreshold) : 1f;
        float wedge = wedgeCredit * oppositionGate;
        LastWedge = wedge;

        return Mathf.Clamp01(qualityContactWeight * contactScore
                           + qualityCoverageWeight * coverage
                           + qualityAntipodalWeight * antipodal
                           + qualityPalmWeight * (palmTouching ? 1f : 0f)
                           + qualityWedgeWeight * wedge);
    }

    // ---- episode stats ----

    private void LogEpisode(bool success)
    {
        m_EpisodeActive = false;
        float holdStd = 0f;
        if (m_HoldWindowCount > 1)
        {
            double mean = m_HoldWindowSum / m_HoldWindowCount;
            holdStd = (float)System.Math.Sqrt(System.Math.Max(0.0, m_HoldWindowSumSq / m_HoldWindowCount - mean * mean));
        }
        float yaw = cylinderTransform != null ? cylinderTransform.eulerAngles.y : 0f;
        var rec = Academy.Instance.StatsRecorder;
        rec.Add("Grasp/Success", success ? 1f : 0f);
        rec.Add("Grasp/StepsToFirstSixContacts", m_StepsToFirstSixContacts);
        rec.Add("Grasp/StepsToSuccess", success ? StepCount : -1);
        rec.Add("Grasp/ContactsAtEnd", CurrentContacts);
        rec.Add("Grasp/DistinctFingersAtEnd", LastDistinctFingers);
        rec.Add("Grasp/ThumbAtEnd", LastThumbTouching ? 1f : 0f);
        rec.Add("Grasp/PalmAtEnd", LastPalmTouching ? 1f : 0f);
        rec.Add("Grasp/CoverageGapDeg", LastCoverageGapDeg);
        rec.Add("Grasp/Antipodality", LastAntipodality);
        rec.Add("Grasp/VerticalSpread", LastVerticalSpread);
        rec.Add("Grasp/WedgeAtEnd", LastWedge);
        rec.Add("Grasp/HoldContactStd", holdStd);
        rec.Add("Grasp/QualityAtEnd", LastQuality);
        rec.Add("Grasp/MinGraspPointDistance", m_MinGraspPointDistance);
        rec.Add("Grasp/FinalGraspPointDistance", previousGraspPointDistance);
        rec.Add("Grasp/ResidualSegmentDistance", ResidualSegmentDistance);
        rec.Add("Grasp/MaxPenetration", m_MaxPenetration);
        rec.Add("Grasp/SpawnDistance", m_SpawnDistance);
        rec.Add("Grasp/CylinderYaw", yaw);
        rec.Add("Return/Shaping", m_ShapingReturn);
        rec.Add("Return/Quality", m_QualityReturn);
        rec.Add("Return/Bonus", m_BonusReturn);
        rec.Add("Return/Penalty", m_PenaltyReturn);
        rec.Add("Return/QualityStepsPaid", m_QualityStepsPaid);
        rec.Add("Return/Effort", m_EffortReturn);
        rec.Add("Return/Safety", m_SafetyReturn);
        rec.Add("Grasp/ClampEvents", m_EpisodeClampEvents);
        rec.Add("Grasp/LimitEvents", m_EpisodeLimitEvents);
        if (m_Morph != null)
        {
            float lenMean = 0f; for (int f = 0; f < MorphologyManager.FingerCount; f++) lenMean += m_Morph.lengthScale[f] / MorphologyManager.FingerCount;
            float wMean = 0f; for (int g = 0; g < GroupCount; g++) wMean += m_Morph.NaturalFrequency(g) / GroupCount;
            rec.Add("Morph/LengthScaleMean", lenMean);
            rec.Add("Morph/OmegaMean", wMean);
            rec.Add("Morph/ActiveGroups", m_Morph.ActiveCount);
            rec.Add("Morph/HandSpanRatio", m_HandSpanRatio);
            rec.Add("Morph/ProjectionEvents", m_Morph.ProjectionEvents);
        }

        if (!string.IsNullOrEmpty(statsCsvPath))
        {
            try
            {
                if (!System.IO.File.Exists(statsCsvPath))
                    System.IO.File.WriteAllText(statsCsvPath, "episode,success,steps,stepsToFirstSixContacts,stepsToHoldCriterion,contacts,distinctFingers,thumb,palm,coverageGapDeg,antipodality,verticalSpread,holdContactStd,qualityAtEnd,minGraspDist,finalGraspDist,residualSegDist,maxPenetration,spawnDistance,cylYaw,retShaping,retQuality,retBonus,retPenalty,qualityStepsPaid\n");
                System.IO.File.AppendAllText(statsCsvPath, string.Join(",", new string[] {
                    CompletedEpisodes.ToString(), success ? "1" : "0", StepCount.ToString(), m_StepsToFirstSixContacts.ToString(), m_StepsToFirstHoldCriterion.ToString(),
                    CurrentContacts.ToString(), LastDistinctFingers.ToString(), LastThumbTouching ? "1" : "0", LastPalmTouching ? "1" : "0",
                    LastCoverageGapDeg.ToString("F1"), LastAntipodality.ToString("F3"), LastVerticalSpread.ToString("F4"), holdStd.ToString("F3"), LastQuality.ToString("F4"),
                    m_MinGraspPointDistance.ToString("F4"), previousGraspPointDistance.ToString("F4"), ResidualSegmentDistance.ToString("F4"), m_MaxPenetration.ToString("F5"),
                    m_SpawnDistance.ToString("F3"), yaw.ToString("F0"), m_ShapingReturn.ToString("F4"), m_QualityReturn.ToString("F4"), m_BonusReturn.ToString("F2"), m_PenaltyReturn.ToString("F4"), m_QualityStepsPaid.ToString() }) + "\n");
            }
            catch (System.Exception e) { Debug.LogWarning("[ArmGraspAgent] stats CSV: " + e.Message); }
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

    // Touch test with the same acceptance as before (overlap, or closest-point gap <= contactDistance), now also
    // returning the contact geometry: azimuth about the cylinder axis from the outward contact normal
    // (ComputePenetration direction when overlapping, closest-point pair otherwise) and the height along the axis.
    private bool TryGetContact(Collider col, out ContactSample sample)
    {
        sample = default;
        if (cylinderCollider == null) return false;
        Vector3 normal;
        Vector3 onCylinder;
        if (Physics.ComputePenetration(col, col.transform.position, col.transform.rotation,
                cylinderCollider, cylinderTransform.position, cylinderTransform.rotation, out Vector3 dir, out float depth))
        {
            if (depth > m_MaxPenetration) m_MaxPenetration = depth;
            normal = dir;                                                   // direction that separates col from the cylinder = outward normal
            onCylinder = cylinderCollider.ClosestPoint(col.transform.position);
        }
        else
        {
            Vector3 p0 = cylinderCollider.ClosestPoint(col.transform.position);
            Vector3 onSegment = col.ClosestPoint(p0);
            if (Vector3.Distance(p0, onSegment) > contactDistance) return false;   // unchanged acceptance test
            onCylinder = cylinderCollider.ClosestPoint(onSegment);              // refine the cylinder-side point
            normal = onSegment - onCylinder;
        }
        Vector3 radial = normal;
        radial -= Vector3.Dot(radial, cylinderTransform.up) * cylinderTransform.up;   // project onto the plane normal to the axis
        if (radial.sqrMagnitude < 1e-10f)
        {
            radial = onCylinder - cylinderTransform.position;
            radial -= Vector3.Dot(radial, cylinderTransform.up) * cylinderTransform.up;
        }
        Vector3 local = cylinderTransform.InverseTransformDirection(radial);
        float az = Mathf.Atan2(local.z, local.x) * Mathf.Rad2Deg;
        if (az < 0f) az += 360f;
        sample.azimuthDeg = az;
        sample.height = Vector3.Dot(onCylinder - cylinderTransform.position, cylinderTransform.up);
        return true;
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
