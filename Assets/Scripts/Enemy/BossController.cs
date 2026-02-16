// BossController.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public enum BossState
{
    Idle,
    Patrol,
    Alert,      // detection delay before engage
    Engaged,    // actively selecting/running attacks
    Movement,   // non-attack movement beat
    Downtime,   // vulnerability window (no attacks)
    Lost,       // player out of range; stop attacks immediately; wait before returning idle/patrol
    Retreat     // damage-threshold reposition
}

public interface IHealthProvider
{
    float Current { get; }
    float Max { get; }
}

public class BossController : MonoBehaviour
{
    [Serializable]
    public class AttackEntry
    {
        public string name;
        public BossAttack attack;
        public float duration = 5f;

        [Tooltip("Used only in WeightedRandom mode.")]
        public float weight = 1f;

        [Header("Optional Conditions")]
        [Range(0f, 1f)] public float minHpFraction = 0f;
        [Range(0f, 1f)] public float maxHpFraction = 1f;
        public float minDistance = 0f;
        public float maxDistance = 9999f;

        [Tooltip("If false, this attack can't be chosen twice in a row in random/sequence selection.")]
        public bool allowRepeat = true;

        [Header("Bullet Attack Settings (used only if 'attack' is BossBulletAttack)")]
        public int patternGroupIndex = 0;
        public bool resetOnBegin = true;
        public bool fireImmediately = true;
        public bool resetOnEnd = true;
    }

    public enum AttackSelectMode { Sequence, WeightedRandom }
    public enum OrchestrationMode
    {
        ChanceOnly,                 // original behavior + optional movement/downtime chance
        ScriptedSequenceOnly,       // run only scripted steps
        ScriptedThenChanceFallback  // try scripted steps, fallback to chance + normal selection
    }

    public enum MovementMode { Stationary, SideToSideAnchor, PlatformPatrol, JumpArc, JumpToHigherPlatform, HoverSine, Chaser, RepositionAroundPlayer }
    public enum RetreatMode { AwayFromPlayer, RandomAroundPlayer, NearestPatrolPoint }

    public enum SequenceActionType
    {
        Attack,
        Movement,
        Downtime,
        Retreat,
        Wait
    }

    public enum HealthGateMode
    {
        None,
        AtOrBelow,  // hp <= gateA
        AtOrAbove,  // hp >= gateA
        Between     // gateMin <= hp <= gateMax
    }

    public enum OnGateFail
    {
        SkipStep,
        StopSequence
    }

    [Serializable]
    public class PatrolSettings
    {
        public bool enabled = false;
        public List<Transform> waypoints = new List<Transform>();
        public float moveSpeed = 2.5f;
        public float arriveDistance = 0.15f;
        public float waitAtPoint = 0.25f;
        public bool loop = true;
        public bool pingPong = false;
    }



    [Serializable]
    public class MovementSettings
    {
        public bool enabled = true;
        [Range(0f, 1f)] public float chanceAfterAttack = 0.35f;
        public float duration = 1.2f;
        public float cooldown = 0.75f;

        public MovementMode mode = MovementMode.SideToSideAnchor;

        [Header("General")]
        [Tooltip("Keeps movement in XY plane and restores original Z.")]
        public bool lockZ = true;

        [Header("One-Way Platform Interaction")]
        [Tooltip("Layer name used for one-way platforms.")]
        public string oneWayPlatformLayerName = "OneWayPlatform";
        [Tooltip("Ignore one-way platform collisions during HoverSine movement.")]
        public bool ignoreOneWayDuringHoverSine = true;
        [Tooltip("Ignore one-way platform collisions during jump movement modes.")]
        public bool ignoreOneWayDuringJumpModes = true;

        [Header("SideToSideAnchor")]
        public float sideAmplitude = 2.0f;
        public float sideFrequency = 1.25f;
        public float sideMoveSpeed = 8f;

        [Header("PlatformPatrol")]
        public float patrolHalfWidth = 3.0f;
        public float patrolSpeed = 3.0f;
        public bool patrolStartRight = true;
        public float patrolEdgePause = 0.1f;

        [Header("JumpArc")]
        public float jumpDistance = 1.8f;
        public float jumpHeight = 1.0f;
        public float jumpDuration = 0.38f;
        public float jumpInterval = 0.65f;
        public bool jumpTowardsPlayer = true;
        public bool jumpHorizontalOnly = true;

        [Header("JumpToHigherPlatform")]
        [Tooltip("Layer mask used to find landing platforms above.")]
        public LayerMask jumpLandingMask = ~0;
        [Tooltip("Minimum vertical gain required for a valid landing.")]
        public float jumpHigherMinRise = 0.5f;
        [Tooltip("Maximum vertical gain allowed for a valid landing.")]
        public float jumpHigherMaxRise = 4f;
        public float jumpHigherForwardMin = 1f;
        public float jumpHigherForwardMax = 3f;
        [Range(2, 16)] public int jumpHigherCandidateCount = 6;
        [Tooltip("Small offset above landing hit point to avoid clipping.")]
        public float jumpLandingClearance = 0.05f;
        [Tooltip("Fallback to regular JumpArc when no higher landing is found.")]
        public bool jumpFallbackToArcIfNoHigherPlatform = true;

        [Header("HoverSine")]
        [Tooltip("Constant vertical lift while hovering so enemy isn't glued to the platform.")]
        public float hoverBaseLift = 0.8f;
        [Tooltip("Horizontal side-to-side width of HoverSine. Increase this for wider strafing.")]
        public float hoverSideAmplitude = 2.5f;
        [Tooltip("Horizontal oscillation speed for HoverSine.")]
        public float hoverSideFrequency = 1.0f;
        [Tooltip("If true, performs a short jump-arc up to hoverBaseLift before hover motion starts.")]
        public bool hoverLiftWithJump = true;
        [Tooltip("Duration of the initial lift jump.")]
        public float hoverLiftJumpDuration = 0.22f;
        [Tooltip("Extra arc height added during the lift jump.")]
        public float hoverLiftJumpHeight = 0.45f;
        [Tooltip("Vertical bob height for HoverSine.")]
        public float hoverAmplitude = 0.6f;
        [Tooltip("Vertical bob speed for HoverSine.")]
        public float hoverFrequency = 1.2f;
        [Tooltip("When true, snap directly to the hover curve to avoid laggy circle/orbit behavior.")]
        public bool hoverUseDirectPosition = true;
        [Tooltip("Only used if hoverUseDirectPosition is false.")]
        public float hoverMoveSpeed = 5f;
        [Tooltip("Temporarily set Rigidbody2D.gravityScale to 0 while HoverSine is active.")]
        public bool disableGravityDuringHoverSine = true;
        [Header("HoverSine Direct Position Clamp")]
        [Tooltip("If true and hoverUseDirectPosition is enabled, cap movement speed to avoid snapping.")]
        public bool hoverClampDirectSpeed = true;
        public float hoverDirectMaxSpeed = 6f;

        [Header("Collision Safe Movement")]
        [Tooltip("Sweep movement against solids so boss does not phase through walls.")]
        public bool useCollisionSafeMovement = true;
        public LayerMask solidCollisionMask = ~0;
        public float collisionSkin = 0.02f;
        [Tooltip("Ignore cast hits at distance ~0 so floor contact doesn't freeze movement.")]
        public bool ignoreInitialOverlapHits = true;


        [Header("Chaser")]
        public float chaseSpeed = 4.2f;
        public bool chaseHorizontalOnly = true;
        public float chaseStopDistance = 2.4f;
        public float chaseDeadZone = 0.3f;

        [Header("RepositionAroundPlayer")]
        public float repositionMinRadius = 2.5f;
        public float repositionMaxRadius = 4.5f;
        public float repositionMoveSpeed = 6f;
        public float arriveDistance = 0.2f;
        [Tooltip("If true, pick one target on enter and stick to it.")]
        public bool repositionSingleTarget = true;
        [Tooltip("Limit how far above/below current Y a reposition target can be.")]
        public float repositionMaxYOffset = 2.0f;
    }

    [Serializable]
    public class DowntimeSettings
    {
        public bool enabled = true;
        [Range(0f, 1f)] public float chanceAfterAttack = 0.30f;
        public float duration = 1.0f;
    }

