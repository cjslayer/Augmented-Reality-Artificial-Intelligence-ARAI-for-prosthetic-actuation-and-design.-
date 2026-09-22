using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections.Generic;

/// <summary>
/// Prosthetic arm grasp agent on the PhysX articulation (branch articulated-hand, run 011 task). The hand is an
/// ArticulatedHand skeleton: shoulder (2) and elbow are velocity drives commanded by the arm actions; the wrist (2) and
/// the 14 finger groups are acceleration-mode spring drives whose targets the finger/wrist actions move at a bounded rate
/// (delta-target control). Contacts are real PhysX collisions relayed by the links. The task is the lift-and-perturb
/// phase machine of run 011: Reach -(contact gate)-> Lift (object released, dynamic) -> Hold (K decisions aloft under
/// random wrench pulses) -> success; a drop or a budget expiry ends the episode with the drop penalty.
/// </summary>
public class ArmGraspAgent : Agent
{
    [Header("Rotation Settings")]
    [Tooltip("Legacy (kinematic velocity actuation); unused on the articulation.")]
    public float rotationSpeed = 90f;
    [Tooltip("Degrees per second of shoulder / elbow joint velocity per action unit (velocity drives).")]
    public float armRotationSpeed = 90f;
    public GameObject arm;

    // Joint groups rotate about their bone's local Z. 0 deg is the open pose (finger Z rotations zeroed when the skeleton is captured).
    // Fingers flex toward the palm with negative Z; the thumb opposes them with positive Z.
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

    // Arm axes (5). Angles are relative to the captured scene pose (0 deg). Proximal (shoulder, elbow) emulate the user's arm;
    // distal (wrist) belongs to the prosthesis.
    [Header("Arm Joints (deg relative to scene pose; x = min, y = max)")]
    public Vector2 shoulderFlexionLimits = new Vector2(-40f, 100f);
    public Vector2 shoulderAbductionLimits = new Vector2(-60f, 60f);
    public Vector2 elbowFlexionLimits = new Vector2(-47f, 98f);
    public Vector2 wristFlexionLimits = new Vector2(-70f, 80f);
    public Vector2 wristPronationLimits = new Vector2(-80f, 80f);
    public bool actuateProximalJoints = true;
    public string shoulderPath = "Armature/Bone/Bicep.r";
    public string forearmPath = "Armature/Bone/Bicep.r/forearm.r";
    [Tooltip("Hierarchy path of the wrist/palm bone relative to this agent.")]
    public string palmPath = "Armature/Bone/Bicep.r/forearm.r/palm.r";

    [Header("Target Spawn (world space)")]
    public Vector3 spawnCenter = new Vector3(-0.329f, 0.690f, 5.474f);
    [Tooltip("Radius (m) of the horizontal spawn disk. Overridden by the 'spawn_radius' environment parameter when present.")]
    public float spawnRadius = 0.09f;
    [Tooltip("Cylinder height is raised by a uniform random amount in [0, spawnHeightRange] (m) when not resting on the platform.")]
    public float spawnHeightRange = 0.036f;
    public bool randomizeYaw = true;
    [Tooltip("Accept only spawns whose distance from the shoulder pivot is within [x, y] (m).")]
    public Vector2 reachRange = new Vector2(0.40f, 0.54f);
    public int spawnAttempts = 50;

    [Header("Contact")]
    [Tooltip("Contact separation (m) at or below which a reported collision counts as touching.")]
    public float contactDistance = 0.001f;

    [Header("Reward")]
    public float distanceRewardScale = 1.0f;
    public float palmDistanceRewardScale = 1.0f;
    public float shapingScale = 1f / 15f;
    public float shapingFloorDistance = 0.018f;
    [Tooltip("Reference-hand offset (m, palm frame: x = palm normal / closing direction, y = along the fingers, z = across the palm) from the palm pivot to the centre of the region the closed fingers enclose.")]
    public Vector3 graspPointOffset = new Vector3(0.0504f, 0.0774f, 0.0281f);
    public float successBonus = 1.0f;
    public int requiredContactSegments = 6;
    public int requiredDistinctFingers = 2;
    public bool requireThumbContact = true;
    public int requiredHoldDecisions = 10;
    [Tooltip("Per-step penalty = -existentialPenaltyScale / MaxStep.")]
    public float existentialPenaltyScale = 1.0f;

    [Header("Lift Task (run 011)")]
    public string platformName = "Platform";
    public bool restOnPlatform = true;
    public float restClearance = 0.0005f;
    [Tooltip("Lifted = the object's world AABB bottom is at least this (m) above the platform top.")]
    public float liftClearance = 0.03f;
    public float phaseReward = 0.1f;
    public int liftBudgetSteps = 200;
    public int taskBudgetSteps = 350;
    public float holdRewardPerStep = 0.004f;
    public int holdRewardBudgetSteps = 50;
    public float dropPenalty = 0.2f;
    public float dropDistance = 0.11f;
    public float dropBelowPlatform = 0.007f;
    [Tooltip("Drop criterion: object axis tilted more than this (deg) from its orientation at the transition; 180 disables.")]
    public float dropTiltDeg = 60f;
    public Vector2 massRange = new Vector2(0.2f, 1.5f);
    public int perturbPulses = 3;
    public int perturbPulseSteps = 5;
    public float perturbPeakWeightRatio = 1.5f;
    public float perturbTorqueLever = 0.054f;
    public float perturbScale = 1f;
    [Tooltip("Diagnostics only: >= 0 overrides the perturbation scale.")]
    public float perturbScaleOverride = -1f;
    [Tooltip("Friction coefficient of the object's collider (static = dynamic, Maximum combine: hand contacts get it too). The drop-test standard's primary value is 1.0.")]
    public float objectFriction = 1.0f;

    [Header("Actuation")]
    [Tooltip("Actions move each spring drive's target by up to this many deg/s.")]
    public float setpointRateDegPerSec = 180f;
    [Tooltip("Neutral pose (deg) held by masked (non-actuated) finger groups. Order: index B/M/E, middle B/M/E, ring B/M/E, pinky B/M/E, thumb B/E.")]
    public float[] neutralPoseDeg = { -30f, -20f, -10f, -30f, -20f, -10f, -30f, -20f, -10f, -30f, -20f, -10f, 20f, 20f };
    [Tooltip("Wedge thresholds scale with handSpan / this reference span (m); 0 = measure the reference from the unscaled rig at Initialize.")]
    public float wedgeHandSpanRef = 0f;

