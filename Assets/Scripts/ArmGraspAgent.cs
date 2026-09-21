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
    [Tooltip("Reference-hand offset (m, palm rotation frame: x = palm normal / closing direction, y = along the fingers, z = across the palm) from the palm pivot to the centre of the region the closed fingers enclose; used for reach shaping and spawn sampling. Robust centre of the forced-close placement grid (2026-09-14): x = 0.14, y = 0.215.")]
    public Vector3 graspPointOffset = new Vector3(0.14f, 0.215f, 0.078f);
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

    [Header("Lift Task (run 011: the object is released at the gate, must be lifted clear of the pedestal and held under perturbation)")]
    [Tooltip("Name of the pedestal GameObject; the top of its collider defines the resting height and the lift threshold.")]
    public string platformName = "Platform";
    [Tooltip("Spawn the object resting on the pedestal (bottom = platform top + restClearance); spawnHeightRange is ignored while set.")]
    public bool restOnPlatform = true;
    public float restClearance = 0.001f;
    [Tooltip("Lifted = the object's world AABB bottom is at least this (m) above the platform top (defeats edge and tilted resting).")]
    public float liftClearance = 0.08f;
    [Tooltip("One-time reward when the gate first fires (phase 1 -> 2). Not the bonus.")]
    public float phaseReward = 0.1f;
    [Tooltip("Steps after the transition within which the object must first count as lifted; expiry ends the episode with the drop penalty.")]
    public int liftBudgetSteps = 200;
    [Tooltip("Steps after the transition within which the hold must complete; expiry ends the episode with the drop penalty.")]
    public int taskBudgetSteps = 350;
    [Tooltip("Reward per lifted (phase 3) step, paid on at most holdRewardBudgetSteps steps per episode (anti-farming cap, run-008 pattern).")]
    public float holdRewardPerStep = 0.004f;
    public int holdRewardBudgetSteps = 50;
    [Tooltip("Penalty (positive, subtracted) when the object is dropped after the transition or a budget expires; the episode ends.")]
    public float dropPenalty = 0.2f;
    [Tooltip("Drop = object centre farther than this (m) from the grasp point, or object bottom below platform top - dropBelowPlatform.")]
    public float dropDistance = 0.3f;
    public float dropBelowPlatform = 0.02f;
    [Tooltip("Object mass (kg) drawn log-uniform in [x, y] per episode; env params mass/min and mass/max override the bounds.")]
    public Vector2 massRange = new Vector2(0.2f, 1.5f);
    [Tooltip("Perturbation pulses per hold window (phase 3), each lasting perturbPulseSteps physics steps from a random start step.")]
    public int perturbPulses = 3;
    public int perturbPulseSteps = 5;
    [Tooltip("Peak horizontal force = perturbScale x this x m g; each pulse draws its magnitude uniformly in [0.5, 1] x peak.")]
    public float perturbPeakWeightRatio = 1.5f;
    [Tooltip("Pulse torque magnitude = force magnitude x this lever (m), about a random axis.")]
    public float perturbTorqueLever = 0.15f;
    [Tooltip("Perturbation scale in [0, 1]; the env param perturb/scale overrides it (curriculum ramp).")]
    public float perturbScale = 1f;
    [Tooltip("Diagnostics only: >= 0 overrides the perturbation scale (field and env param).")]
    public float perturbScaleOverride = -1f;
    [Tooltip("Friction coefficient given to the object's collider (static = dynamic, Maximum combine); <= 0 leaves the scene material. The drop-test standard's primary value is 1.0.")]
    public float objectFriction = 1.0f;
    [Tooltip("Impedance grip model: while the object is dynamic, a finger group whose spring is stalled at the object's surface presses on it with its spring torque k (setpoint - angle) divided by the pivot-to-contact lever, split over the group's touching segments; the set of contact forces is projected to zero net wrench (fingers squeeze, they cannot accelerate the object) and the inward component of each is the normal preload N of that contact. Friction is then explicit: a Coulomb impulse against the relative tangential velocity (finger surface velocity from its pose change), capacity mu N, with torsional friction mu N x patch radius. PhysX's own friction on hand contacts is zeroed (PhysX has no preload: balanced squeeze forces produce no contact impulse). 0 disables friction entirely.")]
    public float gripForceScale = 1f;
    [Tooltip("Cap on the impedance contact force per touching segment (N).")]
    public float gripForceMax = 60f;
    [Tooltip("Contact patch radius (m) for torsional friction at a squeezing contact (torque capacity = mu N x this).")]
    public float gripPatchRadius = 0.008f;
    [Tooltip("Compliant finger: how far (deg) a group may open per step toward the contact angle when the dynamic object has moved into it.")]
    public float yieldRangeDeg = 10f;
    [Tooltip("Drop criterion: the object's axis has tilted more than this (deg) from its orientation at the transition (loss of orientation control); 180 disables.")]
    public float dropTiltDeg = 60f;

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
    /// <summary>
    /// graspPointOffset for the current morphology. Palm frame: x = palm normal (the closing direction), y = along the
    /// fingers, z = across the palm. Only x scales, by the finger-length ratio (sum of link lengths / reference sum): the
    /// closing radius follows the finger length, while the finger-base pivots and the palm do not move with the link
    /// scales, so the along-finger (y) and across-palm (z) components stay fixed. (The wedge thresholds, which measure
    /// finger spread, keep the hand-span scaling.)
    /// </summary>
    public Vector3 EffectiveGraspPointOffset => new Vector3(graspPointOffset.x * m_FingerLengthRatio, graspPointOffset.y, graspPointOffset.z);
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
        public bool clamped;                // this step the penetration clamp stopped the spring short of its target
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
    float m_HandSpanRatio = 1f, m_FingerLengthRatio = 1f;
    int m_ClampEvents, m_LimitEvents; float m_EffortReturn, m_SafetyReturn; int m_EpisodeClampEvents, m_EpisodeLimitEvents;
    public float EffortReturn => m_EffortReturn;
    public float SafetyReturn => m_SafetyReturn;
    public int EpisodeClampEvents => m_EpisodeClampEvents;
    public int EpisodeLimitEvents => m_EpisodeLimitEvents;
    public float HandSpanRatio => m_HandSpanRatio;
    public float FingerLengthRatio => m_FingerLengthRatio;
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
    struct ContactSample { public float azimuthDeg; public float height; public bool isThumb; public bool isPalm; public Vector3 point, normal; }
    private List<ContactSample> m_Contacts = new List<ContactSample>();
    private List<float> m_Azimuths = new List<float>();

    // Per-episode bookkeeping (reset in OnEpisodeBegin, reported at episode end)
    private float m_ShapingReturn, m_PenaltyReturn, m_BonusReturn;
    private int m_StepsToFirstSixContacts = -1, m_StepsToFirstHoldCriterion = -1;
    private float m_MinGraspPointDistance, m_MaxPenetration, m_SpawnDistance;
    private int m_HoldWindowCount; private double m_HoldWindowSum, m_HoldWindowSumSq;
    private bool m_EpisodeActive;

    // ---- run 011 lift task ----
    public enum TaskPhase { Reach = 0, Lift = 1, Hold = 2 }
    TaskPhase m_Phase;
    Rigidbody m_CylRb; RigidbodyConstraints m_CylConstraints0; RigidbodyInterpolation m_CylInterp0; float m_CylHalfHeight = 0.388f;
    Collider m_ForearmCollider; float m_PlatformTop; bool m_ObjectDynamic;
    float m_Mass = 1f, m_MassObs, m_PerturbScale = 1f;
    int m_TransitionStep = -1, m_StepsToLift = -1, m_HoldStepsPaid, m_HoldEntries, m_PulsesApplied;
    float m_PhaseReturn, m_HoldReturn, m_DropReturn, m_MaxPulseForce; string m_EndReason = "";
    int[] m_PulseStart = new int[0]; Vector3[] m_PulseForce = new Vector3[0], m_PulseTorque = new Vector3[0];
    float m_GripForce, m_GripForceSum; int m_GripForceSteps; PhysicsMaterial m_ObjectMaterial;
    struct GripContact { public Vector3 p, f, n; public Collider col; }
    readonly List<GripContact> m_Grip = new List<GripContact>();
    readonly Dictionary<Collider, (Vector3 pos, Quaternion rot)> m_PrevPose = new Dictionary<Collider, (Vector3, Quaternion)>();
    float m_FrictionForce, m_FrictionSum; Vector3 m_ObjectUp0;
    public float FrictionForce => m_FrictionForce;   // sum of the explicit friction forces applied this step (N)
    public Vector3 NetFrictionForce { get; private set; }   // vector sum of the friction forces this step (N)
    public Vector3 NetSqueezeForce { get; private set; }    // vector sum of the projected squeeze forces this step (N, should be ~0)
    public float GripForce => m_GripForce;   // sum of the impedance contact forces applied this step (N)

    // Contact statistics (diagnostics only since run 011; nothing here enters the reward)
    public float LastCoverageGapDeg { get; private set; }
    public float LastAntipodality { get; private set; }
    public float LastVerticalSpread { get; private set; }
    public bool LastPalmTouching { get; private set; }
    public bool LastForearmTouching { get; private set; }
    public int LastDistinctFingers { get; private set; }
    public bool LastThumbTouching { get; private set; }
    public bool LastHoldCriterionMet { get; private set; }
    public float ShapingReturn => m_ShapingReturn;
    public float PenaltyReturn => m_PenaltyReturn;
    public float BonusReturn => m_BonusReturn;
    public float PhaseReturn => m_PhaseReturn;
    public float HoldReturn => m_HoldReturn;
    public float DropReturn => m_DropReturn;
    public int HoldStepsPaid => m_HoldStepsPaid;
    public int HoldSteps => m_HoldSteps;
    public TaskPhase Phase => m_Phase;
    public bool ObjectDynamic => m_ObjectDynamic;
    public float ObjectMass => m_Mass;
    public float PerturbScaleInEffect => m_PerturbScale;
    public float PlatformTop => m_PlatformTop;
    public string EndReason => m_EndReason;
    /// <summary>Snapshot of the last completed episode (filled in LogEpisode; survives the reset, for harnesses).</summary>
    public EpisodeRecord LastEpisode;
    [System.Serializable]
    public struct EpisodeRecord
    {
        public bool success; public string endReason; public int steps, transitionStep, stepsToLift, holdSteps, holdEntries, contacts, distinctFingers, pulsesApplied, holdStepsPaid;
        public bool thumb, palm, forearm; public float mass, perturbScale, maxPulseForce, retShaping, retPhase, retHold, retBonus, retDrop, retPenalty, retEffort, bottomAboveTop, coverageGapDeg, antipodality, verticalSpread, gripForceMean, gripForceEnd;
    }
    public bool Lifted => IsLifted();
    // run-010 compatibility: Q was removed from the task in run 011; BoEvalHarness (the 010 evaluation endpoint) still reads these
    public float LastQuality => 0f;
    public float LastWedge => 0f;
    public int QualityStepsPaid => 0;
    public float QualityReturn => 0f;
    [System.NonSerialized] public float qualityPayPerStep = 0f;
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
            m_CylRb = cylObj.GetComponent<Rigidbody>();
            if (m_CylRb != null) { m_CylConstraints0 = m_CylRb.constraints; m_CylInterp0 = m_CylRb.interpolation; }
            if (cylinderCollider != null) m_CylHalfHeight = cylinderTransform.position.y - cylinderCollider.bounds.min.y;
            if (cylinderCollider != null && objectFriction > 0f)
            {
                m_ObjectMaterial = new PhysicsMaterial("ObjectMu") { staticFriction = objectFriction, dynamicFriction = objectFriction, frictionCombine = PhysicsMaterialCombine.Average, bounciness = 0f, bounceCombine = PhysicsMaterialCombine.Minimum };
                cylinderCollider.sharedMaterial = m_ObjectMaterial;
            }
        }
        // Pedestal: its collider top is the resting height and the reference for the lift threshold
        var plat = !string.IsNullOrEmpty(platformName) ? GameObject.Find(platformName) : null;
        if (plat != null && plat.TryGetComponent<Collider>(out var platCol)) m_PlatformTop = platCol.bounds.max.y;
        else { m_PlatformTop = spawnCenter.y - m_CylHalfHeight; Debug.LogWarning("[ArmGraspAgent] platform '" + platformName + "' not found: platform top taken as the spawn-centre bottom " + m_PlatformTop.ToString("F3")); }

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
        if (m_Forearm != null && m_Forearm.TryGetComponent<Collider>(out var foreCol)) { armColliders.Add(foreCol); m_ForearmCollider = foreCol; }
        if (m_Palm != null && m_Palm.TryGetComponent<Collider>(out var palmCol)) armColliders.Add(palmCol);

        var allHand = new List<Collider>(armColliders); allHand.AddRange(segmentColliders);
        if (objectFriction > 0f)
        {   // hand-object friction is explicit (ApplyGripForces); PhysX's own friction on the hand is zeroed (Multiply combine with 0
            // takes precedence over the object's Average) so the two never act on the same contact
            var handMat = new PhysicsMaterial("HandNoFriction") { staticFriction = 0f, dynamicFriction = 0f, frictionCombine = PhysicsMaterialCombine.Multiply, bounciness = 0f, bounceCombine = PhysicsMaterialCombine.Minimum };
            foreach (var c in allHand) c.sharedMaterial = handMat;
        }
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
        if (m_EpisodeActive) { if (string.IsNullOrEmpty(m_EndReason)) m_EndReason = "maxStep"; LogEpisode(false); }

        // Lift task: object back to its supported (kinematic) state; mass and perturbation scale for this episode
        var epp = Academy.Instance.EnvironmentParameters;
        m_Phase = TaskPhase.Reach; m_ObjectDynamic = false; m_TransitionStep = -1; m_StepsToLift = -1; m_HoldStepsPaid = 0; m_HoldEntries = 0; m_PulsesApplied = 0;
        m_PhaseReturn = m_HoldReturn = m_DropReturn = 0f; m_MaxPulseForce = 0f; m_EndReason = ""; LastForearmTouching = false;
        m_GripForce = m_GripForceSum = 0f; m_GripForceSteps = 0; m_FrictionForce = m_FrictionSum = 0f;
        m_PulseStart = new int[0];
        float mMin = Mathf.Max(1e-3f, epp.GetWithDefault("mass/min", massRange.x)), mMax = Mathf.Max(mMin, epp.GetWithDefault("mass/max", massRange.y));
        m_Mass = Mathf.Exp(Random.Range(Mathf.Log(mMin), Mathf.Log(mMax)));
        m_MassObs = massRange.y > massRange.x ? Mathf.Clamp01(Mathf.Log(m_Mass / massRange.x) / Mathf.Log(massRange.y / massRange.x)) : 0f;
        m_PerturbScale = perturbScaleOverride >= 0f ? perturbScaleOverride : Mathf.Clamp01(epp.GetWithDefault("perturb/scale", perturbScale));
        if (m_CylRb != null)
        {
            m_CylRb.isKinematic = true; m_CylRb.useGravity = false; m_CylRb.constraints = m_CylConstraints0; m_CylRb.interpolation = m_CylInterp0; m_CylRb.mass = m_Mass;
        }

        // Morphology for this episode: link scales, spring parameters, actuation mask; masked groups hold the neutral pose
        m_HandSpanRatio = 1f; m_FingerLengthRatio = 1f;
        if (m_Morph != null)
        {
            m_Morph.ApplyForEpisode();
            m_HandSpanRatio = wedgeHandSpanRef > 0f ? m_Morph.HandSpan / wedgeHandSpanRef : 1f;
            m_FingerLengthRatio = m_Morph.FingerLengthRatio;
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
        StorePrevPoses();

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
        m_ShapingReturn = m_PenaltyReturn = m_BonusReturn = 0f;
        m_StepsToFirstSixContacts = m_StepsToFirstHoldCriterion = -1;
        m_MinGraspPointDistance = previousGraspPointDistance;
        m_MaxPenetration = 0f;
        m_SpawnDistance = (m_Shoulder != null && cylinderTransform != null) ? Vector3.Distance(m_Shoulder.position, cylinderTransform.position) : 0f;
        m_HoldWindowCount = 0; m_HoldWindowSum = m_HoldWindowSumSq = 0;
        LastCoverageGapDeg = 360f; LastAntipodality = 0f; LastVerticalSpread = 0f; LastPalmTouching = false; LastDistinctFingers = 0; LastThumbTouching = false; LastHoldCriterionMet = false;
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
        float restY = restOnPlatform ? m_PlatformTop + m_CylHalfHeight + restClearance : spawnCenter.y;
        Vector3 chosen = new Vector3(spawnCenter.x, restY, spawnCenter.z);
        Quaternion chosenRot = cylinderTransform.rotation;
        Quaternion baseRot = Quaternion.Euler(cylinderTransform.eulerAngles.x, 0f, cylinderTransform.eulerAngles.z);
        for (int attempt = 0; attempt < Mathf.Max(1, spawnAttempts); attempt++)
        {
            Vector2 disk = Random.insideUnitCircle * radius;
            Vector3 candidate = new Vector3(spawnCenter.x + disk.x, restOnPlatform ? restY : spawnCenter.y + Random.Range(0f, spawnHeightRange), spawnCenter.z + disk.y);
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

    // Fixed vector observation (24): palm-frame target 3, task progress 5, arm axes sin/cos 10, morphology summary 6.
    // Per-joint tokens (BufferSensor, up to 16 x 13): one per ACTIVE finger group plus the two wrist axes.
    public override void CollectObservations(VectorSensor sensor)
    {
        // 1) Cylinder position relative to the palm, in the palm's rotation frame - 3
        Vector3 rel = (m_Palm != null && cylinderTransform != null)
            ? m_Palm.InverseTransformDirection(cylinderTransform.position - m_Palm.position) / Mathf.Max(targetObsScale, 1e-4f)
            : Vector3.zero;
        sensor.AddObservation(Vector3.ClampMagnitude(rel, 1.5f));

        // 2) Task progress - 5: lifted-hold fraction, contacts / 14, hold-reward budget spent, task phase (0 / 0.5 / 1), object mass (log-normalized)
        int holdNeeded = HoldStepsNeeded();
        sensor.AddObservation(Mathf.Clamp01((float)m_HoldSteps / holdNeeded));
        sensor.AddObservation(CurrentContacts / (float)GroupCount);
        sensor.AddObservation(holdRewardBudgetSteps > 0 ? Mathf.Clamp01((float)m_HoldStepsPaid / holdRewardBudgetSteps) : 0f);
        sensor.AddObservation(0.5f * (int)m_Phase);
        sensor.AddObservation(m_MassObs);

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
                grp.clamped = !Mathf.Approximately(grp.angle, target);
                if (grp.clamped) { grp.vel = 0f; m_ClampEvents++; }
            }
            else
            {   // no MorphologyManager on the object: legacy velocity actuation
                float target = Mathf.Clamp(grp.angle + a[g] * rotationSpeed * dt, lim.x, lim.y);
                ApplyAngleWithContactClamp(grp, target);
            }
        }
        m_EpisodeClampEvents += m_ClampEvents; m_EpisodeLimitEvents += m_LimitEvents;
        if (m_ObjectDynamic) ApplyGripForces();
        StorePrevPoses();

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

        // 2) Gate (unchanged): >= N segments, >= M distinct fingers, thumb touching. Since run 011 it is the phase 1 -> 2 transition.
        int distinctFingers = CountBits(fingerMask);
        bool grasp = contacts >= requiredContactSegments
                  && distinctFingers >= requiredDistinctFingers
                  && (!requireThumbContact || thumbTouching);
        if (contacts >= requiredContactSegments && m_StepsToFirstSixContacts < 0) m_StepsToFirstSixContacts = StepCount;
        if (grasp && m_StepsToFirstHoldCriterion < 0) m_StepsToFirstHoldCriterion = StepCount;

        // 3) Contact statistics (diagnostics only; nothing here is rewarded)
        ComputeContactStats(thumbTouching);
        LastPalmTouching = palmTouching; LastDistinctFingers = distinctFingers; LastThumbTouching = thumbTouching; LastHoldCriterionMet = grasp;
        LastForearmTouching = m_ForearmCollider != null && TryGetContact(m_ForearmCollider, out _);
        if (grasp) { m_HoldWindowCount++; m_HoldWindowSum += contacts; m_HoldWindowSumSq += (double)contacts * contacts; }
        else { m_HoldWindowCount = 0; m_HoldWindowSum = m_HoldWindowSumSq = 0; }

        // 4) Phase machine: Reach -(gate)-> Lift -(object clear of the pedestal)-> Hold -(K decisions under perturbation)-> success
        int holdNeeded = HoldStepsNeeded();
        if (m_Phase == TaskPhase.Reach)
        {
            if (grasp)
            {
                m_Phase = TaskPhase.Lift; m_TransitionStep = StepCount;
                AddReward(phaseReward); m_PhaseReturn += phaseReward;
                ReleaseObject();
            }
        }
        else
        {
            int sinceTransition = StepCount - m_TransitionStep;
            bool lifted = IsLifted();
            if (lifted && m_StepsToLift < 0) m_StepsToLift = StepCount;
            if (IsDropped()) { FailEpisode("drop"); return; }
            if (!lifted && m_StepsToLift < 0 && sinceTransition >= liftBudgetSteps) { FailEpisode("liftBudget"); return; }
            if (sinceTransition >= taskBudgetSteps) { FailEpisode("taskBudget"); return; }
            if (lifted)
            {
                if (m_Phase == TaskPhase.Lift) { m_Phase = TaskPhase.Hold; m_HoldSteps = 0; m_HoldEntries++; SchedulePerturbations(holdNeeded); }
                m_HoldSteps++;
                if (m_HoldStepsPaid < holdRewardBudgetSteps) { AddReward(holdRewardPerStep); m_HoldReturn += holdRewardPerStep; m_HoldStepsPaid++; }
                ApplyPerturbation();
                if (m_HoldSteps >= holdNeeded)
                {
                    SuccessCount++;
                    AddReward(successBonus);
                    m_BonusReturn += successBonus;
                    m_EndReason = "success";
                    LogEpisode(true);
                    EndEpisode();
                    return;
                }
            }
            else if (m_Phase == TaskPhase.Hold) { m_Phase = TaskPhase.Lift; m_HoldSteps = 0; m_PulseStart = new int[0]; }
        }
    }

    // ---- lift task mechanics ----

    int HoldStepsNeeded() => Mathf.Max(1, (HoldDecisions > 0 ? HoldDecisions : requiredHoldDecisions) * m_DecisionPeriod);

    /// <summary>Object dynamic under gravity at this episode's mass; the arm's penetration clamp against it is off from here on.</summary>
    void ReleaseObject()
    {
        m_ObjectDynamic = true;
        if (m_CylRb == null) return;
        m_CylRb.mass = m_Mass; m_CylRb.interpolation = RigidbodyInterpolation.None; m_CylRb.constraints = RigidbodyConstraints.None;
        m_CylRb.isKinematic = false; m_CylRb.useGravity = true;
        m_CylRb.linearVelocity = Vector3.zero; m_CylRb.angularVelocity = Vector3.zero; m_CylRb.WakeUp();
        m_ObjectUp0 = cylinderTransform.up;
    }

    void StorePrevPoses()
    {
        foreach (var col in segmentColliders) m_PrevPose[col] = (col.transform.position, col.transform.rotation);
        if (m_PalmCollider != null) m_PrevPose[m_PalmCollider] = (m_PalmCollider.transform.position, m_PalmCollider.transform.rotation);
    }

    /// <summary>Diagnostics only: force the phase-1 transition regardless of the gate (scripted calibration / exploit checks).</summary>
    public void DiagnosticForceTransition()
    {
        if (m_Phase != TaskPhase.Reach) return;
        m_Phase = TaskPhase.Lift; m_TransitionStep = StepCount; ReleaseObject();
    }
    /// <summary>Diagnostics only: set this episode's object mass (kg); applied immediately if the object is already dynamic.</summary>
    public void DiagnosticSetMass(float kg)
    {
        m_Mass = Mathf.Max(1e-3f, kg);
        m_MassObs = massRange.y > massRange.x ? Mathf.Clamp01(Mathf.Log(m_Mass / massRange.x) / Mathf.Log(massRange.y / massRange.x)) : 0f;
        if (m_CylRb != null) m_CylRb.mass = m_Mass;
    }
    /// <summary>Diagnostics only: set this episode's perturbation scale.</summary>
    public void DiagnosticSetPerturbScale(float scale) { m_PerturbScale = Mathf.Max(0f, scale); }

    /// <summary>
    /// Impedance grip model (see gripForceScale). Squeeze: a finger group whose spring is stalled against the object (clamp
    /// engaged) presses with its spring torque k (setpoint - angle) over the pivot-to-contact lever, split over the group's
    /// touching segments and capped at gripForceMax; the contact-force set is projected onto the null space of the grasp map
    /// (net force and net torque removed by the minimal-norm correction), so an unopposed contact yields nothing and opposed
    /// contacts yield a preload N each. Friction: per contact a Coulomb impulse against the relative tangential velocity
    /// (object point velocity minus the finger surface velocity from its pose change, with this step's gravity anticipated so
    /// a held object does not creep), capacity mu N, plus torsional friction about the normal (capacity mu N patch radius).
    /// PhysX keeps the geometry (depenetration); PhysX friction on hand contacts is zero. Masked groups are rigid and exert nothing.
    /// </summary>
    void ApplyGripForces()
    {
        m_GripForce = 0f; m_FrictionForce = 0f; m_Grip.Clear(); NetFrictionForce = Vector3.zero; NetSqueezeForce = Vector3.zero;
        if (m_CylRb == null || m_Morph == null || gripForceScale <= 0f) { m_GripForceSteps++; return; }
        for (int g = 0; g < GroupCount; g++)
        {
            var grp = m_Groups[g];
            if (grp.masked || !grp.clamped || grp.joints.Length == 0) continue;
            float tau = Mathf.Abs(m_Morph.stiffness[g] * (grp.setpoint - grp.angle) * Mathf.Deg2Rad) * gripForceScale;
            if (tau <= 0f) continue;
            int touching = 0;
            foreach (var col in grp.colliders) if (TryGetContact(col, out _)) touching++;
            if (touching == 0) continue;
            Vector3 pivot = grp.joints[0].position;
            foreach (var col in grp.colliders)
            {
                if (!TryGetContact(col, out ContactSample c)) continue;
                float lever = Mathf.Max(Vector3.Distance(pivot, c.point), 0.01f);
                float F = Mathf.Min(tau / lever / touching, gripForceMax);
                m_Grip.Add(new GripContact { p = c.point, f = -c.normal * F, n = c.normal, col = col });
            }
        }
        // the palm (no spring) reacts passively: a touching palm joins the contact set with no preload of its own and takes its share of
        // the balancing correction, so fingers squeezing the object against the palm produce a real preload on both sides
        if (m_Grip.Count >= 1 && m_PalmCollider != null && TryGetContact(m_PalmCollider, out ContactSample pc))
            m_Grip.Add(new GripContact { p = pc.point, f = Vector3.zero, n = pc.normal, col = m_PalmCollider });
        if (m_Grip.Count >= 2)
        {
            // minimal-norm correction removing the net wrench: solve (G G^T) lambda = w, subtract G_i^T lambda from each force
            Vector3 com = m_CylRb.worldCenterOfMass;
            double[,] M = new double[6, 7];
            foreach (var gc in m_Grip)
            {
                Vector3 r = gc.p - com; Vector3 F = gc.f;
                double[,] R = { { 0, -r.z, r.y }, { r.z, 0, -r.x }, { -r.y, r.x, 0 } };
                for (int a = 0; a < 3; a++)
                {
                    M[a, a] += 1.0;
                    for (int b = 0; b < 3; b++)
                    {
                        M[3 + a, b] += R[a, b];
                        M[a, 3 + b] += R[b, a];
                        double sum = 0; for (int k = 0; k < 3; k++) sum += R[a, k] * R[b, k];
                        M[3 + a, 3 + b] += sum;
                    }
                }
                Vector3 t = Vector3.Cross(r, F);
                M[0, 6] += F.x; M[1, 6] += F.y; M[2, 6] += F.z; M[3, 6] += t.x; M[4, 6] += t.y; M[5, 6] += t.z;
            }
            for (int a = 0; a < 6; a++) M[a, a] += 1e-6;
            for (int c = 0; c < 6; c++)
            {
                int piv = c; for (int rr = c + 1; rr < 6; rr++) if (System.Math.Abs(M[rr, c]) > System.Math.Abs(M[piv, c])) piv = rr;
                if (piv != c) for (int k = 0; k < 7; k++) { double tmp = M[c, k]; M[c, k] = M[piv, k]; M[piv, k] = tmp; }
                double d = M[c, c]; if (System.Math.Abs(d) < 1e-12) continue;
                for (int rr = 0; rr < 6; rr++) { if (rr == c) continue; double f = M[rr, c] / d; for (int k = c; k < 7; k++) M[rr, k] -= f * M[c, k]; }
            }
            Vector3 lf = new Vector3((float)(M[0, 6] / M[0, 0]), (float)(M[1, 6] / M[1, 1]), (float)(M[2, 6] / M[2, 2]));
            Vector3 lt = new Vector3((float)(M[3, 6] / M[3, 3]), (float)(M[4, 6] / M[4, 4]), (float)(M[5, 6] / M[5, 5]));
            float dt = Time.fixedDeltaTime, mu = Mathf.Max(objectFriction, 0f), mass = m_CylRb.mass; int nc = m_Grip.Count;
            bool onPedestal = cylinderCollider.bounds.min.y - m_PlatformTop < 0.003f;
            Quaternion toInertia = Quaternion.Inverse(m_CylRb.rotation * m_CylRb.inertiaTensorRotation); Vector3 Iten = m_CylRb.inertiaTensor;
            float[] Ns = new float[nc]; float Nsum = 0f;
            for (int i = 0; i < nc; i++)
            {
                var gc = m_Grip[i]; Vector3 r = gc.p - com;
                Vector3 f = gc.f - (lf + Vector3.Cross(lt, r));   // projected squeeze; only its preload matters (PhysX ignores balanced forces)
                Ns[i] = Mathf.Max(0f, Vector3.Dot(f, -gc.n)); Nsum += Ns[i]; NetSqueezeForce += f;
            }
            m_GripForce = Nsum;
            for (int i = 0; i < nc; i++)
            {
                var gc = m_Grip[i]; float N = Ns[i];
                if (N <= 0f || mu <= 0f || Nsum <= 0f) continue;
                float w = N / Nsum;   // the friction demand is shared in proportion to each contact's capacity
                Vector3 vSurf = Vector3.zero;
                if (m_PrevPose.TryGetValue(gc.col, out var prev))
                {   // finger surface velocity at the contact point from the segment's pose change over the last step
                    Vector3 pPrev = prev.pos + prev.rot * (Quaternion.Inverse(gc.col.transform.rotation) * (gc.p - gc.col.transform.position));
                    vSurf = (gc.p - pPrev) / dt;
                }
                Vector3 vRel = m_CylRb.GetPointVelocity(gc.p) - vSurf;
                Vector3 vT = vRel - Vector3.Dot(vRel, gc.n) * gc.n;
                // anticipate this step's gravity (static friction carries the weight) unless the pedestal still supports the object and this
                // finger surface is not moving upward: a supported object squeezed by a still hand keeps its weight on the pedestal
                bool carry = !onPedestal || vSurf.y > 0.005f;
                Vector3 gT = carry ? Physics.gravity - Vector3.Dot(Physics.gravity, gc.n) * gc.n : Vector3.zero;
                Vector3 dv = vT + gT * dt;
                if (dv.sqrMagnitude < 1e-12f) continue;
                // effective mass of the object at this point along the impulse direction (translation + rotation), as a contact solver uses
                Vector3 tdir = dv.normalized; Vector3 rxt = toInertia * Vector3.Cross(gc.p - com, tdir);
                float invMeff = 1f / mass + rxt.x * rxt.x / Mathf.Max(Iten.x, 1e-6f) + rxt.y * rxt.y / Mathf.Max(Iten.y, 1e-6f) + rxt.z * rxt.z / Mathf.Max(Iten.z, 1e-6f);
                Vector3 J = -(w / invMeff) * dv;
                float cap = mu * N * dt; if (J.magnitude > cap) J = J.normalized * cap;
                m_CylRb.AddForceAtPosition(J / dt, gc.p, ForceMode.Force);
                m_FrictionForce += J.magnitude / dt; NetFrictionForce += J / dt;
                float wRel = Vector3.Dot(m_CylRb.angularVelocity, gc.n);
                Vector3 nl = toInertia * gc.n; float In = Mathf.Max(nl.x * nl.x * Iten.x + nl.y * nl.y * Iten.y + nl.z * nl.z * Iten.z, 1e-6f);
                float Jt = -(In * w) * wRel; float capT = mu * N * gripPatchRadius * dt;
                if (Mathf.Abs(Jt) > capT) Jt = Mathf.Sign(Jt) * capT;
                m_CylRb.AddTorque(gc.n * (Jt / dt), ForceMode.Force);
            }
        }
        m_GripForceSum += m_GripForce; m_FrictionSum += m_FrictionForce; m_GripForceSteps++;
    }

    bool IsLifted() => cylinderCollider != null && m_ObjectDynamic && cylinderCollider.bounds.min.y >= m_PlatformTop + liftClearance;

    bool IsDropped()
    {
        if (cylinderCollider == null || m_CylRb == null) return false;
        if (Vector3.Distance(m_CylRb.position, GraspPoint) > dropDistance) return true;
        if (dropTiltDeg < 180f && Vector3.Angle(cylinderTransform.up, m_ObjectUp0) > dropTiltDeg) return true;
        return cylinderCollider.bounds.min.y < m_PlatformTop - dropBelowPlatform;
    }

    void FailEpisode(string reason)
    {
        AddReward(-dropPenalty); m_DropReturn -= dropPenalty; m_EndReason = reason;
        LogEpisode(false);
        EndEpisode();
    }

    /// <summary>Draw the hold window's pulses: random start step, random horizontal direction, magnitude in [0.5, 1] x peak, torque about a random axis.</summary>
    void SchedulePerturbations(int holdNeeded)
    {
        int n = Mathf.Max(0, perturbPulses), len = Mathf.Max(1, perturbPulseSteps);
        m_PulseStart = new int[n]; m_PulseForce = new Vector3[n]; m_PulseTorque = new Vector3[n];
        float peak = m_PerturbScale * perturbPeakWeightRatio * m_Mass * Physics.gravity.magnitude;
        for (int i = 0; i < n; i++)
        {
            m_PulseStart[i] = Random.Range(1, Mathf.Max(2, holdNeeded - len + 1));   // 1-based window step at which the pulse starts
            float mag = peak * Random.Range(0.5f, 1f);
            Vector2 d = Random.insideUnitCircle.normalized; if (d.sqrMagnitude < 1e-6f) d = Vector2.right;
            m_PulseForce[i] = new Vector3(d.x, 0f, d.y) * mag;
            m_PulseTorque[i] = Random.onUnitSphere * (mag * perturbTorqueLever);
        }
    }

    void ApplyPerturbation()
    {
        if (m_CylRb == null || !m_ObjectDynamic) return;
        int len = Mathf.Max(1, perturbPulseSteps);
        for (int i = 0; i < m_PulseStart.Length; i++)
        {
            if (m_HoldSteps < m_PulseStart[i] || m_HoldSteps >= m_PulseStart[i] + len) continue;
            m_CylRb.AddForce(m_PulseForce[i], ForceMode.Force);
            m_CylRb.AddTorque(m_PulseTorque[i], ForceMode.Force);
            float f = m_PulseForce[i].magnitude; if (f > m_MaxPulseForce) m_MaxPulseForce = f;
            if (m_HoldSteps == m_PulseStart[i]) m_PulsesApplied++;
        }
    }

    // ---- contact statistics (diagnostics) ----

    /// <summary>Coverage gap, thumb antipodality and vertical spread of the contact samples gathered this step. Logged only.</summary>
    private void ComputeContactStats(bool thumbTouching)
    {
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
                antipodal = Mathf.Clamp01(Mathf.Abs(Mathf.DeltaAngle(thumbAz, fingerAz)) / 180f);
            }
        }
        float minH = float.MaxValue, maxH = float.MinValue;
        foreach (var c in m_Contacts) { if (c.height < minH) minH = c.height; if (c.height > maxH) maxH = c.height; }
        LastVerticalSpread = m_Contacts.Count > 0 ? maxH - minH : 0f;
        LastCoverageGapDeg = largestGap;
        LastAntipodality = antipodal;
    }

    // ---- episode stats ----

    private void LogEpisode(bool success)
    {
        m_EpisodeActive = false;
        LastEpisode = new EpisodeRecord
        {
            success = success, endReason = m_EndReason, steps = StepCount, transitionStep = m_TransitionStep, stepsToLift = m_StepsToLift, holdSteps = m_HoldSteps, holdEntries = m_HoldEntries,
            contacts = CurrentContacts, distinctFingers = LastDistinctFingers, pulsesApplied = m_PulsesApplied, holdStepsPaid = m_HoldStepsPaid, thumb = LastThumbTouching, palm = LastPalmTouching, forearm = LastForearmTouching,
            mass = m_Mass, perturbScale = m_PerturbScale, maxPulseForce = m_MaxPulseForce, retShaping = m_ShapingReturn, retPhase = m_PhaseReturn, retHold = m_HoldReturn, retBonus = m_BonusReturn, retDrop = m_DropReturn,
            retPenalty = m_PenaltyReturn, retEffort = m_EffortReturn, bottomAboveTop = cylinderCollider != null ? cylinderCollider.bounds.min.y - m_PlatformTop : 0f,
            gripForceMean = m_GripForceSteps > 0 ? m_GripForceSum / m_GripForceSteps : 0f, gripForceEnd = m_GripForce,
            coverageGapDeg = LastCoverageGapDeg, antipodality = LastAntipodality, verticalSpread = LastVerticalSpread
        };
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
        rec.Add("Grasp/ForearmAtEnd", LastForearmTouching ? 1f : 0f);
        rec.Add("Grasp/CoverageGapDeg", LastCoverageGapDeg);
        rec.Add("Grasp/Antipodality", LastAntipodality);
        rec.Add("Grasp/VerticalSpread", LastVerticalSpread);
        rec.Add("Grasp/HoldContactStd", holdStd);
        rec.Add("Task/PhaseReached", (int)m_Phase);
        rec.Add("Task/Transitioned", m_TransitionStep >= 0 ? 1f : 0f);
        rec.Add("Task/Lifted", m_StepsToLift >= 0 ? 1f : 0f);
        rec.Add("Task/StepsToTransition", m_TransitionStep);
        rec.Add("Task/StepsToLift", m_StepsToLift);
        rec.Add("Task/HoldSteps", m_HoldSteps);
        rec.Add("Task/HoldEntries", m_HoldEntries);
        rec.Add("Task/EndDrop", m_EndReason == "drop" ? 1f : 0f);
        rec.Add("Task/EndLiftBudget", m_EndReason == "liftBudget" ? 1f : 0f);
        rec.Add("Task/EndTaskBudget", m_EndReason == "taskBudget" ? 1f : 0f);
        rec.Add("Task/EndMaxStep", m_EndReason == "maxStep" ? 1f : 0f);
        rec.Add("Task/ObjectMass", m_Mass);
        rec.Add("Task/PerturbScale", m_PerturbScale);
        rec.Add("Task/PulsesApplied", m_PulsesApplied);
        rec.Add("Task/MaxPulseForce", m_MaxPulseForce);
        rec.Add("Task/ObjectBottomAboveTop", cylinderCollider != null ? cylinderCollider.bounds.min.y - m_PlatformTop : 0f);
        rec.Add("Task/GripForceMean", m_GripForceSteps > 0 ? m_GripForceSum / m_GripForceSteps : 0f);
        rec.Add("Task/GripForceAtEnd", m_GripForce);
        rec.Add("Task/FrictionForceMean", m_GripForceSteps > 0 ? m_FrictionSum / m_GripForceSteps : 0f);
        rec.Add("Task/EndTilt", m_EndReason == "drop" && m_ObjectDynamic && cylinderTransform != null ? Vector3.Angle(cylinderTransform.up, m_ObjectUp0) : 0f);
        rec.Add("Grasp/MinGraspPointDistance", m_MinGraspPointDistance);
        rec.Add("Grasp/FinalGraspPointDistance", previousGraspPointDistance);
        rec.Add("Grasp/ResidualSegmentDistance", ResidualSegmentDistance);
        rec.Add("Grasp/MaxPenetration", m_MaxPenetration);
        rec.Add("Grasp/SpawnDistance", m_SpawnDistance);
        rec.Add("Grasp/CylinderYaw", yaw);
        rec.Add("Return/Shaping", m_ShapingReturn);
        rec.Add("Return/Phase", m_PhaseReturn);
        rec.Add("Return/Hold", m_HoldReturn);
        rec.Add("Return/Bonus", m_BonusReturn);
        rec.Add("Return/Drop", m_DropReturn);
        rec.Add("Return/Penalty", m_PenaltyReturn);
        rec.Add("Return/HoldStepsPaid", m_HoldStepsPaid);
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
            rec.Add("Morph/FingerLengthRatio", m_FingerLengthRatio);
            rec.Add("Morph/OutOfRangeEvents", m_Morph.OutOfRangeEvents);
        }

        if (!string.IsNullOrEmpty(statsCsvPath))
        {
            try
            {
                if (!System.IO.File.Exists(statsCsvPath))
                    System.IO.File.WriteAllText(statsCsvPath, "episode,success,endReason,steps,stepsToFirstSixContacts,stepsToHoldCriterion,stepsToTransition,stepsToLift,holdSteps,holdEntries,contacts,distinctFingers,thumb,palm,forearm,coverageGapDeg,antipodality,verticalSpread,holdContactStd,minGraspDist,finalGraspDist,residualSegDist,maxPenetration,spawnDistance,cylYaw,mass,perturbScale,pulsesApplied,maxPulseForce,retShaping,retPhase,retHold,retBonus,retDrop,retPenalty,holdStepsPaid\n");
                System.IO.File.AppendAllText(statsCsvPath, string.Join(",", new string[] {
                    CompletedEpisodes.ToString(), success ? "1" : "0", m_EndReason, StepCount.ToString(), m_StepsToFirstSixContacts.ToString(), m_StepsToFirstHoldCriterion.ToString(),
                    m_TransitionStep.ToString(), m_StepsToLift.ToString(), m_HoldSteps.ToString(), m_HoldEntries.ToString(),
                    CurrentContacts.ToString(), LastDistinctFingers.ToString(), LastThumbTouching ? "1" : "0", LastPalmTouching ? "1" : "0", LastForearmTouching ? "1" : "0",
                    LastCoverageGapDeg.ToString("F1"), LastAntipodality.ToString("F3"), LastVerticalSpread.ToString("F4"), holdStd.ToString("F3"),
                    m_MinGraspPointDistance.ToString("F4"), previousGraspPointDistance.ToString("F4"), ResidualSegmentDistance.ToString("F4"), m_MaxPenetration.ToString("F5"),
                    m_SpawnDistance.ToString("F3"), yaw.ToString("F0"), m_Mass.ToString("F3"), m_PerturbScale.ToString("F2"), m_PulsesApplied.ToString(), m_MaxPulseForce.ToString("F2"),
                    m_ShapingReturn.ToString("F4"), m_PhaseReturn.ToString("F3"), m_HoldReturn.ToString("F4"), m_BonusReturn.ToString("F2"), m_DropReturn.ToString("F2"), m_PenaltyReturn.ToString("F4"), m_HoldStepsPaid.ToString() }) + "\n");
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
        if (Mathf.Approximately(target, from)) { if (m_ObjectDynamic) YieldToObject(grp, from, target); return; }

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
        if (m_ObjectDynamic) YieldToObject(grp, lo, target);
    }

    /// <summary>
    /// Compliant finger (dynamic object only): when the group penetrates the object at the angle it is holding, because the
    /// object moved into it (arm motion, perturbation, the object settling), the finger opens to the contact angle instead of
    /// pushing with the clamp's infinite force. The spring's bounded squeeze (ApplyGripForces) is then the only force the
    /// finger exerts. Searches up to yieldRangeDeg toward open.
    /// </summary>
    private void YieldToObject(JointGroup grp, float angle, float target)
    {
        if (!Penetrates(grp.colliders)) return;
        float openDir = target <= angle ? 1f : -1f;   // away from the spring's target
        float a = angle, b = angle + openDir * yieldRangeDeg;
        SetGroupAngle(grp, b);
        if (Penetrates(grp.colliders)) { grp.angle = b; grp.clamped = true; return; }   // still penetrating after the full range: leave it there
        for (int i = 0; i < contactSolveIterations; i++)
        {
            float mid = 0.5f * (a + b);
            SetGroupAngle(grp, mid);
            if (Penetrates(grp.colliders)) a = mid; else b = mid;
        }
        SetGroupAngle(grp, b);
        grp.angle = b; grp.clamped = true; grp.vel = 0f;
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

        // Once the object is dynamic the arm moves freely and carries it through the contact solver (fingers keep their clamp)
        if (m_ObjectDynamic) { SetArmAngle(ax, target); ax.angle = target; return; }
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
        sample.point = onCylinder; sample.normal = normal.sqrMagnitude > 1e-12f ? normal.normalized : radial.normalized;   // outward normal of the object at the contact
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