    [Serializable]
    public class RetreatSettings
    {
        public bool enabled = true;
        [Tooltip("Trigger when this much damage has accumulated since the last retreat trigger reset.")]
        public float damageThreshold = 25f;
        [Tooltip("Minimum time between retreat triggers.")]
        public float cooldown = 4f;
        [Range(0f, 1f)] public float onlyBelowHpFraction = 1f;

        public float duration = 1.0f;
        public float moveSpeed = 8f;
        public float distance = 4f;
        public float arriveDistance = 0.2f;

        public RetreatMode mode = RetreatMode.AwayFromPlayer;
        public float postRetreatDowntime = 0.6f;
    }

    [Serializable]
    public class SequenceStep
    {
        public SequenceActionType action = SequenceActionType.Attack;

        [Header("Attack")]
        [Tooltip("Index into 'attacks' list when action = Attack.")]
        public int attackIndex = 0;

        [Header("Movement")]
        [Tooltip("Used when action = Movement. Uses shared tuning from the Movement section.")]
        public MovementMode movementMode = MovementMode.SideToSideAnchor;

        [Header("Timing Override")]
        [Tooltip("-1 = use default duration for this action.")]
        public float durationOverride = -1f;

        [Header("Health Gate")]
        public HealthGateMode healthGate = HealthGateMode.None;
        [Range(0f, 1f)] public float gateA = 1f;
        [Range(0f, 1f)] public float gateB = 0f;
        public OnGateFail onGateFail = OnGateFail.SkipStep;
    }

    [Header("State")]
    public BossState currentState = BossState.Idle;
    public bool canPatrol = false;

    [Header("Player & Detection")]
    [SerializeField] private Transform player;
    public float detectionDelay = 1.5f;
    public float playerLostDelay = 2f;
    public float rotationSpeed = 5f;

    [Header("Attacks (any count)")]
    public List<AttackEntry> attacks = new List<AttackEntry>();

    [Header("Optional Health Provider (for HP thresholds)")]
    [SerializeField] private MonoBehaviour healthProviderBehaviour;

    [Header("Attack Selection")]
    public AttackSelectMode selectMode = AttackSelectMode.WeightedRandom;
    public bool resetSequenceOnEngage = true;

    [Header("Orchestration")]
    public OrchestrationMode orchestrationMode = OrchestrationMode.ChanceOnly;
    public bool resetScriptedSequenceOnEngage = true;
    public bool loopScriptedSequence = true;
    public List<SequenceStep> scriptedSequence = new List<SequenceStep>();

    [Header("Sequencer Pacing")]
    [Tooltip("Pause inserted after each completed scripted sequence step (Attack/Movement/Downtime/Retreat/Wait).")]
    public bool useThinkingPauseBetweenScriptedSteps = true;
    [Tooltip("Also insert the same pause between beats in ChanceOnly orchestration mode.")]
    public bool useThinkingPauseBetweenChanceBeats = true;
    [Min(0f), Tooltip("Seconds to pause between beats so players can react/reposition.")]
    public float thinkingPauseDuration = 0.75f;

    [Header("Patrol")]
    public PatrolSettings patrol = new PatrolSettings();

    [Header("Movement")]
    public MovementSettings movement = new MovementSettings();

    [Header("Downtime")]
    public DowntimeSettings downtime = new DowntimeSettings();

    [Header("Retreat")]
    public RetreatSettings retreat = new RetreatSettings();

    [Header("Presentation")]
    [SerializeField] private SpriteRenderer sprite;
    [Tooltip("Prevents 'rolling' jitter on circular enemies.")]
    public bool keepUpright2D = true;

    [Header("Facing Gizmo")]
    public bool drawFacingGizmo = true;
    public float facingRayLength = 1.5f;
    [Range(1f, 180f)] public float facingFovDegrees = 50f;
    [Tooltip("Ignore tiny left/right deltas when updating facing.")]
    public float facingDeadzone = 0.02f;


    // Runtime
    private IHealthProvider healthProvider;
    private Rigidbody2D rb2D;
    private bool hoverGravityOverridden = false;
    private float savedGravityScale = 1f;
    private readonly RaycastHit2D[] moveHits = new RaycastHit2D[16];


    private bool isPlayerInRange;
    private int playerColliderCount;

    private float alertTimer;
    private float lostTimer;

    private BossAttack activeAttack;
    private int activeAttackIndex = -1;
    private float attackTimer;

    // Attack selection cursor
    private int sequenceCursor = -1;

    // Scripted sequence cursor
    private int scriptedCursor = 0;

    // Patrol runtime
    private int patrolIndex = 0;
    private int patrolDir = 1;
    private float patrolWaitTimer = 0f;

    // Movement runtime
    private float movementTimer = 0f;
    private float movementDurationTotal = 0f;
    private float movementCooldownUntil = -999f;
    private float movementElapsed = 0f;
    private MovementMode activeMovementMode;
    private Vector3 movementAnchor;
    private Vector3 movementTarget;
    private bool movementHasTarget;
    private int movementPatrolDir = 1;
    private float movementEdgePauseTimer = 0f;

    // Jump runtime
    private bool jumpActive = false;
    private float jumpTimer = 0f;
    private float nextJumpAt = 0f;
    private Vector3 jumpStart;
    private Vector3 jumpEnd;
    private int jumpDirSign = 1;

    // Hover runtime
    private bool hoverLiftActive = false;
    private float hoverLiftTimer = 0f;
    private float hoverWaveElapsed = 0f;
    private Vector3 hoverLiftStart;
    private Vector3 hoverLiftEnd;

    // Facing runtime
    private Vector2 facingDirection = Vector2.right;

    // Downtime runtime
    private float downtimeTimer = 0f;
    private bool downtimeIsThinkingPause = false;

    // Sequencer runtime
    private bool currentBeatWasScripted = false;
    private bool currentBeatWasChance = false;

    // Retreat runtime
    private float retreatTimer = 0f;
    private Vector3 retreatTarget;
    private float retreatCooldownUntil = -999f;
    private float accumulatedDamage = 0f;
    private float lastKnownHealth = -1f;

    // One-way platform collision handling (per-instance)
    private Collider2D[] selfColliders;
    private readonly List<Collider2D> ignoredOneWayColliders = new List<Collider2D>();
    private bool isIgnoringOneWayPlatforms = false;
    private int cachedOneWayLayer = -1;

    // Cached collider half-height for landing placement
    private float cachedBodyHalfHeight = -1f;

    public Transform PlayerTransform => player;

    private void Start()
    {
        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player")?.transform;

        healthProvider = healthProviderBehaviour as IHealthProvider;
        rb2D = GetComponent<Rigidbody2D>();
        selfColliders = GetComponentsInChildren<Collider2D>();
        cachedOneWayLayer = ResolveOneWayLayer();
        cachedBodyHalfHeight = ComputeBodyHalfHeight();

        if (healthProvider != null)
            lastKnownHealth = healthProvider.Current;

        EnterState(canPatrol ? BossState.Patrol : BossState.Idle);
    }

    private void Update()
    {
        FacePlayerIfInRange();
        KeepUpright();

        // Damage tracking for retreat trigger
        TrackDamageTaken();

        switch (currentState)
        {
            case BossState.Idle:
                TickIdle();
                break;

            case BossState.Patrol:
                TickPatrol(Time.deltaTime);
                if (isPlayerInRange)
                    EnterAlert();
                break;

            case BossState.Alert:
                TickAlert(Time.deltaTime);
                break;

            case BossState.Engaged:
                TickEngaged(Time.deltaTime);
                break;

            case BossState.Movement:
                TickMovement(Time.deltaTime);
                break;

            case BossState.Downtime:
                TickDowntime(Time.deltaTime);
                break;

            case BossState.Lost:
                TickLost(Time.deltaTime);
                break;

            case BossState.Retreat:
                TickRetreat(Time.deltaTime);
                break;
        }
    }

    private void OnDisable()
    {
        SetIgnoreOneWayPlatforms(false);
        SetHoverGravityOverride(false);
    }

    private void OnDestroy()
    {
        SetIgnoreOneWayPlatforms(false);
        SetHoverGravityOverride(false);
    }