    [Header("Effort / Safety")]
    public float effortWeight = 2e-5f;
    public float safetyWeight = 1e-4f;

    [Header("Episode Stats")]
    public string statsCsvPath = "";

    [Header("Observations")]
    public float workspaceScale = 0.15f;
    public float targetObsScale = 0.36f;

    [Header("Testing")]
    public float[] heuristicActions = new float[ActionCount];

    // ---- public state ----
    public int SuccessCount { get; private set; }
    public int HoldDecisions { get; private set; }
    public int CurrentContacts { get; private set; }
    public float GetGroupAngle(int group) => m_Hand != null && m_Hand.Built ? m_Hand.GroupAngle(group) : 0f;
    public float GetArmAngle(int axis) => m_Hand != null && m_Hand.Built ? m_Hand.ArmAngle(axis) : 0f;
    public Vector3 EffectiveGraspPointOffset => new Vector3(graspPointOffset.x * m_FingerLengthRatio, graspPointOffset.y, graspPointOffset.z);
    public Vector3 GraspPoint => m_Palm != null ? m_Palm.position + m_Palm.TransformDirection(EffectiveGraspPointOffset) : Vector3.zero;
    public float GraspPointDistance => cylinderTransform != null ? Vector3.Distance(GraspPoint, cylinderTransform.position) : 0f;

    public const int GroupCount = 14;
    public const int ArmAxisCount = 5;
    public const int ActionCount = GroupCount + ArmAxisCount;
    static readonly int[] k_GroupFinger = { 0,0,0, 1,1,1, 2,2,2, 3,3,3, 4,4 };
    static readonly int[] k_GroupParent = { -1, 0, 1, -1, 3, 4, -1, 6, 7, -1, 9, 10, -1, 12 };
    static readonly int[] k_GroupSegment = { 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1 };
    const int k_ThumbFinger = 4;

    // ---- rig ----
    ArticulatedHand m_Hand; MorphologyManager m_Morph;
    Unity.MLAgents.Sensors.BufferSensorComponent m_TokenSensor;
    public const int TokenSize = 13;
    public const int MaxTokens = GroupCount + 2;
    float[] m_Token = new float[TokenSize];
    float m_HandSpanRatio = 1f, m_FingerLengthRatio = 1f;
    int m_LimitEvents, m_SaturationEvents; float m_EffortReturn, m_SafetyReturn; int m_EpisodeSaturationEvents, m_EpisodeLimitEvents;
    public float EffortReturn => m_EffortReturn;
    public float SafetyReturn => m_SafetyReturn;
    public int EpisodeClampEvents => m_EpisodeSaturationEvents;   // drive force-limit saturations (was: penetration clamps)
    public int EpisodeLimitEvents => m_EpisodeLimitEvents;
    public float HandSpanRatio => m_HandSpanRatio;
    public float FingerLengthRatio => m_FingerLengthRatio;
    public MorphologyManager Morphology => m_Morph;
    public ArticulatedHand Hand => m_Hand;
    float[] m_Setpoint = new float[GroupCount]; bool[] m_Masked = new bool[GroupCount]; float[] m_WristSetpoint = new float[2];
    /// <summary>State of a finger group (angle deg, velocity deg/s, drive target deg).</summary>
    public Vector3 GetGroupState(int g) => m_Hand != null && m_Hand.Built ? new Vector3(m_Hand.GroupAngle(g), m_Hand.GroupVelocity(g), m_Setpoint[g]) : Vector3.zero;
    public bool IsGroupMasked(int g) => m_Masked[g];
    public bool IsGroupTouching(int g) => m_GroupTouching[g];
    /// <summary>State of an arm axis (angle deg, velocity deg/s, target deg for the wrist axes).</summary>
    public Vector3 GetArmState(int axis) => m_Hand != null && m_Hand.Built ? new Vector3(m_Hand.ArmAngle(axis), m_Hand.ArmVelocity(axis), axis >= 3 ? m_WristSetpoint[axis - 3] : 0f) : Vector3.zero;
    public void RefreshImpedanceMatrices() => ConfigureDrives();
    /// <summary>Diagnostics only: place a finger group's drive target (deg).</summary>
    public void SetGroupSetpoint(int g, float deg) { var lim = GetLimits(g); m_Setpoint[g] = Mathf.Clamp(deg, lim.x, lim.y); if (m_Hand != null && m_Hand.Built && !m_Masked[g]) m_Hand.SetGroupTarget(g, m_Setpoint[g]); }
    /// <summary>Diagnostics only: wrist axes (3, 4) take a target (deg); shoulder / elbow axes are velocity drives and ignore it.</summary>
    public void SetArmSetpoint(int axis, float deg) { if (axis < 3) return; var lim = GetArmLimits(axis); m_WristSetpoint[axis - 3] = Mathf.Clamp(deg, lim.x, lim.y); if (m_Hand != null && m_Hand.Built) m_Hand.SetWristTarget(axis - 3, m_WristSetpoint[axis - 3]); }

    Transform m_Shoulder, m_Forearm, m_Palm;
    private Transform cylinderTransform;
    private Collider cylinderCollider;
    private List<Collider> segmentColliders = new List<Collider>();
    private List<Collider> armColliders = new List<Collider>();
    private Dictionary<Collider, int> segmentFinger = new Dictionary<Collider, int>();
    private Dictionary<Collider, float> previousDistances = new Dictionary<Collider, float>();
    private Dictionary<Collider, float> initialDistances = new Dictionary<Collider, float>();
    private float previousGraspPointDistance, initialGraspPointDistance;
    private Collider m_PalmCollider, m_ForearmCollider;
    private int m_HoldSteps;
    private int m_DecisionPeriod = 1;

    struct ContactSample { public float azimuthDeg; public float height; public bool isThumb; public bool isPalm; public Vector3 point, normal; public float impulse; }
    private List<ContactSample> m_Contacts = new List<ContactSample>();
    private List<float> m_Azimuths = new List<float>();
    bool[] m_GroupTouching = new bool[GroupCount];

    // per-episode bookkeeping
    private float m_ShapingReturn, m_PenaltyReturn, m_BonusReturn;
    private int m_StepsToFirstSixContacts = -1, m_StepsToFirstHoldCriterion = -1;
    private float m_MinGraspPointDistance, m_MaxPenetration, m_SpawnDistance;
    private int m_HoldWindowCount; private double m_HoldWindowSum, m_HoldWindowSumSq;
    private bool m_EpisodeActive;

    public float LastCoverageGapDeg { get; private set; }
    public float LastAntipodality { get; private set; }
    public float LastVerticalSpread { get; private set; }
    public bool LastPalmTouching { get; private set; }
    public bool LastForearmTouching { get; private set; }
    public int LastDistinctFingers { get; private set; }
    public bool LastThumbTouching { get; private set; }
    public bool LastHoldCriterionMet { get; private set; }
    public float ShapingReturn => m_ShapingReturn;
    /// <summary>Diagnostics (read-only): the analytic telescoped value of the potential-based shaping from the episode start to now, i.e. what ShapingReturn must equal for a non-teleporting path.</summary>
    public float TelescopedShaping()
    {
        float sum = 0f;
        foreach (var col in segmentColliders) if (initialDistances.TryGetValue(col, out float d0) && d0 > 0f) sum += distanceRewardScale * (d0 - SegmentDistance(col)) / d0;
        if (initialGraspPointDistance > 0f) sum += palmDistanceRewardScale * (initialGraspPointDistance - GraspPointDistance) / initialGraspPointDistance;
        return sum * shapingScale;
    }
    /// <summary>Diagnostics (read-only): consecutive lifted steps counted toward the hold requirement so far this episode.</summary>
    public int HoldStepsNow => m_HoldSteps;
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
    public bool Lifted => IsLifted();
    /// <summary>Sum of drive torques (N m) of the touching finger groups this step.</summary>
    public float GripForce { get; private set; }
    /// <summary>Contact impulse magnitude sum / dt (N) over all hand-object contacts this step.</summary>
    public float FrictionForce { get; private set; }
    public Vector3 NetFrictionForce { get; private set; }
    public Vector3 NetSqueezeForce { get; private set; }
    public float MaxPenetration => m_MaxPenetration;
    public float SpawnDistance => m_SpawnDistance;
    public float ResidualSegmentDistance { get; private set; }
    // run-010 compatibility (BoEvalHarness, the 010 evaluation endpoint, still reads these)
    public float LastQuality => 0f;
    public float LastWedge => 0f;
    public int QualityStepsPaid => 0;
    public float QualityReturn => 0f;
    [System.NonSerialized] public float qualityPayPerStep = 0f;

    public EpisodeRecord LastEpisode;
    [System.Serializable]
    public struct EpisodeRecord
    {
        public bool success; public string endReason; public int steps, transitionStep, stepsToLift, holdSteps, holdEntries, contacts, distinctFingers, pulsesApplied, holdStepsPaid;
        public bool thumb, palm, forearm; public float mass, perturbScale, maxPulseForce, retShaping, retPhase, retHold, retBonus, retDrop, retPenalty, retEffort, bottomAboveTop, coverageGapDeg, antipodality, verticalSpread, gripForceMean, gripForceEnd, maxPenetration, maxJointSpeed;
    }

    // ---- lift task ----
    public enum TaskPhase { Reach = 0, Lift = 1, Hold = 2 }
    TaskPhase m_Phase;
    Rigidbody m_CylRb; RigidbodyConstraints m_CylConstraints0; RigidbodyInterpolation m_CylInterp0; float m_CylHalfHeight = 0.14f;
    float m_PlatformTop; bool m_ObjectDynamic;
    float m_Mass = 1f, m_MassObs, m_PerturbScale = 1f;
    int m_TransitionStep = -1, m_StepsToLift = -1, m_HoldStepsPaid, m_HoldEntries, m_PulsesApplied;
    float m_PhaseReturn, m_HoldReturn, m_DropReturn, m_MaxPulseForce; string m_EndReason = "";
    int[] m_PulseStart = new int[0]; Vector3[] m_PulseForce = new Vector3[0], m_PulseTorque = new Vector3[0];
    float m_GripForceSum, m_MaxJointSpeed; int m_GripForceSteps; PhysicsMaterial m_ObjectMaterial; Vector3 m_ObjectUp0;