    private void SetHoverGravityOverride(bool enable)
    {
        if (rb2D == null) return;

        if (enable)
        {
            if (!hoverGravityOverridden)
            {
                savedGravityScale = rb2D.gravityScale;
                hoverGravityOverridden = true;
            }

            rb2D.gravityScale = 0f;

            // Clear existing downward momentum from pre-hover movement.
            Vector2 v = rb2D.linearVelocity;
            if (v.y < 0f) v.y = 0f;
            rb2D.linearVelocity = v;
        }
        else
        {
            if (!hoverGravityOverridden) return;
            rb2D.gravityScale = savedGravityScale;
            hoverGravityOverridden = false;
        }
    }

    private void TickIdle()
    {
        if (isPlayerInRange)
        {
            EnterAlert();
            return;
        }

        if (canPatrol && patrol.enabled && patrol.waypoints != null && patrol.waypoints.Count > 0)
            EnterState(BossState.Patrol);
    }

    private void TickPatrol(float dt)
    {
        if (!canPatrol || !patrol.enabled || patrol.waypoints == null || patrol.waypoints.Count == 0)
        {
            EnterState(BossState.Idle);
            return;
        }

        if (patrolWaitTimer > 0f)
        {
            patrolWaitTimer -= dt;
            return;
        }

        Transform wp = patrol.waypoints[patrolIndex];
        if (wp == null)
        {
            AdvancePatrolIndex();
            return;
        }

        Vector3 pos = transform.position;
        Vector3 target = new Vector3(wp.position.x, wp.position.y, pos.z);
        Vector3 next = Vector3.MoveTowards(pos, target, Mathf.Max(0f, patrol.moveSpeed) * dt);
        transform.position = next;
        SetFacingFromDeltaX(next.x - pos.x);

        if (Vector2.Distance(next, target) <= Mathf.Max(0.001f, patrol.arriveDistance))
        {
            patrolWaitTimer = Mathf.Max(0f, patrol.waitAtPoint);
            AdvancePatrolIndex();
        }

        if (isPlayerInRange)
            EnterAlert();
    }

    private void AdvancePatrolIndex()
    {
        if (patrol.waypoints == null || patrol.waypoints.Count == 0) return;

        if (patrol.pingPong && patrol.waypoints.Count > 1)
        {
            patrolIndex += patrolDir;
            if (patrolIndex >= patrol.waypoints.Count)
            {
                patrolIndex = patrol.waypoints.Count - 2;
                patrolDir = -1;
            }
            else if (patrolIndex < 0)
            {
                patrolIndex = 1;
                patrolDir = 1;
            }
        }
        else
        {
            patrolIndex++;
            if (patrolIndex >= patrol.waypoints.Count)
                patrolIndex = patrol.loop ? 0 : patrol.waypoints.Count - 1;
        }
    }

    private void TickAlert(float dt)
    {
        if (!isPlayerInRange)
        {
            EnterState(canPatrol ? BossState.Patrol : BossState.Idle);
            return;
        }

        alertTimer -= dt;
        if (alertTimer <= 0f)
            EnterEngaged();
    }

    private void TickEngaged(float dt)
    {
        if (!isPlayerInRange)
        {
            EnterLost();
            return;
        }

        // Retreat pre-emption can interrupt between beats or while attacking.
        if (ShouldTriggerRetreat())
        {
            StopActiveAttack(reset: true);
            EnterRetreat();
            return;
        }

        TickActiveAttack(dt);
    }

    private void TickLost(float dt)
    {
        if (isPlayerInRange)
        {
            EnterAlert();
            return;
        }

        lostTimer -= dt;
        if (lostTimer <= 0f)
            EnterState(canPatrol ? BossState.Patrol : BossState.Idle);
    }


    private void TickMovement(float dt)
    {
        if (!isPlayerInRange)
        {
            EnterLost();
            return;
        }

        movementTimer -= dt;
        movementElapsed += dt;

        switch (activeMovementMode)
        {
            case MovementMode.Stationary:
                // Intentionally no movement (readable "breather" beat)
                break;

            case MovementMode.SideToSideAnchor:
                {
                    float t = (movementDurationTotal <= 0f) ? 1f : 1f - Mathf.Clamp01(movementTimer / movementDurationTotal);
                    float x = Mathf.Sin(t * Mathf.PI * 2f * Mathf.Max(0f, movement.sideFrequency)) * movement.sideAmplitude;
                    Vector3 target = movementAnchor + new Vector3(x, 0f, 0f);
                    MoveTowardsPosition(target, Mathf.Max(0f, movement.sideMoveSpeed), dt);
                    SetFacingFromDeltaX(target.x - transform.position.x);
                    break;
                }

            case MovementMode.PlatformPatrol:
                {
                    if (movementEdgePauseTimer > 0f)
                    {
                        movementEdgePauseTimer -= dt;
                        break;
                    }

                    float half = Mathf.Max(0f, movement.patrolHalfWidth);
                    float left = movementAnchor.x - half;
                    float right = movementAnchor.x + half;

                    Vector3 pos = transform.position;
                    float nx = pos.x + movementPatrolDir * Mathf.Max(0f, movement.patrolSpeed) * dt;

                    if (nx >= right)
                    {
                        nx = right;
                        movementPatrolDir = -1;
                        movementEdgePauseTimer = Mathf.Max(0f, movement.patrolEdgePause);
                    }
                    else if (nx <= left)
                    {
                        nx = left;
                        movementPatrolDir = 1;
                        movementEdgePauseTimer = Mathf.Max(0f, movement.patrolEdgePause);
                    }

                    Vector3 target = new Vector3(nx, movementAnchor.y, pos.z);
                    ApplyPosition(target);
                    SetFacingFromDeltaX(movementPatrolDir);
                    break;
                }

            case MovementMode.JumpArc:
            case MovementMode.JumpToHigherPlatform:
                {
                    TickJumpArc(dt);
                    break;
                }

            case MovementMode.HoverSine:
                {
                    if (hoverLiftActive)
                    {
                        TickHoverLift(dt);
                        break;
                    }

                    hoverWaveElapsed += dt;
                    float t = hoverWaveElapsed;
                    float x = Mathf.Sin(t * Mathf.PI * 2f * Mathf.Max(0f, movement.hoverSideFrequency)) * movement.hoverSideAmplitude;
                    float y = movement.hoverBaseLift +
                              Mathf.Sin(t * Mathf.PI * 2f * Mathf.Max(0f, movement.hoverFrequency)) * movement.hoverAmplitude;

                    Vector3 target = movementAnchor + new Vector3(x, y, 0f);

                    if (movement.hoverUseDirectPosition)
                    {
                        if (movement.hoverClampDirectSpeed)
                            MoveTowardsPosition(target, Mathf.Max(0f, movement.hoverDirectMaxSpeed), dt);
                        else
                            ApplyPosition(target);
                    }
                    else
                    {
                        MoveTowardsPosition(target, Mathf.Max(0f, movement.hoverMoveSpeed), dt);
                    }


                    if (movement.disableGravityDuringHoverSine && rb2D != null)
                    {
                        Vector2 v = rb2D.linearVelocity;
                        if (v.y != 0f)
                        {
                            v.y = 0f;
                            rb2D.linearVelocity = v;
                        }
                    }

                    // Face travel direction of the hover wave itself.
                    float dxWave = Mathf.Cos(t * Mathf.PI * 2f * Mathf.Max(0f, movement.hoverSideFrequency)) * movement.hoverSideAmplitude;
                    SetFacingFromDeltaX(dxWave);
                    break;
                }

            case MovementMode.Chaser:
                {
                    if (player == null) break;

                    Vector3 pos = transform.position;
                    Vector3 toPlayer = player.position - pos;

                    if (movement.chaseHorizontalOnly)
                        toPlayer.y = 0f;

                    float dist = toPlayer.magnitude;
                    if (dist <= 0.0001f) break;

                    Vector3 dir = toPlayer / dist;
                    float speed = Mathf.Max(0f, movement.chaseSpeed);
                    float desired = Mathf.Max(0f, movement.chaseStopDistance);
                    float deadZone = Mathf.Max(0f, movement.chaseDeadZone);

                    Vector3 next = pos;
                    if (dist > desired + deadZone)
                    {
                        next = pos + dir * speed * dt; // approach
                        SetFacingFromDeltaX(dir.x);
                    }
                    else if (dist < Mathf.Max(0f, desired - deadZone))
                    {
                        next = pos - dir * speed * dt; // back off
                        SetFacingFromDeltaX(-dir.x);
                    }

                    if (movement.chaseHorizontalOnly)
                        next.y = movementAnchor.y;

                    ApplyPosition(next);
                    break;
                }

            case MovementMode.RepositionAroundPlayer:
                {
                    if (movementHasTarget)
                    {
                        Vector3 target = new Vector3(movementTarget.x, movementTarget.y, transform.position.z);
                        MoveTowardsPosition(target, Mathf.Max(0f, movement.repositionMoveSpeed), dt);
                        SetFacingFromDeltaX(target.x - transform.position.x);

                        bool reached = Vector2.Distance(transform.position, target) <= Mathf.Max(0.001f, movement.arriveDistance);
                        if (reached && !movement.repositionSingleTarget)
                            movementTarget = PickRepositionTarget();
                    }
                    break;
                }
        }

        if (movementTimer <= 0f)
            EndMovementAndContinue();
    }