    public override void Initialize()
    {
        m_Hand = GetComponent<ArticulatedHand>();
        if (m_Hand == null) { m_Hand = gameObject.AddComponent<ArticulatedHand>(); }
        m_Hand.shoulderPath = shoulderPath; m_Hand.forearmPath = forearmPath; m_Hand.palmPath = palmPath;
        m_Morph = GetComponent<MorphologyManager>();
        if (m_Morph == null) Debug.LogError("[ArmGraspAgent] a MorphologyManager is required on the articulation");

        var cylObj = GameObject.FindGameObjectWithTag("Cylinder");
        if (cylObj != null)
        {
            cylinderTransform = cylObj.transform; cylinderCollider = cylObj.GetComponent<Collider>(); m_CylRb = cylObj.GetComponent<Rigidbody>();
            if (cylinderCollider != null && m_Hand != null) cylinderCollider.contactOffset = m_Hand.contactOffset;
            if (m_CylRb != null) { m_CylConstraints0 = m_CylRb.constraints; m_CylInterp0 = m_CylRb.interpolation; m_CylRb.sleepThreshold = 0f; }
            if (cylinderCollider != null) m_CylHalfHeight = cylinderTransform.position.y - cylinderCollider.bounds.min.y;
            if (cylinderCollider != null && objectFriction > 0f)
            {
                m_ObjectMaterial = new PhysicsMaterial("ObjectMu") { staticFriction = objectFriction, dynamicFriction = objectFriction, frictionCombine = PhysicsMaterialCombine.Maximum, bounciness = 0f, bounceCombine = PhysicsMaterialCombine.Minimum };
                cylinderCollider.sharedMaterial = m_ObjectMaterial;
            }
        }
        var plat = !string.IsNullOrEmpty(platformName) ? GameObject.Find(platformName) : null;
        if (plat != null && plat.TryGetComponent<Collider>(out var platCol)) m_PlatformTop = platCol.bounds.max.y;
        else { m_PlatformTop = spawnCenter.y - m_CylHalfHeight; Debug.LogWarning("[ArmGraspAgent] platform '" + platformName + "' not found"); }

        // build the skeleton at the reference morphology, then let the morphology manager capture it and take over rebuilds
        m_Hand.CaptureBones();
        m_Hand.Rebuild(null, null);
        CacheLinks();
        if (m_Morph != null)
        {
            m_Morph.rig = m_Hand;
            var groupJoints = new Transform[GroupCount][];
            for (int g = 0; g < GroupCount; g++) groupJoints[g] = m_Hand.Groups[g] != null ? new Transform[] { m_Hand.Groups[g].transform } : new Transform[0];
            m_Morph.Initialize(groupJoints, m_Palm);
            CacheLinks();   // Initialize rebuilt the skeleton once more at scale 1
            if (wedgeHandSpanRef <= 0f) wedgeHandSpanRef = m_Morph.HandSpanRef;
        }
        m_TokenSensor = GetComponent<Unity.MLAgents.Sensors.BufferSensorComponent>();
        if (neutralPoseDeg == null || neutralPoseDeg.Length != GroupCount) neutralPoseDeg = new float[GroupCount];
        var requester = GetComponent<DecisionRequester>();
        m_DecisionPeriod = requester != null ? Mathf.Max(1, requester.DecisionPeriod) : 1;
        if (heuristicActions == null || heuristicActions.Length != ActionCount) heuristicActions = new float[ActionCount];
    }

    /// <summary>Re-cache link transforms and colliders after a skeleton rebuild.</summary>
    void CacheLinks()
    {
        m_Shoulder = m_Hand.ShoulderLink; m_Forearm = m_Hand.ForearmLink; m_Palm = m_Hand.PalmLink;
        segmentColliders.Clear(); segmentFinger.Clear(); armColliders.Clear();
        for (int g = 0; g < GroupCount; g++)
        {
            var b = m_Hand.Groups[g]; if (b == null) continue;
            var col = b.GetComponent<Collider>(); if (col == null) continue;
            segmentColliders.Add(col); segmentFinger[col] = k_GroupFinger[g];
        }
        m_ForearmCollider = m_Forearm != null ? m_Forearm.GetComponent<Collider>() : null; if (m_ForearmCollider != null) armColliders.Add(m_ForearmCollider);
        m_PalmCollider = m_Palm != null ? m_Palm.GetComponent<Collider>() : null; if (m_PalmCollider != null) armColliders.Add(m_PalmCollider);
    }

    /// <summary>Drive gains from this episode's theta: fingers and wrist as (omega, zeta) springs, masked groups held at neutral, arm axes as velocity drives with their limits.</summary>
    void ConfigureDrives()
    {
        if (m_Hand == null || !m_Hand.Built) return;
        for (int g = 0; g < GroupCount; g++)
        {
            float k = m_Morph != null ? m_Morph.stiffness[g] : 0.5f, b = m_Morph != null ? m_Morph.damping[g] : 0.03f;
            m_Masked[g] = m_Morph != null && !m_Morph.mask[g];
            var lim = GetLimits(g);
            m_Hand.ConfigureGroup(g, k, b, lim, m_Masked[g], neutralPoseDeg[g]);
            m_Setpoint[g] = m_Masked[g] ? Mathf.Clamp(neutralPoseDeg[g], lim.x, lim.y) : 0f;
        }
        for (int a = 0; a < 2; a++)
        {
            int mi = GroupCount + a;
            float k = m_Morph != null ? m_Morph.stiffness[mi] : 5f, b = m_Morph != null ? m_Morph.damping[mi] : 0.5f;
            m_Hand.ConfigureWrist(a, k, b, GetArmLimits(3 + a)); m_WristSetpoint[a] = 0f;
        }
        for (int a = 0; a < 3; a++) m_Hand.ConfigureArm(a, GetArmLimits(a));
    }