    private void MoveTowardsPosition(Vector3 target, float speed, float dt)
    {
        Vector3 next = Vector3.MoveTowards(transform.position, target, speed * dt);
        ApplyPosition(next);
    }

    private void ApplyPosition(Vector3 p)
    {
        if (movement.lockZ)
            p.z = movementAnchor.z;

        Vector3 current = transform.position;
        Vector3 target = movement.useCollisionSafeMovement
            ? ResolveCollisionSafePosition(current, p)
            : p;

        if (rb2D != null && rb2D.simulated)
            rb2D.MovePosition(target);
        else
            transform.position = target;
    }

    private Vector3 ResolveCollisionSafePosition(Vector3 current, Vector3 desired)
    {
        Vector2 delta = (Vector2)(desired - current);
        float distance = delta.magnitude;
        if (distance <= 0.0001f) return desired;

        Vector2 dir = delta / distance;
        float allowed = distance;
        float skin = Mathf.Max(0f, movement.collisionSkin);

        if (selfColliders == null || selfColliders.Length == 0)
            selfColliders = GetComponentsInChildren<Collider2D>();

        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = false;
        filter.SetLayerMask(movement.solidCollisionMask);

        for (int c = 0; c < selfColliders.Length; c++)
        {
            Collider2D myCol = selfColliders[c];
            if (myCol == null || !myCol.enabled) continue;

            int hitCount = myCol.Cast(dir, filter, moveHits, distance + skin);
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit2D hit = moveHits[i];
                if (hit.collider == null) continue;

                // Respect temporary one-way ignore state
                if (isIgnoringOneWayPlatforms &&
                    cachedOneWayLayer >= 0 &&
                    hit.collider.gameObject.layer == cachedOneWayLayer)
                    continue;

                // Critical: don't let existing floor contact lock movement to zero
                if (movement.ignoreInitialOverlapHits && hit.distance <= 0.0001f)
                    continue;

                float d = Mathf.Max(0f, hit.distance - skin);
                if (d < allowed) allowed = d;
            }
        }

        if (allowed >= distance) return desired;