    public override void OnEpisodeBegin()
    {
        if (m_EpisodeActive) { if (string.IsNullOrEmpty(m_EndReason)) m_EndReason = "maxStep"; LogEpisode(false); }

        // object back to its supported (kinematic) state; mass and perturbation scale for this episode
        var epp = Academy.Instance.EnvironmentParameters;
        m_Phase = TaskPhase.Reach; m_ObjectDynamic = false; m_TransitionStep = -1; m_StepsToLift = -1; m_HoldStepsPaid = 0; m_HoldEntries = 0; m_PulsesApplied = 0;
        m_PhaseReturn = m_HoldReturn = m_DropReturn = 0f; m_MaxPulseForce = 0f; m_EndReason = ""; LastForearmTouching = false;
        m_GripForceSum = 0f; m_GripForceSteps = 0; m_MaxJointSpeed = 0f; m_PulseStart = new int[0];
        float mMin = Mathf.Max(1e-3f, epp.GetWithDefault("mass/min", massRange.x)), mMax = Mathf.Max(mMin, epp.GetWithDefault("mass/max", massRange.y));
        m_Mass = Mathf.Exp(Random.Range(Mathf.Log(mMin), Mathf.Log(mMax)));
        m_MassObs = massRange.y > massRange.x ? Mathf.Clamp01(Mathf.Log(m_Mass / massRange.x) / Mathf.Log(massRange.y / massRange.x)) : 0f;
        m_PerturbScale = perturbScaleOverride >= 0f ? perturbScaleOverride : Mathf.Clamp01(epp.GetWithDefault("perturb/scale", perturbScale));
        if (m_CylRb != null) { m_CylRb.isKinematic = true; m_CylRb.useGravity = false; m_CylRb.constraints = m_CylConstraints0; m_CylRb.interpolation = m_CylInterp0; m_CylRb.mass = m_Mass; }

        // morphology for this episode: rebuilds the skeleton at the open pose with this episode's link scales and masses
        m_HandSpanRatio = 1f; m_FingerLengthRatio = 1f;
        if (m_Morph != null)
        {
            m_Morph.ApplyForEpisode();
            m_HandSpanRatio = wedgeHandSpanRef > 0f ? m_Morph.HandSpan / wedgeHandSpanRef : 1f;
            m_FingerLengthRatio = m_Morph.FingerLengthRatio;
        }
        CacheLinks();
        ConfigureDrives();
        m_Hand.ClearContacts();
        m_LimitEvents = m_SaturationEvents = 0; m_EpisodeSaturationEvents = m_EpisodeLimitEvents = 0; m_EffortReturn = m_SafetyReturn = 0f;

        SpawnCylinder();

        previousDistances.Clear(); initialDistances.Clear();
        foreach (var col in segmentColliders)
        {
            float d0 = SegmentDistance(col);
            previousDistances[col] = d0;
            initialDistances[col] = Mathf.Max(d0, shapingFloorDistance);
        }
        previousGraspPointDistance = GraspPointDistance;
        initialGraspPointDistance = Mathf.Max(previousGraspPointDistance, shapingFloorDistance);

        m_HoldSteps = 0; CurrentContacts = 0;
        m_ShapingReturn = m_PenaltyReturn = m_BonusReturn = 0f;
        m_StepsToFirstSixContacts = m_StepsToFirstHoldCriterion = -1;
        m_MinGraspPointDistance = previousGraspPointDistance;
        m_MaxPenetration = 0f;
        m_SpawnDistance = (m_Shoulder != null && cylinderTransform != null) ? Vector3.Distance(m_Shoulder.position, cylinderTransform.position) : 0f;
        m_HoldWindowCount = 0; m_HoldWindowSum = m_HoldWindowSumSq = 0;
        LastCoverageGapDeg = 360f; LastAntipodality = 0f; LastVerticalSpread = 0f; LastPalmTouching = false; LastDistinctFingers = 0; LastThumbTouching = false; LastHoldCriterionMet = false;
        m_EpisodeActive = true;
        HoldDecisions = Mathf.Max(1, Mathf.RoundToInt(Academy.Instance.EnvironmentParameters.GetWithDefault("hold/decisions", Academy.Instance.EnvironmentParameters.GetWithDefault("hold_decisions", requiredHoldDecisions))));   // run 011 curriculum key hold/decisions (run-010 key hold_decisions kept)
    }

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

    // Fixed vector observation (24): palm-frame target 3, task progress 5, arm axes sin/cos 10, morphology summary 6.
    // Per-joint tokens (BufferSensor, up to 16 x 13): one per ACTIVE finger group plus the two wrist axes.
    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 rel = (m_Palm != null && cylinderTransform != null)
            ? m_Palm.InverseTransformDirection(cylinderTransform.position - m_Palm.position) / Mathf.Max(targetObsScale, 1e-4f)
            : Vector3.zero;
        sensor.AddObservation(Vector3.ClampMagnitude(rel, 1.5f));

        int holdNeeded = HoldStepsNeeded();
        sensor.AddObservation(Mathf.Clamp01((float)m_HoldSteps / holdNeeded));
        sensor.AddObservation(CurrentContacts / (float)GroupCount);
        sensor.AddObservation(holdRewardBudgetSteps > 0 ? Mathf.Clamp01((float)m_HoldStepsPaid / holdRewardBudgetSteps) : 0f);
        sensor.AddObservation(0.5f * (int)m_Phase);
        sensor.AddObservation(m_MassObs);

        for (int a = 0; a < ArmAxisCount; a++)
        {
            float rad = GetArmAngle(a) * Mathf.Deg2Rad;
            sensor.AddObservation(Mathf.Sin(rad));
            sensor.AddObservation(Mathf.Cos(rad));
        }

        for (int f = 0; f < MorphologyManager.FingerCount; f++) sensor.AddObservation(m_Morph != null ? m_Morph.lengthScale[f] : 1f);
        sensor.AddObservation(m_Morph != null ? m_Morph.ActiveCount / (float)GroupCount : 1f);