        Vector3 safe = current + (Vector3)(dir * allowed);
        safe.z = desired.z;
        return safe;
    }


    private Vector3 PickRepositionTarget()
    {
        if (player == null) return transform.position;

        float minR = Mathf.Max(0f, Mathf.Min(movement.repositionMinRadius, movement.repositionMaxRadius));
        float maxR = Mathf.Max(minR, Mathf.Max(movement.repositionMinRadius, movement.repositionMaxRadius));

        Vector2 rnd = UnityEngine.Random.insideUnitCircle;
        if (rnd.sqrMagnitude < 0.0001f) rnd = Vector2.right;
        rnd.Normalize();

        float radius = UnityEngine.Random.Range(minR, maxR);
        Vector2 target2D = (Vector2)player.position + rnd * radius;

        float yMin = movementAnchor.y - Mathf.Abs(movement.repositionMaxYOffset);
        float yMax = movementAnchor.y + Mathf.Abs(movement.repositionMaxYOffset);
        target2D.y = Mathf.Clamp(target2D.y, yMin, yMax);

        return new Vector3(target2D.x, target2D.y, movementAnchor.z);
    }

    private void TickJumpArc(float dt)
    {
        if (!jumpActive)
        {
            if (movementElapsed >= nextJumpAt)
                BeginNextJump();

            return;
        }

        jumpTimer += dt;
        float dur = Mathf.Max(0.01f, movement.jumpDuration);
        float t = Mathf.Clamp01(jumpTimer / dur);

        Vector3 basePos = Vector3.Lerp(jumpStart, jumpEnd, t);
        float arc = 4f * Mathf.Max(0f, movement.jumpHeight) * t * (1f - t); // parabola, apex at t=0.5
        basePos.y += arc;

        ApplyPosition(basePos);

        if (t >= 1f)
        {
            jumpActive = false;
            ApplyPosition(jumpEnd);
            nextJumpAt = movementElapsed + Mathf.Max(0f, movement.jumpInterval);
        }
    }


    private void BeginNextJump()
    {
        jumpActive = true;
        jumpTimer = 0f;

        Vector3 origin = transform.position;
        int sign = ResolveJumpDirection(origin);

        jumpDirSign = sign;
        SetFacingFromDeltaX(sign);

        // Special mode: jump to a detected higher platform if possible.
        if (activeMovementMode == MovementMode.JumpToHigherPlatform)
        {
            if (TryGetHigherPlatformLanding(origin, sign, out Vector3 higherLanding))
            {
                jumpStart = new Vector3(origin.x, origin.y, movementAnchor.z);
                jumpEnd = new Vector3(higherLanding.x, higherLanding.y, movementAnchor.z);
                return;
            }

            if (!movement.jumpFallbackToArcIfNoHigherPlatform)
            {
                jumpActive = false;
                nextJumpAt = movementElapsed + Mathf.Max(0f, movement.jumpInterval);
                return;
            }
        }

        // Standard jump arc behavior
        float horizontal = Mathf.Max(0f, movement.jumpDistance) * sign;
        Vector3 end = origin + new Vector3(horizontal, 0f, 0f);

        if (!movement.jumpHorizontalOnly && player != null)
            end.y = player.position.y;

        end.z = movementAnchor.z;

        jumpStart = new Vector3(origin.x, movementAnchor.y, movementAnchor.z);
        jumpEnd = end;
    }

    private int ResolveJumpDirection(Vector3 origin)
    {
        int sign = (jumpDirSign == 0) ? 1 : jumpDirSign;

        if (movement.jumpTowardsPlayer && player != null)
        {
            float dx = player.position.x - origin.x;
            if (Mathf.Abs(dx) > 0.001f)
                sign = (dx >= 0f) ? 1 : -1;
        }
        else
        {
            sign *= -1; // alternate when not tracking player
        }

        if (sign == 0) sign = 1;
        return sign;
    }

    private bool TryGetHigherPlatformLanding(Vector3 origin, int sign, out Vector3 landing)
    {
        landing = origin;

        int candidateCount = Mathf.Clamp(movement.jumpHigherCandidateCount, 2, 16);
        float minForward = Mathf.Max(0.05f, movement.jumpHigherForwardMin);
        float maxForward = Mathf.Max(minForward, movement.jumpHigherForwardMax);

        float minRise = Mathf.Max(0f, movement.jumpHigherMinRise);
        float maxRise = Mathf.Max(minRise, movement.jumpHigherMaxRise);

        float rayStartY = origin.y + maxRise + 0.5f;
        float rayEndY = origin.y - 1f;
        float rayDistance = rayStartY - rayEndY;

        for (int i = 0; i < candidateCount; i++)
        {
            float t = (candidateCount <= 1) ? 0f : (float)i / (candidateCount - 1);
            float forward = Mathf.Lerp(minForward, maxForward, t);
            float candidateX = origin.x + sign * forward;

            Vector2 rayOrigin = new Vector2(candidateX, rayStartY);
            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, rayDistance, movement.jumpLandingMask);

            if (!hit.collider) continue;
            if (hit.normal.y < 0.5f) continue; // prefer top surfaces

            float landingY = hit.point.y + GetBodyHalfHeight() + Mathf.Max(0f, movement.jumpLandingClearance);
            float rise = landingY - origin.y;

            if (rise < minRise || rise > maxRise)
                continue;

            landing = new Vector3(candidateX, landingY, movementAnchor.z);
            return true;
        }

        return false;
    }

    private float GetBodyHalfHeight()
    {
        if (cachedBodyHalfHeight <= 0f)
            cachedBodyHalfHeight = ComputeBodyHalfHeight();

        return cachedBodyHalfHeight;
    }

    private float ComputeBodyHalfHeight()
    {
        if (selfColliders == null || selfColliders.Length == 0)
            selfColliders = GetComponentsInChildren<Collider2D>();

        float maxHalf = 0.5f;
        if (selfColliders != null)
        {
            for (int i = 0; i < selfColliders.Length; i++)
            {
                Collider2D c = selfColliders[i];
                if (c == null || !c.enabled) continue;
                maxHalf = Mathf.Max(maxHalf, c.bounds.extents.y);
            }
        }
        return maxHalf;
    }

    private bool ShouldIgnoreOneWayForMovement(MovementMode mode)
    {
        if (mode == MovementMode.HoverSine && movement.ignoreOneWayDuringHoverSine)
            return true;

        if ((mode == MovementMode.JumpArc || mode == MovementMode.JumpToHigherPlatform) &&
            movement.ignoreOneWayDuringJumpModes)
            return true;

        return false;
    }

    private int ResolveOneWayLayer()
    {
        string layerName = string.IsNullOrWhiteSpace(movement.oneWayPlatformLayerName)
            ? "OneWayPlatform"
            : movement.oneWayPlatformLayerName;

        return LayerMask.NameToLayer(layerName);
    }

    private void SetIgnoreOneWayPlatforms(bool ignore)
    {
        if (ignore == isIgnoringOneWayPlatforms)
            return;

        if (selfColliders == null || selfColliders.Length == 0)
            selfColliders = GetComponentsInChildren<Collider2D>();

        if (selfColliders == null || selfColliders.Length == 0)
            return;

        if (cachedOneWayLayer < 0)
            cachedOneWayLayer = ResolveOneWayLayer();

        if (cachedOneWayLayer < 0)
            return;

        if (ignore)
        {
            ignoredOneWayColliders.Clear();

            Collider2D[] all = FindObjectsByType<Collider2D>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                Collider2D platformCol = all[i];
                if (platformCol == null) continue;
                if (platformCol.gameObject.layer != cachedOneWayLayer) continue;

                for (int j = 0; j < selfColliders.Length; j++)
                {
                    Collider2D myCol = selfColliders[j];
                    if (myCol == null || !myCol.enabled) continue;
                    Physics2D.IgnoreCollision(myCol, platformCol, true);
                }

                ignoredOneWayColliders.Add(platformCol);
            }

            isIgnoringOneWayPlatforms = true;
        }
        else
        {
            for (int i = 0; i < ignoredOneWayColliders.Count; i++)
            {
                Collider2D platformCol = ignoredOneWayColliders[i];
                if (platformCol == null) continue;

                for (int j = 0; j < selfColliders.Length; j++)
                {
                    Collider2D myCol = selfColliders[j];
                    if (myCol == null || !myCol.enabled) continue;
                    Physics2D.IgnoreCollision(myCol, platformCol, false);
                }
            }

            ignoredOneWayColliders.Clear();
            isIgnoringOneWayPlatforms = false;
        }
    }

        private void BeginHoverLift()
    {
        hoverWaveElapsed = 0f;

        float lift = Mathf.Max(0f, movement.hoverBaseLift);
        if (!movement.hoverLiftWithJump || lift <= 0.0001f)
        {
            hoverLiftActive = false;
            return;
        }

        hoverLiftActive = true;
        hoverLiftTimer = 0f;

        Vector3 now = transform.position;
        hoverLiftStart = now;
        hoverLiftEnd = new Vector3(now.x, movementAnchor.y + lift, now.z);
    }

    private void TickHoverLift(float dt)
    {
        hoverLiftTimer += dt;

        float dur = Mathf.Max(0.01f, movement.hoverLiftJumpDuration);
        float t = Mathf.Clamp01(hoverLiftTimer / dur);

        Vector3 basePos = Vector3.Lerp(hoverLiftStart, hoverLiftEnd, t);
        float arc = 4f * Mathf.Max(0f, movement.hoverLiftJumpHeight) * t * (1f - t);
        basePos.y += arc;

        ApplyPosition(basePos);

        if (t >= 1f)
        {
            hoverLiftActive = false;
            ApplyPosition(hoverLiftEnd);
            hoverWaveElapsed = 0f;
        }
    }

    private void TickDowntime(float dt)
    {
        if (!isPlayerInRange)
        {
            EnterLost();
            return;
        }

        downtimeTimer -= dt;
        if (downtimeTimer > 0f) return;

        // Thinking pause finished -> proceed without inserting another thinking pause.
        if (downtimeIsThinkingPause)
        {
            downtimeIsThinkingPause = false;
            StartNextBeat(onAttackFinished: false);
            return;
        }

        ContinueAfterNonAttackBeat();
    }

    private void TickRetreat(float dt)
    {
        if (!isPlayerInRange)
        {
            EnterLost();
            return;
        }

        retreatTimer -= dt;

        Vector3 target = new Vector3(retreatTarget.x, retreatTarget.y, transform.position.z);
        transform.position = Vector3.MoveTowards(transform.position, target, Mathf.Max(0f, retreat.moveSpeed) * dt);
        SetFacingFromDeltaX(target.x - transform.position.x);

        bool arrived = Vector2.Distance(transform.position, target) <= Mathf.Max(0.001f, retreat.arriveDistance);
        if (arrived || retreatTimer <= 0f)
        {
            if (retreat.postRetreatDowntime > 0f)
                EnterDowntime(retreat.postRetreatDowntime);
            else
                ContinueAfterNonAttackBeat();
        }
    }

    private void KeepUpright()
    {
        if (!keepUpright2D) return;

        if (rb2D != null)
        {
            rb2D.angularVelocity = 0f;
            rb2D.rotation = 0f;
            return;
        }

        Vector3 e = transform.eulerAngles;
        if (Mathf.Abs(e.z) > 0.001f)
            transform.rotation = Quaternion.Euler(0f, 0f, 0f);
    }


    private void FacePlayerIfInRange()
    {
        if (player == null) return;

        // Prefer player-facing when engaged, but preserve movement-facing otherwise.
        if (isPlayerInRange)
            SetFacingFromDeltaX(player.position.x - transform.position.x);
    }

    private void SetFacingFromDeltaX(float dx)
    {
        if (Mathf.Abs(dx) <= Mathf.Max(0f, facingDeadzone)) return;

        facingDirection = (dx >= 0f) ? Vector2.right : Vector2.left;

        if (sprite != null)
            sprite.flipX = dx < 0f;
    }

    private bool IsFacingPlayer(out float dot)
    {
        dot = -1f;
        if (player == null) return false;

        Vector2 toPlayer = ((Vector2)player.position - (Vector2)transform.position);
        if (toPlayer.sqrMagnitude <= 0.0001f) return true;

        Vector2 fwd = (facingDirection.sqrMagnitude <= 0.0001f) ? Vector2.right : facingDirection.normalized;
        dot = Vector2.Dot(fwd, toPlayer.normalized);

        float halfFov = Mathf.Clamp(facingFovDegrees, 1f, 180f) * 0.5f;
        float threshold = Mathf.Cos(halfFov * Mathf.Deg2Rad);
        return dot >= threshold;
    }

    private void EnterState(BossState newState)
    {
        if (newState == currentState) return;

        ExitState(currentState);
        currentState = newState;
        OnEnterState(newState);
    }

    private void OnEnterState(BossState state)
    {
        switch (state)
        {
            case BossState.Alert:
                alertTimer = Mathf.Max(0f, detectionDelay);
                break;

            case BossState.Lost:
                lostTimer = Mathf.Max(0f, playerLostDelay);
                break;

            case BossState.Engaged:
                break;

            case BossState.Patrol:
                patrolWaitTimer = 0f;
                break;
        }
    }

    private void ExitState(BossState state)
    {
        // Ensure active attack is stopped when leaving engagement path.
        if (state == BossState.Engaged || state == BossState.Alert || state == BossState.Lost)
        {
            StopActiveAttack(reset: true);
        }

        if (state == BossState.Movement)
        {
            SetIgnoreOneWayPlatforms(false);
            SetHoverGravityOverride(false);
        }
    }

    private void EnterAlert()
    {
        StopActiveAttack(reset: true);
        activeAttackIndex = -1;
        currentBeatWasScripted = false;
        currentBeatWasChance = false;
        EnterState(BossState.Alert);
    }

    private void EnterEngaged()
    {
        if (resetSequenceOnEngage)
            sequenceCursor = -1;

        if (resetScriptedSequenceOnEngage)
            scriptedCursor = 0;

        currentBeatWasScripted = false;
        currentBeatWasChance = false;
        downtimeIsThinkingPause = false;
        EnterState(BossState.Engaged);
        StartNextBeat(onAttackFinished: false);
    }

    private void EnterLost()
    {
        StopActiveAttack(reset: true);
        currentBeatWasScripted = false;
        currentBeatWasChance = false;
        EnterState(BossState.Lost);
    }



    private void EnterMovement(float durationOverride = -1f)
    {
        EnterMovementWithMode(movement.mode, durationOverride);
    }

    private void EnterMovementWithMode(MovementMode modeOverride, float durationOverride = -1f)
    {
        if (!movement.enabled)
        {
            ContinueAfterNonAttackBeat();
            return;
        }

        activeMovementMode = modeOverride;
        movementTimer = (durationOverride >= 0f) ? durationOverride : movement.duration;
        movementTimer = Mathf.Max(0.05f, movementTimer);
        movementDurationTotal = movementTimer;
        movementCooldownUntil = Time.time + Mathf.Max(0f, movement.cooldown);

        movementAnchor = transform.position;
        movementElapsed = 0f;
        movementHasTarget = false;
        movementPatrolDir = movement.patrolStartRight ? 1 : -1;
        movementEdgePauseTimer = 0f;

        jumpActive = false;
        jumpTimer = 0f;
        nextJumpAt = 0f;
        jumpStart = movementAnchor;
        jumpEnd = movementAnchor;

        hoverLiftActive = false;
        hoverLiftTimer = 0f;
        hoverWaveElapsed = 0f;
        hoverLiftStart = movementAnchor;
        hoverLiftEnd = movementAnchor;

        // Refresh one-way layer and apply mode-specific collision behavior.
        cachedOneWayLayer = ResolveOneWayLayer();
        SetIgnoreOneWayPlatforms(false);
        SetIgnoreOneWayPlatforms(ShouldIgnoreOneWayForMovement(activeMovementMode));
        SetHoverGravityOverride(activeMovementMode == MovementMode.HoverSine && movement.disableGravityDuringHoverSine);

        if (activeMovementMode == MovementMode.RepositionAroundPlayer && player != null)
        {
            movementTarget = PickRepositionTarget();
            movementHasTarget = true;
        }
        else if (activeMovementMode == MovementMode.JumpArc ||
                 activeMovementMode == MovementMode.JumpToHigherPlatform)
        {
            // Start with a jump immediately.
            BeginNextJump();
        }
        else if (activeMovementMode == MovementMode.HoverSine)
        {
            BeginHoverLift();
        }


        EnterState(BossState.Movement);
    }

    private void EndMovementAndContinue()
    {
        ContinueAfterNonAttackBeat();
    }

    private void EnterDowntime(float durationOverride = -1f)
    {
        if (!downtime.enabled)
        {
            ContinueAfterNonAttackBeat();
            return;
        }

        downtimeIsThinkingPause = false;
        downtimeTimer = (durationOverride >= 0f) ? durationOverride : downtime.duration;
        downtimeTimer = Mathf.Max(0.01f, downtimeTimer);
        EnterState(BossState.Downtime);
    }

    private void EnterRetreat(float durationOverride = -1f)
    {
        if (!retreat.enabled)
        {
            ContinueAfterNonAttackBeat();
            return;
        }

        // Retreat triggered outside scripted sequencing should still count as a beat in ChanceOnly mode.
        if (!currentBeatWasScripted && orchestrationMode == OrchestrationMode.ChanceOnly)
            currentBeatWasChance = true;

        if (player == null)
        {
            ContinueAfterNonAttackBeat();
            return;
        }

        retreatTimer = (durationOverride >= 0f) ? durationOverride : retreat.duration;
        retreatTimer = Mathf.Max(0.05f, retreatTimer);

        retreatTarget = ComputeRetreatTarget();
        retreatCooldownUntil = Time.time + Mathf.Max(0f, retreat.cooldown);
        accumulatedDamage = 0f; // consume threshold trigger

        EnterState(BossState.Retreat);
    }

    private Vector3 ComputeRetreatTarget()
    {
        Vector3 origin = transform.position;

        switch (retreat.mode)
        {
            case RetreatMode.AwayFromPlayer:
                {
                    Vector2 away = (origin - player.position);
                    if (away.sqrMagnitude < 0.0001f) away = Vector2.right;
                    away.Normalize();
                    return origin + (Vector3)(away * retreat.distance);
                }

            case RetreatMode.RandomAroundPlayer:
                {
                    Vector2 d = UnityEngine.Random.insideUnitCircle.normalized;
                    if (d.sqrMagnitude < 0.0001f) d = Vector2.right;
                    return player.position + (Vector3)(d * retreat.distance);
                }

            case RetreatMode.NearestPatrolPoint:
                {
                    if (patrol.waypoints != null && patrol.waypoints.Count > 0)
                    {
                        float best = float.MaxValue;
                        Vector3 bestPos = origin;
                        for (int i = 0; i < patrol.waypoints.Count; i++)
                        {
                            var p = patrol.waypoints[i];
                            if (p == null) continue;
                            float d2 = (p.position - origin).sqrMagnitude;
                            if (d2 < best)
                            {
                                best = d2;
                                bestPos = p.position;
                            }
                        }
                        return bestPos;
                    }
                    // fallback
                    return origin;
                }
        }

        return origin;
    }

    private void TickActiveAttack(float dt)
    {
        if (activeAttack == null)
        {
            StartNextBeat(onAttackFinished: false);
            return;
        }

        activeAttack.Tick(this, dt);
        attackTimer -= dt;

        if (attackTimer <= 0f)
        {
            StopActiveAttack(reset: false);

            if (TryEnterThinkingPauseAfterScriptedBeat())
                return;

            StartNextBeat(onAttackFinished: true);
        }
    }

    private void StartNextBeat(bool onAttackFinished)
    {
        // 1) Scripted sequence has priority if enabled.
        if (orchestrationMode == OrchestrationMode.ScriptedSequenceOnly ||
            orchestrationMode == OrchestrationMode.ScriptedThenChanceFallback)
        {
            bool ranScripted = TryRunNextScriptedStep();
            if (ranScripted) return;

            if (orchestrationMode == OrchestrationMode.ScriptedSequenceOnly)
            {
                // No scripted step available: idle in downtime briefly to avoid tight loops.
                EnterDowntime(0.15f);
                return;
            }
        }

        // 2) Chance layer only on transition after an attack finishes.
        if (onAttackFinished && TryRunChanceNonAttackBeat())
            return;

        // 3) Default to selecting next attack.
        StartNextAttackBySelection();
    }

    private void ContinueAfterNonAttackBeat()
    {
        // After non-attack beats (movement/downtime/retreat), continue orchestration.
        if (TryEnterThinkingPauseAfterScriptedBeat())
            return;

        StartNextBeat(onAttackFinished: false);
    }

    private bool TryRunNextScriptedStep()
    {
        if (scriptedSequence == null || scriptedSequence.Count == 0)
            return false;

        int count = scriptedSequence.Count;
        int attempts = 0;

        while (attempts < count)
        {
            if (scriptedCursor >= count)
            {
                if (!loopScriptedSequence) return false;
                scriptedCursor = 0;
            }

            int thisIndex = scriptedCursor;
            SequenceStep step = scriptedSequence[thisIndex];
            scriptedCursor++;
            attempts++;

            if (!PassesHealthGate(step))
            {
                if (step.onGateFail == OnGateFail.StopSequence)
                    return false;

                // SkipStep
                continue;
            }

            if (ExecuteScriptedStep(step))
                return true;
        }

        return false;
    }

    private bool PassesHealthGate(SequenceStep step)
    {
        float hp = GetHpFraction();

        switch (step.healthGate)
        {
            case HealthGateMode.None:
                return true;

            case HealthGateMode.AtOrBelow:
                return hp <= step.gateA;

            case HealthGateMode.AtOrAbove:
                return hp >= step.gateA;

            case HealthGateMode.Between:
                float min = Mathf.Min(step.gateA, step.gateB);
                float max = Mathf.Max(step.gateA, step.gateB);
                return hp >= min && hp <= max;
        }

        return true;
    }

    private bool ExecuteScriptedStep(SequenceStep step)
    {
        // Scripted beat source for thinking pause logic.
        currentBeatWasChance = false;

        switch (step.action)
        {
            case SequenceActionType.Attack:
                currentBeatWasScripted = true;
                return StartAttackByIndex(step.attackIndex);

            case SequenceActionType.Movement:
                currentBeatWasScripted = true;
                EnterMovementWithMode(step.movementMode, step.durationOverride);
                return true;

            case SequenceActionType.Downtime:
            case SequenceActionType.Wait:
                currentBeatWasScripted = true;
                EnterDowntime(step.durationOverride);
                return true;

            case SequenceActionType.Retreat:
                currentBeatWasScripted = true;
                EnterRetreat(step.durationOverride);
                return true;
        }

        return false;
    }


    private bool TryEnterThinkingPauseAfterScriptedBeat()
    {
        bool scriptedPause = currentBeatWasScripted && useThinkingPauseBetweenScriptedSteps;
        bool chancePause = currentBeatWasChance && useThinkingPauseBetweenChanceBeats;

        // Consume one pause opportunity per completed beat.
        currentBeatWasScripted = false;
        currentBeatWasChance = false;

        if (!scriptedPause && !chancePause)
            return false;

        float duration = Mathf.Max(0f, thinkingPauseDuration);
        if (duration <= 0f)
            return false;

        downtimeIsThinkingPause = true;
        downtimeTimer = duration;

        if (currentState != BossState.Downtime)
            EnterState(BossState.Downtime);

        return true;
    }

    private bool TryRunChanceNonAttackBeat()
    {
        // Movement chance
        if (movement.enabled &&
            Time.time >= movementCooldownUntil &&
            UnityEngine.Random.value <= Mathf.Clamp01(movement.chanceAfterAttack))
        {
            currentBeatWasScripted = false;
            currentBeatWasChance = true;
            EnterMovement();
            return true;
        }

        // Downtime chance
        if (downtime.enabled &&
            UnityEngine.Random.value <= Mathf.Clamp01(downtime.chanceAfterAttack))
        {
            currentBeatWasScripted = false;
            currentBeatWasChance = true;
            EnterDowntime();
            return true;
        }

        return false;
    }

    private bool StartAttackByIndex(int index)
    {
        if (attacks == null || index < 0 || index >= attacks.Count)
            return false;

        var entry = attacks[index];
        if (entry == null || entry.attack == null)
            return false;

        if (currentState != BossState.Engaged)
            EnterState(BossState.Engaged);

        activeAttackIndex = index;
        activeAttack = entry.attack;
        attackTimer = Mathf.Max(0.1f, entry.duration);

        if (activeAttack is BossBulletAttack bullet)
            bullet.Configure(entry.patternGroupIndex, entry.resetOnBegin, entry.fireImmediately, entry.resetOnEnd);

        activeAttack.Begin(this);
        return true;
    }

    private void StartNextAttackBySelection()
    {
        int nextIndex = PickNextAttackIndex();
        if (nextIndex < 0)
        {
            DumpEligibility();
            Debug.LogWarning($"[Boss] No eligible attacks. player={(player ? player.name : "NULL")} inRange={isPlayerInRange}", this);
            currentBeatWasScripted = false;
            currentBeatWasChance = false;
            EnterDowntime(0.25f);
            return;
        }

        currentBeatWasScripted = false;
        currentBeatWasChance = (orchestrationMode == OrchestrationMode.ChanceOnly);
        StartAttackByIndex(nextIndex);
    }

    private void StopActiveAttack(bool reset)
    {
        if (activeAttack != null)
            activeAttack.End(this, reset);

        activeAttack = null;
        attackTimer = 0f;
    }

    private int PickNextAttackIndex()
    {
        // Try with normal rules (including allowRepeat logic)
        int idx = (selectMode == AttackSelectMode.Sequence)
            ? PickNextAttack_Sequence(disallowImmediateRepeat: true)
            : PickNextAttack_Weighted(disallowImmediateRepeat: true);

        if (idx >= 0) return idx;

        // Fallback: allow repeats so the boss never stalls
        return (selectMode == AttackSelectMode.Sequence)
            ? PickNextAttack_Sequence(disallowImmediateRepeat: false)
            : PickNextAttack_Weighted(disallowImmediateRepeat: false);
    }

    private bool IsEligible(int i, float hp, float dist, bool disallowImmediateRepeat)
    {
        var a = attacks[i];
        if (a.attack == null) return false;
        if (a.weight <= 0f && selectMode == AttackSelectMode.WeightedRandom) return false;

        if (hp < a.minHpFraction || hp > a.maxHpFraction) return false;
        if (dist < a.minDistance || dist > a.maxDistance) return false;

        if (disallowImmediateRepeat && !a.allowRepeat && i == activeAttackIndex) return false;

        return true;
    }

    private int PickNextAttack_Sequence(bool disallowImmediateRepeat)
    {
        if (attacks == null || attacks.Count == 0) return -1;
        if (player == null) return -1;

        float hp = GetHpFraction();
        float dist = Vector3.Distance(transform.position, player.position);

        for (int step = 0; step < attacks.Count; step++)
        {
            sequenceCursor = (sequenceCursor + 1) % attacks.Count;

            if (IsEligible(sequenceCursor, hp, dist, disallowImmediateRepeat))
                return sequenceCursor;
        }

        return -1;
    }

    private int PickNextAttack_Weighted(bool disallowImmediateRepeat)
    {
        if (attacks == null || attacks.Count == 0) return -1;
        if (player == null) return -1;

        float hp = GetHpFraction();
        float dist = Vector3.Distance(transform.position, player.position);

        float totalWeight = 0f;
        List<int> eligible = new List<int>();

        for (int i = 0; i < attacks.Count; i++)
        {
            if (!IsEligible(i, hp, dist, disallowImmediateRepeat)) continue;

            eligible.Add(i);
            totalWeight += attacks[i].weight;
        }

        if (eligible.Count == 0) return -1;

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        float acc = 0f;

        for (int k = 0; k < eligible.Count; k++)
        {
            int idx = eligible[k];
            acc += attacks[idx].weight;
            if (roll <= acc) return idx;
        }

        return eligible[eligible.Count - 1];
    }

    private float GetHpFraction()
    {
        if (healthProvider == null) return 1f;
        if (healthProvider.Max <= 0f) return 1f;
        return Mathf.Clamp01(healthProvider.Current / healthProvider.Max);
    }

    private void TrackDamageTaken()
    {
        if (healthProvider == null) return;

        float current = healthProvider.Current;
        if (lastKnownHealth < 0f)
        {
            lastKnownHealth = current;
            return;
        }

        float delta = lastKnownHealth - current;
        if (delta > 0f)
            accumulatedDamage += delta;

        lastKnownHealth = current;
    }

    private bool ShouldTriggerRetreat()
    {
        if (!retreat.enabled) return false;
        if (Time.time < retreatCooldownUntil) return false;
        if (healthProvider == null) return false;

        float hp = GetHpFraction();
        if (hp > retreat.onlyBelowHpFraction) return false;

        return accumulatedDamage >= Mathf.Max(0.01f, retreat.damageThreshold);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        playerColliderCount++;
        isPlayerInRange = playerColliderCount > 0;

        if (player == null)
            player = other.transform;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        playerColliderCount = Mathf.Max(0, playerColliderCount - 1);
        isPlayerInRange = playerColliderCount > 0;
    }

    private void DumpEligibility()
    {
        if (attacks == null) { Debug.Log("[Boss] attacks=null", this); return; }
        if (player == null) { Debug.Log("[Boss] player=null", this); return; }

        float hp = GetHpFraction();
        float dist = Vector3.Distance(transform.position, player.position);

        Debug.Log($"[Boss] Eligibility check: hp={hp:0.00} dist={dist:0.00} lastIndex={activeAttackIndex}", this);

        for (int i = 0; i < attacks.Count; i++)
        {
            var a = attacks[i];
            bool ok = true;
            string why = "";

            if (a.attack == null) { ok = false; why += "attack=NULL; "; }
            if (selectMode == AttackSelectMode.WeightedRandom && a.weight <= 0f) { ok = false; why += "weight<=0; "; }
            if (hp < a.minHpFraction || hp > a.maxHpFraction) { ok = false; why += $"hpGate({a.minHpFraction}-{a.maxHpFraction}); "; }
            if (dist < a.minDistance || dist > a.maxDistance) { ok = false; why += $"distGate({a.minDistance}-{a.maxDistance}); "; }
            if (!a.allowRepeat && i == activeAttackIndex) { ok = false; why += "noRepeat(last); "; }

            Debug.Log($"  [{i}] '{a.name}' ok={ok} {why}", this);
        }
    }


    private static Vector2 RotateVector2(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float s = Mathf.Sin(rad);
        float c = Mathf.Cos(rad);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawFacingGizmo) return;

        Vector3 origin = transform.position;
        Vector2 fwd = (facingDirection.sqrMagnitude <= 0.0001f) ? Vector2.right : facingDirection.normalized;
        float rayLen = Mathf.Max(0.05f, facingRayLength);
        float halfFov = Mathf.Clamp(facingFovDegrees, 1f, 180f) * 0.5f;

        // Facing ray
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(origin, origin + (Vector3)(fwd * rayLen));

        // FOV wedge edges
        Vector2 left = RotateVector2(fwd, +halfFov);
        Vector2 right = RotateVector2(fwd, -halfFov);

        Gizmos.color = new Color(1f, 0.6f, 0.1f, 1f);
        Gizmos.DrawLine(origin, origin + (Vector3)(left * rayLen));
        Gizmos.DrawLine(origin, origin + (Vector3)(right * rayLen));

        // Player relation line (green if within facing cone, red otherwise)
        if (player != null)
        {
            float dot;
            bool facingPlayer = IsFacingPlayer(out dot);
            Gizmos.color = facingPlayer ? Color.green : Color.red;
            Gizmos.DrawLine(origin, player.position);
            Gizmos.DrawSphere(player.position, 0.08f);
        }
    }