        if (m_TokenSensor == null || m_Hand == null || !m_Hand.Built) return;
        float scale = Mathf.Max(workspaceScale, 1e-4f);
        for (int g = 0; g < GroupCount; g++)
        {
            if (m_Masked[g]) continue;
            var lim = GetLimits(g);
            var body = m_Hand.Groups[g];
            float d = body != null && cylinderTransform != null ? Vector3.Distance(body.transform.position, cylinderTransform.position) / scale : 0f;
            FillToken(k_GroupParent[g] < 0 ? 0f : (k_GroupParent[g] + 1) / (float)MaxTokens, m_Morph != null ? m_Morph.LinkLength[g] : 0.03f,
                      k_GroupFinger[g] / 4f, k_GroupSegment[g] / 2f, lim, g, GetGroupAngle(g), m_Hand.GroupVelocity(g), m_Setpoint[g], Mathf.Clamp(d, 0f, 1.5f));
            m_TokenSensor.AppendObservation(m_Token);
        }
        for (int w = 0; w < 2; w++)
        {
            var lim = GetArmLimits(3 + w);
            float d = m_Palm != null && cylinderTransform != null ? Vector3.Distance(m_Palm.position, cylinderTransform.position) / scale : 0f;
            FillToken(0f, m_Morph != null ? m_Morph.HandSpan : 0.19f, 5f / 4f, w / 2f, lim, GroupCount + w, GetArmAngle(3 + w), m_Hand.ArmVelocity(3 + w), m_WristSetpoint[w], Mathf.Clamp(d, 0f, 1.5f));
            m_TokenSensor.AppendObservation(m_Token);
        }
    }

    void FillToken(float parent, float length, float fingerCode, float segCode, Vector2 lim, int morphIndex, float angle, float vel, float setpoint, float dist)
    {
        float rad = angle * Mathf.Deg2Rad;
        float k = m_Morph != null ? m_Morph.stiffness[morphIndex] : 1f, I = m_Morph != null ? m_Morph.inertia[morphIndex] : 1e-3f;
        m_Token[0] = parent;
        m_Token[1] = length / 0.06f;
        m_Token[2] = fingerCode;
        m_Token[3] = segCode;
        m_Token[4] = lim.x / 90f;
        m_Token[5] = lim.y / 90f;
        m_Token[6] = Mathf.Log10(Mathf.Max(k, 1e-6f)) / 3f;   // physical k 0.1..15 N m/rad -> -0.33..0.39
        m_Token[7] = (Mathf.Log10(Mathf.Max(I, 1e-10f)) + 6f) / 2f;   // human-scale subtree inertia 1e-7..1e-4 kg m^2 -> [-0.5, 1]
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
        if (m_Hand == null || !m_Hand.Built) return;
        var a = actions.ContinuousActions;
        float dt = Time.fixedDeltaTime;
        m_LimitEvents = 0; m_SaturationEvents = 0;
        float sumA2 = 0f;
        for (int i = 0; i < a.Length; i++) sumA2 += a[i] * a[i];

        // arm: shoulder flexion / abduction, elbow as velocity drives; wrist flexion / pronation as spring targets
        for (int i = 0; i < ArmAxisCount && GroupCount + i < a.Length; i++)
        {
            var lim = GetArmLimits(i);
            if (i < 3)
            {
                if (!actuateProximalJoints) { m_Hand.SetArmVelocity(i, 0f); continue; }
                float v = a[GroupCount + i] * armRotationSpeed, ang = GetArmAngle(i);
                if ((ang <= lim.x + 0.5f && v < 0f) || (ang >= lim.y - 0.5f && v > 0f)) { m_LimitEvents++; v = 0f; }
                m_Hand.SetArmVelocity(i, v);
            }
            else
            {
                int w = i - 3;
                float sp = m_WristSetpoint[w] + a[GroupCount + i] * setpointRateDegPerSec * dt;
                if (sp < lim.x || sp > lim.y) { m_LimitEvents++; sp = Mathf.Clamp(sp, lim.x, lim.y); }
                m_WristSetpoint[w] = sp; m_Hand.SetWristTarget(w, sp);
            }
        }
        // fingers: delta-target on the spring drives; masked groups hold neutral
        for (int g = 0; g < GroupCount; g++)
        {
            if (m_Masked[g]) continue;
            var lim = GetLimits(g);
            float sp = m_Setpoint[g] + a[g] * setpointRateDegPerSec * dt;
            if (sp < lim.x || sp > lim.y) { m_LimitEvents++; sp = Mathf.Clamp(sp, lim.x, lim.y); }
            m_Setpoint[g] = sp; m_Hand.SetGroupTarget(g, sp);
        }
        m_EpisodeLimitEvents += m_LimitEvents;
        var health = m_Hand.Health(); if (health.maxJointSpeedDeg > m_MaxJointSpeed) m_MaxJointSpeed = health.maxJointSpeedDeg;

        // ---- contacts (real collisions relayed by the links during the last physics step) ----
        int contacts = 0; bool thumbTouching = false; int fingerMask = 0; bool palmTouching = false, forearmTouching = false;
        for (int g = 0; g < GroupCount; g++) m_GroupTouching[g] = false;
        m_Contacts.Clear(); float impulseSum = 0f; Vector3 impulseVec = Vector3.zero;
        foreach (var c in m_Hand.Contacts)
        {
            if (c.separation > contactDistance) continue;
            impulseSum += c.impulse; impulseVec += c.normal * c.impulse;
            if (c.group >= 0) m_GroupTouching[c.group] = true; else if (c.isPalm) palmTouching = true; else if (c.isForearm) forearmTouching = true;
            var s = MakeSample(c.point); s.isThumb = c.group >= 0 && k_GroupFinger[c.group] == k_ThumbFinger; s.isPalm = c.isPalm; s.impulse = c.impulse;
            if (c.group >= 0 || c.isPalm) m_Contacts.Add(s);
        }
        m_Hand.ClearContacts();
        float gripTorque = 0f;
        for (int g = 0; g < GroupCount; g++)
        {
            if (m_GroupTouching[g]) { contacts++; int f = k_GroupFinger[g]; if (f == k_ThumbFinger) thumbTouching = true; else fingerMask |= 1 << f; gripTorque += Mathf.Abs(m_Hand.GroupDriveForce(g)); }
            if (!m_Masked[g] && Mathf.Abs(m_Hand.GroupDriveForce(g)) >= 0.98f * m_Hand.forceLimits[g < 12 ? g % 3 : (g == 12 ? 3 : 4)]) m_SaturationEvents++;
        }
        m_EpisodeSaturationEvents += m_SaturationEvents;
        CurrentContacts = contacts; GripForce = gripTorque; FrictionForce = impulseSum / dt; NetFrictionForce = impulseVec / dt; NetSqueezeForce = Vector3.zero;
        m_GripForceSum += gripTorque; m_GripForceSteps++;

        // ---- shaping (potential-based, normalized per episode) ----
        float shapingSum = 0f, residual = 0f;
        foreach (var col in segmentColliders)
        {
            float currDist = SegmentDistance(col);
            float delta = previousDistances[col] - currDist;
            shapingSum += distanceRewardScale * delta / initialDistances[col];
            previousDistances[col] = currDist;
            residual += currDist;
        }
        ResidualSegmentDistance = residual;
        float graspDist = GraspPointDistance;
        shapingSum += palmDistanceRewardScale * (previousGraspPointDistance - graspDist) / initialGraspPointDistance;
        previousGraspPointDistance = graspDist;
        if (graspDist < m_MinGraspPointDistance) m_MinGraspPointDistance = graspDist;
        float shaping = shapingSum * shapingScale;
        float penalty = MaxStep > 0 ? -existentialPenaltyScale / MaxStep : 0f;
        m_ShapingReturn += shaping; m_PenaltyReturn += penalty;
        float effort = -effortWeight * sumA2;
        float safety = -safetyWeight * m_LimitEvents;
        m_EffortReturn += effort; m_SafetyReturn += safety;
        AddReward(shaping + penalty + effort + safety);

        // penetration statistic (object into the finger segments)
        if (cylinderCollider != null)
            foreach (var col in segmentColliders)
                if (Physics.ComputePenetration(col, col.transform.position, col.transform.rotation, cylinderCollider, cylinderTransform.position, cylinderTransform.rotation, out _, out float depth) && depth > m_MaxPenetration) m_MaxPenetration = depth;

        // ---- gate (unchanged): >= N segments, >= M distinct fingers, thumb touching; since run 011 the phase 1 -> 2 transition ----
        int distinctFingers = CountBits(fingerMask);
        bool grasp = contacts >= requiredContactSegments && distinctFingers >= requiredDistinctFingers && (!requireThumbContact || thumbTouching);
        if (contacts >= requiredContactSegments && m_StepsToFirstSixContacts < 0) m_StepsToFirstSixContacts = StepCount;
        if (grasp && m_StepsToFirstHoldCriterion < 0) m_StepsToFirstHoldCriterion = StepCount;
        ComputeContactStats(thumbTouching);
        LastPalmTouching = palmTouching; LastForearmTouching = forearmTouching; LastDistinctFingers = distinctFingers; LastThumbTouching = thumbTouching; LastHoldCriterionMet = grasp;
        if (grasp) { m_HoldWindowCount++; m_HoldWindowSum += contacts; m_HoldWindowSumSq += (double)contacts * contacts; }
        else { m_HoldWindowCount = 0; m_HoldWindowSum = m_HoldWindowSumSq = 0; }

        // ---- phase machine ----
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
                    SuccessCount++; AddReward(successBonus); m_BonusReturn += successBonus; m_EndReason = "success";
                    LogEpisode(true); EndEpisode(); return;
                }
            }
            else if (m_Phase == TaskPhase.Hold) { m_Phase = TaskPhase.Lift; m_HoldSteps = 0; m_PulseStart = new int[0]; }
        }
    }

    ContactSample MakeSample(Vector3 point)
    {
        var s = new ContactSample { point = point };
        if (cylinderTransform == null) return s;
        Vector3 radial = point - cylinderTransform.position; radial -= Vector3.Dot(radial, cylinderTransform.up) * cylinderTransform.up;
        Vector3 local = cylinderTransform.InverseTransformDirection(radial);
        float az = Mathf.Atan2(local.z, local.x) * Mathf.Rad2Deg; if (az < 0f) az += 360f;
        s.azimuthDeg = az; s.height = Vector3.Dot(point - cylinderTransform.position, cylinderTransform.up); s.normal = radial.sqrMagnitude > 1e-12f ? radial.normalized : Vector3.zero;
        return s;
    }

    // ---- lift task mechanics (unchanged from the kinematic run-011 definition) ----
    int HoldStepsNeeded() => Mathf.Max(1, (HoldDecisions > 0 ? HoldDecisions : requiredHoldDecisions) * m_DecisionPeriod);

    void ReleaseObject()
    {
        m_ObjectDynamic = true;
        if (m_CylRb == null) return;
        m_CylRb.mass = m_Mass; m_CylRb.interpolation = RigidbodyInterpolation.None; m_CylRb.constraints = RigidbodyConstraints.None;
        m_CylRb.isKinematic = false; m_CylRb.useGravity = true;
        m_CylRb.linearVelocity = Vector3.zero; m_CylRb.angularVelocity = Vector3.zero; m_CylRb.WakeUp();
        m_ObjectUp0 = cylinderTransform.up;
    }
    public void DiagnosticForceTransition() { if (m_Phase != TaskPhase.Reach) return; m_Phase = TaskPhase.Lift; m_TransitionStep = StepCount; ReleaseObject(); }
    public void DiagnosticSetMass(float kg) { m_Mass = Mathf.Max(1e-3f, kg); m_MassObs = massRange.y > massRange.x ? Mathf.Clamp01(Mathf.Log(m_Mass / massRange.x) / Mathf.Log(massRange.y / massRange.x)) : 0f; if (m_CylRb != null) m_CylRb.mass = m_Mass; }
    public void DiagnosticSetPerturbScale(float scale) { m_PerturbScale = Mathf.Max(0f, scale); }
    bool IsLifted() => cylinderCollider != null && m_ObjectDynamic && cylinderCollider.bounds.min.y >= m_PlatformTop + liftClearance;
    bool IsDropped()
    {
        if (cylinderCollider == null || m_CylRb == null) return false;
        if (Vector3.Distance(m_CylRb.position, GraspPoint) > dropDistance) return true;
        if (dropTiltDeg < 180f && Vector3.Angle(cylinderTransform.up, m_ObjectUp0) > dropTiltDeg) return true;
        return cylinderCollider.bounds.min.y < m_PlatformTop - dropBelowPlatform;
    }
    void FailEpisode(string reason) { AddReward(-dropPenalty); m_DropReturn -= dropPenalty; m_EndReason = reason; LogEpisode(false); EndEpisode(); }
    void SchedulePerturbations(int holdNeeded)
    {
        int n = Mathf.Max(0, perturbPulses), len = Mathf.Max(1, perturbPulseSteps);
        m_PulseStart = new int[n]; m_PulseForce = new Vector3[n]; m_PulseTorque = new Vector3[n];
        float peak = m_PerturbScale * perturbPeakWeightRatio * m_Mass * Physics.gravity.magnitude;
        for (int i = 0; i < n; i++)
        {
            m_PulseStart[i] = Random.Range(1, Mathf.Max(2, holdNeeded - len + 1));
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
            m_CylRb.AddForce(m_PulseForce[i], ForceMode.Force); m_CylRb.AddTorque(m_PulseTorque[i], ForceMode.Force);
            float f = m_PulseForce[i].magnitude; if (f > m_MaxPulseForce) m_MaxPulseForce = f;
            if (m_HoldSteps == m_PulseStart[i]) m_PulsesApplied++;
        }
    }

    // ---- contact statistics (diagnostics) ----
    private void ComputeContactStats(bool thumbTouching)
    {
        m_Azimuths.Clear();
        foreach (var c in m_Contacts) m_Azimuths.Add(c.azimuthDeg);
        float largestGap = 360f;
        if (m_Azimuths.Count >= 2)
        {
            m_Azimuths.Sort(); largestGap = 0f;
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
                if (c.isThumb) { ts += Mathf.Sin(r); tc += Mathf.Cos(r); } else { fs += Mathf.Sin(r); fc += Mathf.Cos(r); fingerCount++; }
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
        LastCoverageGapDeg = largestGap; LastAntipodality = antipodal;
    }

    // ---- episode stats ----
    private void LogEpisode(bool success)
    {
        m_EpisodeActive = false;
        float holdStd = 0f;
        if (m_HoldWindowCount > 1) { double mean = m_HoldWindowSum / m_HoldWindowCount; holdStd = (float)System.Math.Sqrt(System.Math.Max(0.0, m_HoldWindowSumSq / m_HoldWindowCount - mean * mean)); }
        float yaw = cylinderTransform != null ? cylinderTransform.eulerAngles.y : 0f;
        float bottom = cylinderCollider != null ? cylinderCollider.bounds.min.y - m_PlatformTop : 0f;
        LastEpisode = new EpisodeRecord
        {
            success = success, endReason = m_EndReason, steps = StepCount, transitionStep = m_TransitionStep, stepsToLift = m_StepsToLift, holdSteps = m_HoldSteps, holdEntries = m_HoldEntries,
            contacts = CurrentContacts, distinctFingers = LastDistinctFingers, pulsesApplied = m_PulsesApplied, holdStepsPaid = m_HoldStepsPaid, thumb = LastThumbTouching, palm = LastPalmTouching, forearm = LastForearmTouching,
            mass = m_Mass, perturbScale = m_PerturbScale, maxPulseForce = m_MaxPulseForce, retShaping = m_ShapingReturn, retPhase = m_PhaseReturn, retHold = m_HoldReturn, retBonus = m_BonusReturn, retDrop = m_DropReturn,
            retPenalty = m_PenaltyReturn, retEffort = m_EffortReturn, bottomAboveTop = bottom, gripForceMean = m_GripForceSteps > 0 ? m_GripForceSum / m_GripForceSteps : 0f, gripForceEnd = GripForce,
            coverageGapDeg = LastCoverageGapDeg, antipodality = LastAntipodality, verticalSpread = LastVerticalSpread, maxPenetration = m_MaxPenetration, maxJointSpeed = m_MaxJointSpeed
        };
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
        rec.Add("Task/ObjectBottomAboveTop", bottom);
        rec.Add("Task/GripTorqueMean", m_GripForceSteps > 0 ? m_GripForceSum / m_GripForceSteps : 0f);
        rec.Add("Task/GripTorqueAtEnd", GripForce);
        rec.Add("Task/MaxJointSpeedDeg", m_MaxJointSpeed);
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
        rec.Add("Grasp/DriveSaturationEvents", m_EpisodeSaturationEvents);
        rec.Add("Grasp/LimitEvents", m_EpisodeLimitEvents);
        if (m_Morph != null)
        {
            float lenMean = 0f; for (int f = 0; f < MorphologyManager.FingerCount; f++) lenMean += m_Morph.lengthScale[f] / MorphologyManager.FingerCount;
            float wMean = 0f; for (int g = 0; g < GroupCount; g++) wMean += m_Morph.NaturalFrequency(g) / GroupCount;
            rec.Add("Morph/LengthScaleMean", lenMean);
            rec.Add("Morph/OmegaMean", wMean);   // derived omega = sqrt(k / I), logged only
            rec.Add("Morph/StiffnessMean", m_Morph.MeanStiffness(0, GroupCount));
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
                    System.IO.File.WriteAllText(statsCsvPath, "episode,success,endReason,steps,stepsToFirstSixContacts,stepsToHoldCriterion,stepsToTransition,stepsToLift,holdSteps,holdEntries,contacts,distinctFingers,thumb,palm,forearm,coverageGapDeg,antipodality,verticalSpread,holdContactStd,minGraspDist,finalGraspDist,residualSegDist,maxPenetration,spawnDistance,cylYaw,mass,perturbScale,pulsesApplied,maxPulseForce,retShaping,retPhase,retHold,retBonus,retDrop,retPenalty,holdStepsPaid,maxJointSpeed\n");
                System.IO.File.AppendAllText(statsCsvPath, string.Join(",", new string[] {
                    CompletedEpisodes.ToString(), success ? "1" : "0", m_EndReason, StepCount.ToString(), m_StepsToFirstSixContacts.ToString(), m_StepsToFirstHoldCriterion.ToString(),
                    m_TransitionStep.ToString(), m_StepsToLift.ToString(), m_HoldSteps.ToString(), m_HoldEntries.ToString(),
                    CurrentContacts.ToString(), LastDistinctFingers.ToString(), LastThumbTouching ? "1" : "0", LastPalmTouching ? "1" : "0", LastForearmTouching ? "1" : "0",
                    LastCoverageGapDeg.ToString("F1"), LastAntipodality.ToString("F3"), LastVerticalSpread.ToString("F4"), holdStd.ToString("F3"),
                    m_MinGraspPointDistance.ToString("F4"), previousGraspPointDistance.ToString("F4"), ResidualSegmentDistance.ToString("F4"), m_MaxPenetration.ToString("F5"),
                    m_SpawnDistance.ToString("F3"), yaw.ToString("F0"), m_Mass.ToString("F3"), m_PerturbScale.ToString("F2"), m_PulsesApplied.ToString(), m_MaxPulseForce.ToString("F2"),
                    m_ShapingReturn.ToString("F4"), m_PhaseReturn.ToString("F3"), m_HoldReturn.ToString("F4"), m_BonusReturn.ToString("F2"), m_DropReturn.ToString("F2"), m_PenaltyReturn.ToString("F4"), m_HoldStepsPaid.ToString(), m_MaxJointSpeed.ToString("F0") }) + "\n");
            }
            catch (System.Exception e) { Debug.LogWarning("[ArmGraspAgent] stats CSV: " + e.Message); }
        }
    }

    // ---- geometry ----
    private float SegmentDistance(Collider col)
    {
        if (cylinderCollider == null) return 0f;
        return Vector3.Distance(col.ClosestPoint(cylinderTransform.position), cylinderCollider.ClosestPoint(col.transform.position));
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