#if UNITY_EDITOR

    private void OnValidate()
    {
        // Movement clamps
        if (movement.sideAmplitude < 0f) movement.sideAmplitude = 0f;
        if (movement.sideFrequency < 0f) movement.sideFrequency = 0f;
        if (movement.sideMoveSpeed < 0f) movement.sideMoveSpeed = 0f;

        if (movement.patrolHalfWidth < 0f) movement.patrolHalfWidth = 0f;
        if (movement.patrolSpeed < 0f) movement.patrolSpeed = 0f;
        if (movement.patrolEdgePause < 0f) movement.patrolEdgePause = 0f;

        if (movement.jumpDistance < 0f) movement.jumpDistance = 0f;
        if (movement.jumpHeight < 0f) movement.jumpHeight = 0f;
        if (movement.jumpDuration < 0.01f) movement.jumpDuration = 0.01f;
        if (movement.jumpInterval < 0f) movement.jumpInterval = 0f;

        if (movement.jumpHigherMinRise < 0f) movement.jumpHigherMinRise = 0f;
        if (movement.jumpHigherMaxRise < movement.jumpHigherMinRise) movement.jumpHigherMaxRise = movement.jumpHigherMinRise;
        if (movement.jumpHigherForwardMin < 0f) movement.jumpHigherForwardMin = 0f;
        if (movement.jumpHigherForwardMax < movement.jumpHigherForwardMin) movement.jumpHigherForwardMax = movement.jumpHigherForwardMin;
        movement.jumpHigherCandidateCount = Mathf.Clamp(movement.jumpHigherCandidateCount, 2, 16);
        if (movement.jumpLandingClearance < 0f) movement.jumpLandingClearance = 0f;

        if (movement.hoverBaseLift < 0f) movement.hoverBaseLift = 0f;
        if (movement.hoverSideAmplitude < 0f) movement.hoverSideAmplitude = 0f;
        if (movement.hoverSideFrequency < 0f) movement.hoverSideFrequency = 0f;
        if (movement.hoverLiftJumpDuration < 0.01f) movement.hoverLiftJumpDuration = 0.01f;
        if (movement.hoverLiftJumpHeight < 0f) movement.hoverLiftJumpHeight = 0f;
        if (movement.hoverAmplitude < 0f) movement.hoverAmplitude = 0f;
        if (movement.hoverFrequency < 0f) movement.hoverFrequency = 0f;
        if (movement.hoverMoveSpeed < 0f) movement.hoverMoveSpeed = 0f;

        if (movement.chaseSpeed < 0f) movement.chaseSpeed = 0f;
        if (movement.chaseStopDistance < 0f) movement.chaseStopDistance = 0f;
        if (movement.chaseDeadZone < 0f) movement.chaseDeadZone = 0f;

        if (string.IsNullOrWhiteSpace(movement.oneWayPlatformLayerName)) movement.oneWayPlatformLayerName = "OneWayPlatform";
        if (movement.repositionMinRadius < 0f) movement.repositionMinRadius = 0f;
        if (movement.repositionMaxRadius < movement.repositionMinRadius) movement.repositionMaxRadius = movement.repositionMinRadius;
        if (movement.hoverAmplitude < 0f) movement.hoverAmplitude = 0f;
        if (movement.hoverFrequency < 0f) movement.hoverFrequency = 0f;
        if (movement.hoverSideAmplitude < 0f) movement.hoverSideAmplitude = 0f;
        if (movement.hoverSideFrequency < 0f) movement.hoverSideFrequency = 0f;
        if (movement.hoverMoveSpeed < 0f) movement.hoverMoveSpeed = 0f;
        if (movement.hoverBaseLift < 0f) movement.hoverBaseLift = 0f;
        if (movement.repositionMoveSpeed < 0f) movement.repositionMoveSpeed = 0f;
        if (movement.repositionMaxYOffset < 0f) movement.repositionMaxYOffset = 0f;

        // Sequencer pacing clamps
        if (thinkingPauseDuration < 0f) thinkingPauseDuration = 0f;

        // Retreat clamps
        if (retreat.onlyBelowHpFraction < 0f) retreat.onlyBelowHpFraction = 0f;
        if (retreat.onlyBelowHpFraction > 1f) retreat.onlyBelowHpFraction = 1f;
        if (retreat.damageThreshold < 0f) retreat.damageThreshold = 0f;
        if (retreat.duration < 0f) retreat.duration = 0f;
        if (retreat.moveSpeed < 0f) retreat.moveSpeed = 0f;
        if (retreat.distance < 0f) retreat.distance = 0f;
        if (retreat.arriveDistance < 0f) retreat.arriveDistance = 0f;
        if (retreat.cooldown < 0f) retreat.cooldown = 0f;

        // Gizmo clamps
        if (facingRayLength < 0.05f) facingRayLength = 0.05f;
        if (facingFovDegrees < 1f) facingFovDegrees = 1f;
        if (facingFovDegrees > 180f) facingFovDegrees = 180f;
        if (facingDeadzone < 0f) facingDeadzone = 0f;
    }
#endif
}
