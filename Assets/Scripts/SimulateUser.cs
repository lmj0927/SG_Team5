using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class SimulateUser : MonoBehaviour
{
    [SerializeField] private float stride = 1.2f;
    [SerializeField] private Color32 color;
    [SerializeField] private AgentRole role = AgentRole.Coverage;
    [SerializeField] private float strideRate = 1f;
    [Tooltip("뷰포트 가장자리 안쪽으로 둘 여백(0~0.5). Simulator는 경계를 담당하지 않습니다.")]
    [SerializeField] private float viewportEdgeInset = 0.02f;
    [Header("Separation (Phase 2)")]
    [SerializeField] private float separationQueryRadius = 1.2f;
    [SerializeField] private float separationPushStrength = 6f;
    [SerializeField] private float maxSeparationSpeed = 10f;
    [Tooltip("높을수록 separation 응답이 빠름. 낮추면 군집에서 떨림이 줄어듭니다.")]
    [SerializeField] private float separationSharpness = 8f;
    [Tooltip("클 수록 회전이 빨리 따라감. 낮추면 분리로 인한 transform.up 흔들림이 줄어듭니다.")]
    [SerializeField] private float rotationSharpness = 8f;
    [Header("Boundary Constraint")]
    [Tooltip("경계 근접도 계산에 쓰는 감쇠 거리(월드 단위). 정체 복구 트리거 전용.")]
    [SerializeField] private float boundaryProximityFalloffDistance = 2.2f;
    [Tooltip("맵 경계를 가상 에이전트로 볼 때 안쪽으로 미는 강도.")]
    [SerializeField] private float wallRepulsionStrength = 1.6f;
    [Tooltip("경계 박치기 시 강제 탈출(유턴/측면턴) 트리거 뷰포트 폭.")]
    [SerializeField] private float wallEscapeTriggerViewport = 0.08f;
    [Tooltip("로컬 재탐색 시 경계 리스크 패널티를 적용하기 시작하는 근접도(0~1).")]
    [SerializeField] private float boundaryRiskStartProximity = 0.18f;
    [Tooltip("로컬 재탐색 점수에서 경계 리스크를 얼마나 강하게 감점할지.")]
    [SerializeField] private float boundaryRiskPenaltyWeight = 1.25f;
    [Tooltip("로컬 재탐색 점수에서 경계 바깥 성분 진행을 추가 감점하는 강도.")]
    [SerializeField] private float outwardHeadingPenaltyWeight = 0.95f;
    [Tooltip("경계 근접 시 의도 재평가 주기를 얼마나 줄일지(초).")]
    [SerializeField] private float nearBoundaryThinkInterval = 0.08f;
    [Header("Boundary Recover")]
    [Tooltip("BoundaryRecover 진입 경계 근접도(0~1).")]
    [SerializeField] private float boundaryRecoverEnterProximity = 0.38f;
    [Tooltip("BoundaryRecover 해제 경계 근접도(0~1). Enter보다 작게 두는 것을 권장.")]
    [SerializeField] private float boundaryRecoverExitProximity = 0.22f;
    [Tooltip("BoundaryRecover 상태를 최소 유지할 시간(초).")]
    [SerializeField] private float boundaryRecoverMinHoldTime = 0.28f;
    [Tooltip("BoundaryRecover 중 안쪽 방향 정렬에 주는 우선순위 보너스 강도.")]
    [SerializeField] private float boundaryRecoverInwardPriorityWeight = 1.1f;
    [Tooltip("BoundaryRecover 중 의도 재평가 주기(초).")]
    [SerializeField] private float boundaryRecoverThinkInterval = 0.06f;
    [Range(0f, 1f)]
    [Tooltip("회전 목표 계산 시 desired(의도) 가중치. separation 시각 노이즈를 줄이기 위해 기본값을 높게 둡니다.")]
    [SerializeField] private float facingDesiredWeight = 0.8f;
    [Header("AI Movement (Phase 3~5)")]
    [SerializeField] private float thinkInterval = 0.25f;
    [SerializeField] private float perceptionRadius = 2.5f;
    [SerializeField] private int directionSampleCount = 12;
    [SerializeField] private int radialSampleCount = 3;
    [SerializeField] private float intentSharpness = 10f;
    [SerializeField] private float denyEnemyWeight = 1.6f;
    [SerializeField] private float denyNeutralWeight = 0.25f;
    [SerializeField] private float coverageEnemyWeight = 1.0f;
    [SerializeField] private float coverageNeutralWeight = 0.8f;
    [Tooltip("Coverage/Defender(Patrol)에서 baseColor(흰색) 우선순위 배수.")]
    [SerializeField] private float coverageBasePriorityMultiplier = 2.0f;
    [Tooltip("Coverage에서 적 영역 중심 방향 정렬 보너스 강도.")]
    [SerializeField] private float coverageCenterAttractWeight = 1.15f;
    [Tooltip("Coverage에서 적 중심에 가까워지는 진행도 보너스 강도.")]
    [SerializeField] private float coverageProgressWeight = 0.45f;
    [Tooltip("Coverage에서 선택한 측면(좌/우)을 최소 유지할 시간(초).")]
    [SerializeField] private float coverageSideLockDuration = 0.28f;
    [Tooltip("Coverage에서 반대 측면으로 갈아타기 위해 필요한 최소 점수 우위.")]
    [SerializeField] private float coverageSideSwitchMargin = 0.15f;
    [Tooltip("Coverage 측면 락 중 반대 측면에 부여할 감점.")]
    [SerializeField] private float coverageOppositeSidePenalty = 0.2f;
    [SerializeField] private float defendBoundaryWeight = 2.0f;
    [SerializeField] private float defendNeutralWeight = 0.2f;
    [Header("Defender FSM")]
    [SerializeField] private float defenderThreatDetectRadius = 2.2f;
    [SerializeField] private int defenderThreatSampleCount = 16;
    [SerializeField] private float defenderEnterThreatThreshold = 0.22f;
    [SerializeField] private float defenderExitThreatThreshold = 0.10f;
    [SerializeField] private float defenderMinDefendHoldTime = 0.55f;
    [SerializeField] private float defenderTargetAttractWeight = 1.35f;
    [SerializeField] private float defenderEnemyEngageWeight = 1.15f;
    [Header("Collision Prediction")]
    [SerializeField] private float collisionLookAheadDistance = 0.9f;
    [SerializeField] private float collisionQueryRadius = 0.9f;
    [SerializeField] private float collisionPenaltyWeight = 1.2f;
    [Tooltip("Coverage는 라인 고착을 줄이기 위해 충돌 패널티를 다소 완화합니다.")]
    [SerializeField] private float coverageCollisionPenaltyScale = 0.65f;
    [SerializeField] private float defenderCollisionPenaltyScale = 0.85f;
    [SerializeField] private float imminentCollisionRiskThreshold = 0.75f;
    [SerializeField] private float emergencyReplanCooldown = 0.12f;
    [Header("Evade Mode")]
    [SerializeField] private float evadeEnterRiskThreshold = 1.2f;
    [SerializeField] private float evadeExitRiskThreshold = 0.75f;
    [SerializeField] private float evadeHoldTime = 0.3f;
    [SerializeField] private float evadeTurnBias = 0.9f;
    [SerializeField] private float evadeReplanCooldown = 0.1f;
    [Header("Stuck Recovery")]
    [SerializeField] private float stuckSpeedThreshold = 0.18f;
    [SerializeField] private float stuckTimeToRecover = 0.55f;
    [SerializeField] private float stuckRecoverDuration = 0.35f;
    [SerializeField] private float stuckRecoverBoost = 2.1f;

    private float currentStrideRate;
    private float thinkTimer;
    private float stuckTimer;
    private float recoverTimer;
    private float emergencyReplanTimer;
    private float evadeHoldTimer;
    private float evadeReplanTimer;
    private float boundaryRecoverHoldTimer;
    private Coroutine drawColorCoroutine;
    private Vector2 startPosition;
    private Rigidbody2D rb;
    private CircleCollider2D selfCollider;
    private Vector2 currentIntent = Vector2.up;
    private Vector2 previousSeparation = Vector2.zero;
    private ColorDrawer drawer;
    private DefenderMode defenderMode = DefenderMode.Patrol;
    private MovementMode movementMode = MovementMode.Normal;
    private bool isBoundaryRecoverActive = false;
    private Vector2 defenderThreatTarget = Vector2.zero;
    private float defenderHoldTimer = 0f;
    private float coverageSideLockTimer = 0f;
    private int coverageLockedSideSign = 0;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.freezeRotation = false;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        selfCollider = GetComponent<CircleCollider2D>();
        selfCollider.isTrigger = true;
        currentIntent = Vector2.up;
    }

    private void Start()
    {
        drawer = ColorDrawer.Instance;
        thinkTimer = Random.Range(0f, Mathf.Max(0.01f, thinkInterval));
    }

    private void Update()
    {
        thinkTimer += Time.deltaTime;
        coverageSideLockTimer = Mathf.Max(0f, coverageSideLockTimer - Time.deltaTime);
        float currentThinkInterval = GetCurrentThinkInterval();
        if (thinkTimer >= currentThinkInterval)
        {
            thinkTimer = 0f;
            UpdateDefenderMode();
            Vector2 nextIntent = EvaluateBestIntent(currentThinkInterval);
            if (nextIntent.sqrMagnitude > 1e-8f)
            {
                currentIntent = nextIntent.normalized;
            }
        }

        currentStrideRate += Time.deltaTime;
        if (currentStrideRate >= strideRate)
        {
            currentStrideRate = 0f;
            if (drawColorCoroutine != null)
            {
                StopCoroutine(drawColorCoroutine);
            }

            startPosition = rb.position;
            drawColorCoroutine = StartCoroutine(DrawColor());
        }
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        emergencyReplanTimer = Mathf.Max(0f, emergencyReplanTimer - dt);
        evadeReplanTimer = Mathf.Max(0f, evadeReplanTimer - dt);
        Vector2 desired = currentIntent * stride;

        Vector2 separationRaw = AgentSeparation2D.Compute(
            rb.position,
            selfCollider,
            separationQueryRadius,
            separationPushStrength,
            maxSeparationSpeed);
        float sepT = 1f - Mathf.Exp(-Mathf.Max(0f, separationSharpness) * dt);
        Vector2 separation = Vector2.Lerp(previousSeparation, separationRaw, sepT);
        previousSeparation = separation;

        Vector2 velocity = desired + separation;
        Camera cam = Camera.main;
        float edgeProximity = 0f;
        Vector2 inwardDir = Vector2.zero;
        if (cam != null)
        {
            edgeProximity = GetEdgeProximity(cam);
            inwardDir = GetInwardDirection(cam);
        }
        UpdateBoundaryRecoverState(edgeProximity, dt);

        if (cam != null)
        {
            Vector2 wallRepulsion = AgentSeparation2D.ComputeBoundaryRepulsion(
                cam,
                transform.position,
                viewportEdgeInset,
                wallEscapeTriggerViewport,
                wallRepulsionStrength);
            velocity += wallRepulsion;

            if (AgentSeparation2D.TryGetBoundaryEscapeDirection(
                cam,
                transform.position,
                velocity.sqrMagnitude > 1e-8f ? velocity : desired,
                viewportEdgeInset,
                wallEscapeTriggerViewport,
                out Vector2 escapeDir))
            {
                currentIntent = escapeDir;
                desired = currentIntent * stride;
                velocity = desired + separation + wallRepulsion;
            }
        }

        float imminentRisk = AgentSeparation2D.EstimateCollisionRisk(
            rb.position,
            desired.sqrMagnitude > 1e-8f ? desired.normalized : currentIntent,
            collisionLookAheadDistance,
            selfCollider,
            collisionQueryRadius);

        UpdateMovementMode(imminentRisk, dt);
        if (movementMode == MovementMode.Evade && evadeReplanTimer <= 0f)
        {
            Vector2 evadeDir = ChooseEvadeDirection();
            if (evadeDir.sqrMagnitude > 1e-8f)
            {
                currentIntent = evadeDir.normalized;
                desired = currentIntent * stride;
                velocity = desired + separation;
                if (cam != null)
                {
                    Vector2 wallRepulsion = AgentSeparation2D.ComputeBoundaryRepulsion(
                        cam,
                        transform.position,
                        viewportEdgeInset,
                        wallEscapeTriggerViewport,
                        wallRepulsionStrength);
                    velocity += wallRepulsion;

                    if (AgentSeparation2D.TryGetBoundaryEscapeDirection(
                        cam,
                        transform.position,
                        velocity.sqrMagnitude > 1e-8f ? velocity : desired,
                        viewportEdgeInset,
                        wallEscapeTriggerViewport,
                        out Vector2 escapeDir))
                    {
                        currentIntent = escapeDir;
                        desired = currentIntent * stride;
                        velocity = desired + separation + wallRepulsion;
                    }
                }
            }

            evadeReplanTimer = Mathf.Max(0.02f, evadeReplanCooldown);
        }

        if (imminentRisk >= Mathf.Max(0f, imminentCollisionRiskThreshold) && emergencyReplanTimer <= 0f)
        {
            Vector2 replanned = EvaluateBestIntent(GetCurrentThinkInterval());
            if (replanned.sqrMagnitude > 1e-8f)
            {
                currentIntent = replanned.normalized;
                desired = currentIntent * stride;
                velocity = desired + separation;
                if (cam != null)
                {
                    Vector2 wallRepulsion = AgentSeparation2D.ComputeBoundaryRepulsion(
                        cam,
                        transform.position,
                        viewportEdgeInset,
                        wallEscapeTriggerViewport,
                        wallRepulsionStrength);
                    velocity += wallRepulsion;

                    if (AgentSeparation2D.TryGetBoundaryEscapeDirection(
                        cam,
                        transform.position,
                        velocity.sqrMagnitude > 1e-8f ? velocity : desired,
                        viewportEdgeInset,
                        wallEscapeTriggerViewport,
                        out Vector2 escapeDir))
                    {
                        currentIntent = escapeDir;
                        desired = currentIntent * stride;
                        velocity = desired + separation + wallRepulsion;
                    }
                }
            }

            emergencyReplanTimer = Mathf.Max(0.02f, emergencyReplanCooldown);
        }

        bool isNearBoundary = edgeProximity > 0.2f;
        if (isNearBoundary && velocity.magnitude < Mathf.Max(0f, stuckSpeedThreshold))
        {
            stuckTimer += dt;
            if (stuckTimer >= Mathf.Max(0.01f, stuckTimeToRecover))
            {
                recoverTimer = Mathf.Max(recoverTimer, Mathf.Max(0.05f, stuckRecoverDuration));
                stuckTimer = 0f;
            }
        }
        else
        {
            stuckTimer = 0f;
        }

        if (recoverTimer > 0f && inwardDir.sqrMagnitude > 1e-8f)
        {
            velocity += inwardDir * Mathf.Max(0f, stuckRecoverBoost);
            recoverTimer -= dt;
        }

        Vector2 targetFacing = GetFacingTarget(desired, separation, velocity);

        Vector2 currentFacing = new Vector2(transform.up.x, transform.up.y);
        if (currentFacing.sqrMagnitude < 1e-8f)
        {
            currentFacing = Vector2.up;
        }
        else
        {
            currentFacing.Normalize();
        }

        float smoothT = 1f - Mathf.Exp(-rotationSharpness * dt);
        Vector2 smoothedFacing = Vector2.Lerp(currentFacing, targetFacing, smoothT);
        if (smoothedFacing.sqrMagnitude > 1e-8f)
        {
            smoothedFacing.Normalize();
            transform.up = new Vector3(smoothedFacing.x, smoothedFacing.y, 0f);
        }

        Vector2 delta = velocity * dt;
        Vector3 next = new Vector3(rb.position.x + delta.x, rb.position.y + delta.y, transform.position.z);

        rb.MovePosition(new Vector2(next.x, next.y));
    }

    private Vector2 GetFacingTarget(Vector2 desired, Vector2 separation, Vector2 velocity)
    {
        float desiredWeight = Mathf.Clamp01(facingDesiredWeight);
        float separationWeight = 1f - desiredWeight;
        Vector2 blended = desired * desiredWeight + separation * separationWeight;
        if (blended.sqrMagnitude > 1e-8f)
        {
            return blended.normalized;
        }

        if (velocity.sqrMagnitude > 1e-8f)
        {
            return velocity.normalized;
        }

        return new Vector2(transform.up.x, transform.up.y);
    }

    private Vector2 EvaluateBestIntent()
    {
        return EvaluateBestIntent(Mathf.Max(0.01f, thinkInterval));
    }

    private Vector2 EvaluateBestIntent(float decisionInterval)
    {
        if (movementMode == MovementMode.Evade)
        {
            return ChooseEvadeDirection();
        }

        if (drawer == null)
        {
            drawer = ColorDrawer.Instance;
        }

        if (drawer == null || !drawer.IsInNormalGameplayPhase())
        {
            return currentIntent;
        }

        int dirCount = Mathf.Max(6, directionSampleCount);
        int radialCount = Mathf.Max(1, radialSampleCount);
        float radius = Mathf.Max(0.2f, perceptionRadius);
        Vector2 coverageTargetPos = rb.position;
        bool hasCoverageTarget = role == AgentRole.Coverage && TryComputeCoverageTarget(out coverageTargetPos);
        Vector2 coverageTargetDir = Vector2.zero;
        float coverageTargetDist = 0f;
        if (hasCoverageTarget)
        {
            Vector2 toTarget = coverageTargetPos - rb.position;
            coverageTargetDist = toTarget.magnitude;
            if (toTarget.sqrMagnitude > 1e-8f)
            {
                coverageTargetDir = toTarget.normalized;
            }
            else
            {
                hasCoverageTarget = false;
            }
        }

        Vector2 bestDir = currentIntent.sqrMagnitude > 1e-8f ? currentIntent.normalized : Vector2.up;
        float bestScore = float.NegativeInfinity;
        Vector2 bestLockedDir = bestDir;
        float bestLockedScore = float.NegativeInfinity;
        bool hasLockedCandidate = false;

        for (int i = 0; i < dirCount; i++)
        {
            float angle = (Mathf.PI * 2f * i) / dirCount;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float score = ScoreDirection(
                dir,
                radius,
                radialCount,
                hasCoverageTarget,
                coverageTargetPos,
                coverageTargetDir,
                coverageTargetDist);
            if (score > bestScore)
            {
                bestScore = score;
                bestDir = dir;
            }

            if (hasCoverageTarget && coverageLockedSideSign != 0)
            {
                int sideSign = GetSideSign(coverageTargetDir, dir);
                if (sideSign == 0 || sideSign == coverageLockedSideSign)
                {
                    if (score > bestLockedScore)
                    {
                        bestLockedScore = score;
                        bestLockedDir = dir;
                        hasLockedCandidate = true;
                    }
                }
            }
        }

        if (hasCoverageTarget && coverageLockedSideSign != 0 && coverageSideLockTimer > 0f && hasLockedCandidate)
        {
            float switchMargin = Mathf.Max(0f, coverageSideSwitchMargin);
            if (bestScore - bestLockedScore < switchMargin)
            {
                bestDir = bestLockedDir;
                bestScore = bestLockedScore;
            }
        }

        if (hasCoverageTarget)
        {
            int selectedSideSign = GetSideSign(coverageTargetDir, bestDir);
            if (selectedSideSign != 0)
            {
                bool shouldStartLock = coverageLockedSideSign == 0 || coverageSideLockTimer <= 0f;
                bool switchedSide = coverageLockedSideSign != 0 && coverageLockedSideSign != selectedSideSign;
                if (shouldStartLock || switchedSide)
                {
                    coverageLockedSideSign = selectedSideSign;
                    coverageSideLockTimer = Mathf.Max(0.01f, coverageSideLockDuration);
                }
            }
        }

        float dt = Mathf.Max(0.01f, decisionInterval);
        Vector2 smooth = Vector2.Lerp(currentIntent.normalized, bestDir, 1f - Mathf.Exp(-intentSharpness * dt));
        return smooth.sqrMagnitude > 1e-8f ? smooth.normalized : bestDir;
    }

    private float ScoreDirection(
        Vector2 direction,
        float radius,
        int radialCount,
        bool hasCoverageTarget,
        Vector2 coverageTargetPos,
        Vector2 coverageTargetDir,
        float coverageTargetDist)
    {
        float score = 0f;
        Vector2 origin = rb.position;
        for (int i = 1; i <= radialCount; i++)
        {
            float t = i / (float)radialCount;
            Vector2 samplePos = origin + direction * (radius * t);
            if (!drawer.TryGetPixelColorAtWorld(samplePos, out Color32 c))
            {
                continue;
            }

            bool isSelf = c.Equals(color);
            bool isBase = drawer.IsBaseColor(c);
            bool isEnemy = !isSelf && !isBase;

            switch (role)
            {
                case AgentRole.Coverage:
                    if (isEnemy) score += coverageEnemyWeight;
                    else if (isBase) score += coverageNeutralWeight * Mathf.Max(1f, coverageBasePriorityMultiplier);
                    break;
                case AgentRole.Denial:
                    if (isEnemy) score += denyEnemyWeight;
                    else if (isBase) score += denyNeutralWeight;
                    break;
                case AgentRole.Defender:
                    if (defenderMode == DefenderMode.Patrol)
                    {
                        // 평시엔 Coverage처럼 움직이며 영역 확장에 기여
                        if (isEnemy) score += coverageEnemyWeight;
                        else if (isBase) score += coverageNeutralWeight * Mathf.Max(1f, coverageBasePriorityMultiplier);
                    }
                    else
                    {
                        // 위협 감지 상태에서는 적색 대응 우선
                        if (isEnemy)
                        {
                            score += defenderEnemyEngageWeight;
                        }

                        if (isSelf)
                        {
                            Vector2 boundaryProbe = samplePos + direction * (radius * 0.3f);
                            if (drawer.TryGetPixelColorAtWorld(boundaryProbe, out Color32 around))
                            {
                                bool aroundEnemy = !around.Equals(color) && !drawer.IsBaseColor(around);
                                if (aroundEnemy)
                                {
                                    score += defendBoundaryWeight;
                                }
                            }
                        }
                        else if (isBase)
                        {
                            score += defendNeutralWeight;
                        }

                        Vector2 toThreat = defenderThreatTarget - rb.position;
                        if (toThreat.sqrMagnitude > 1e-8f)
                        {
                            score += Mathf.Max(0f, defenderTargetAttractWeight) * Mathf.Clamp(Vector2.Dot(direction, toThreat.normalized), -1f, 1f);
                        }
                    }
                    break;
            }
        }

        // 관성 보너스: 너무 빈번한 방향 전환을 줄여 떨림 완화
        if (currentIntent.sqrMagnitude > 1e-8f)
        {
            float inertiaWeight = movementMode == MovementMode.Evade ? 0.02f : 0.15f;
            if (isBoundaryRecoverActive)
            {
                inertiaWeight *= 0.7f;
            }
            score += Mathf.Clamp01(Vector2.Dot(currentIntent.normalized, direction)) * inertiaWeight;
        }

        float collisionRisk = AgentSeparation2D.EstimateCollisionRisk(
            rb.position,
            direction,
            collisionLookAheadDistance,
            selfCollider,
            collisionQueryRadius);
        float rolePenaltyScale = role switch
        {
            AgentRole.Coverage => Mathf.Max(0.1f, coverageCollisionPenaltyScale),
            AgentRole.Defender => Mathf.Max(0.1f, defenderCollisionPenaltyScale),
            _ => 1f,
        };
        score -= collisionRisk * Mathf.Max(0f, collisionPenaltyWeight) * rolePenaltyScale;

        if (role == AgentRole.Coverage && hasCoverageTarget)
        {
            float centerWeight = Mathf.Max(0f, coverageCenterAttractWeight);
            if (movementMode == MovementMode.Evade || isBoundaryRecoverActive)
            {
                centerWeight *= 0.6f;
            }

            float alignment = Mathf.Clamp(Vector2.Dot(direction, coverageTargetDir), -1f, 1f);
            score += alignment * centerWeight;

            Vector2 predictedPos = rb.position + direction * Mathf.Max(0.05f, collisionLookAheadDistance);
            float predictedDist = Vector2.Distance(predictedPos, coverageTargetPos);
            float progress = coverageTargetDist - predictedDist;
            score += progress * Mathf.Max(0f, coverageProgressWeight);

            if (coverageLockedSideSign != 0 && coverageSideLockTimer > 0f)
            {
                int sideSign = GetSideSign(coverageTargetDir, direction);
                if (sideSign != 0 && sideSign != coverageLockedSideSign)
                {
                    score -= Mathf.Max(0f, coverageOppositeSidePenalty);
                }
            }
        }

        Camera cam = Camera.main;
        if (cam != null)
        {
            if (isBoundaryRecoverActive)
            {
                Vector2 inwardDir = GetInwardDirection(cam);
                if (inwardDir.sqrMagnitude > 1e-8f)
                {
                    float inwardAlignment = Mathf.Clamp(Vector2.Dot(direction, inwardDir), -1f, 1f);
                    score += inwardAlignment * Mathf.Max(0f, boundaryRecoverInwardPriorityWeight);
                }
            }

            float currentEdgeProximity = GetEdgeProximity(cam);
            if (currentEdgeProximity >= Mathf.Clamp01(boundaryRiskStartProximity))
            {
                Vector2 probePos = rb.position + direction * Mathf.Max(0.05f, collisionLookAheadDistance);
                float falloffViewport = Mathf.Clamp01(Mathf.Max(0.01f, boundaryProximityFalloffDistance) * 0.05f);
                float predictedEdgeRisk = AgentSeparation2D.GetViewportEdgeProximity(
                    cam,
                    new Vector3(probePos.x, probePos.y, transform.position.z),
                    viewportEdgeInset,
                    falloffViewport);
                score -= predictedEdgeRisk * Mathf.Max(0f, boundaryRiskPenaltyWeight);

                if (AgentSeparation2D.TryGetBoundaryEscapeDirection(
                    cam,
                    transform.position,
                    direction,
                    viewportEdgeInset,
                    wallEscapeTriggerViewport,
                    out _))
                {
                    score -= Mathf.Max(0f, outwardHeadingPenaltyWeight);
                }
            }
        }

        return score;
    }

    private bool TryComputeCoverageTarget(out Vector2 centroid)
    {
        centroid = rb.position;
        if (drawer == null)
        {
            return false;
        }

        int dirCount = Mathf.Max(8, directionSampleCount);
        int radialCount = Mathf.Max(2, radialSampleCount + 1);
        float radius = Mathf.Max(0.2f, perceptionRadius);
        float weightSum = 0f;
        Vector2 weighted = Vector2.zero;

        for (int i = 0; i < dirCount; i++)
        {
            float angle = (Mathf.PI * 2f * i) / dirCount;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            for (int r = 1; r <= radialCount; r++)
            {
                float t = r / (float)radialCount;
                Vector2 samplePos = rb.position + dir * (radius * t);
                if (!drawer.TryGetPixelColorAtWorld(samplePos, out Color32 c))
                {
                    continue;
                }

                bool isEnemy = !c.Equals(color) && !drawer.IsBaseColor(c);
                if (!isEnemy)
                {
                    continue;
                }

                float distanceWeight = 1f + (1f - t) * 0.5f;
                weighted += samplePos * distanceWeight;
                weightSum += distanceWeight;
            }
        }

        if (weightSum <= 1e-6f)
        {
            return false;
        }

        centroid = weighted / weightSum;
        return true;
    }

    private static int GetSideSign(Vector2 referenceDir, Vector2 direction)
    {
        float cross = (referenceDir.x * direction.y) - (referenceDir.y * direction.x);
        if (cross > 1e-4f)
        {
            return 1;
        }

        if (cross < -1e-4f)
        {
            return -1;
        }

        return 0;
    }

    private float GetCurrentThinkInterval()
    {
        float baseInterval = Mathf.Max(0.01f, thinkInterval);
        Camera cam = Camera.main;
        if (cam == null)
        {
            return baseInterval;
        }

        float nearInterval = Mathf.Clamp(nearBoundaryThinkInterval, 0.01f, baseInterval);
        float edgeProximity = GetEdgeProximity(cam);
        float interval = Mathf.Lerp(baseInterval, nearInterval, edgeProximity);
        if (isBoundaryRecoverActive)
        {
            float recoverInterval = Mathf.Clamp(boundaryRecoverThinkInterval, 0.01f, baseInterval);
            interval = Mathf.Min(interval, recoverInterval);
        }

        return interval;
    }

    private void UpdateBoundaryRecoverState(float edgeProximity, float dt)
    {
        boundaryRecoverHoldTimer = Mathf.Max(0f, boundaryRecoverHoldTimer - dt);
        float enter = Mathf.Clamp01(boundaryRecoverEnterProximity);
        float exit = Mathf.Clamp01(Mathf.Min(boundaryRecoverExitProximity, enter));

        if (!isBoundaryRecoverActive)
        {
            if (edgeProximity >= enter)
            {
                isBoundaryRecoverActive = true;
                boundaryRecoverHoldTimer = Mathf.Max(boundaryRecoverHoldTimer, Mathf.Max(0.01f, boundaryRecoverMinHoldTime));
            }

            return;
        }

        bool canExit = boundaryRecoverHoldTimer <= 0f;
        if (canExit && edgeProximity <= exit)
        {
            isBoundaryRecoverActive = false;
        }
    }

    private void UpdateMovementMode(float imminentRisk, float dt)
    {
        if (movementMode == MovementMode.Normal)
        {
            if (imminentRisk >= Mathf.Max(0f, evadeEnterRiskThreshold))
            {
                movementMode = MovementMode.Evade;
                evadeHoldTimer = Mathf.Max(0.05f, evadeHoldTime);
            }

            return;
        }

        evadeHoldTimer = Mathf.Max(0f, evadeHoldTimer - dt);
        bool underExit = imminentRisk <= Mathf.Max(0f, evadeExitRiskThreshold);
        if (underExit && evadeHoldTimer <= 0f)
        {
            movementMode = MovementMode.Normal;
        }
    }

    private Vector2 ChooseEvadeDirection()
    {
        Vector2 forward = currentIntent.sqrMagnitude > 1e-8f ? currentIntent.normalized : Vector2.up;
        Vector2 back = -forward;
        Vector2 left = new Vector2(-forward.y, forward.x);
        Vector2 right = new Vector2(forward.y, -forward.x);
        Vector2[] candidates = { back, left, right };

        Vector2 bestDir = back;
        float bestCost = float.PositiveInfinity;
        float sideBias = Mathf.Max(0f, evadeTurnBias);
        for (int i = 0; i < candidates.Length; i++)
        {
            Vector2 dir = candidates[i];
            float risk = AgentSeparation2D.EstimateCollisionRisk(
                rb.position,
                dir,
                collisionLookAheadDistance,
                selfCollider,
                collisionQueryRadius);
            float cost = risk;
            if (i > 0)
            {
                // 좌/우 우회가 정면후진보다 약간 유리하도록 바이어스
                cost -= sideBias * 0.05f;
            }

            if (cost < bestCost)
            {
                bestCost = cost;
                bestDir = dir;
            }
        }

        return bestDir.normalized;
    }

    private float GetEdgeProximity(Camera cam)
    {
        float falloffViewport = Mathf.Clamp01(Mathf.Max(0.01f, boundaryProximityFalloffDistance) * 0.05f);
        return AgentSeparation2D.GetViewportEdgeProximity(cam, transform.position, viewportEdgeInset, falloffViewport);
    }

    private Vector2 GetInwardDirection(Camera cam)
    {
        if (cam == null)
        {
            return Vector2.zero;
        }

        float depth = Mathf.Abs(cam.transform.position.z - transform.position.z);
        if (depth < 0.01f)
        {
            depth = 10f;
        }

        Vector3 centerWorld = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, depth));
        Vector2 inward = (Vector2)centerWorld - rb.position;
        return inward.sqrMagnitude > 1e-8f ? inward.normalized : Vector2.zero;
    }

    private void UpdateDefenderMode()
    {
        if (role != AgentRole.Defender || drawer == null || !drawer.IsInNormalGameplayPhase())
        {
            defenderMode = DefenderMode.Patrol;
            defenderHoldTimer = 0f;
            return;
        }

        float signal = ScanThreatSignal(out Vector2 bestThreatPos);
        if (defenderMode == DefenderMode.Patrol)
        {
            if (signal >= Mathf.Max(0f, defenderEnterThreatThreshold))
            {
                defenderMode = DefenderMode.Defend;
                defenderThreatTarget = bestThreatPos;
                defenderHoldTimer = Mathf.Max(0f, defenderMinDefendHoldTime);
            }

            return;
        }

        // Defend 상태: 위협이 있으면 타깃을 갱신하고, 없으면 최소 유지시간 후 Patrol로 복귀
        if (signal > 0f)
        {
            defenderThreatTarget = bestThreatPos;
        }

        defenderHoldTimer = Mathf.Max(0f, defenderHoldTimer - Mathf.Max(0.01f, thinkInterval));
        if (signal <= Mathf.Max(0f, defenderExitThreatThreshold) && defenderHoldTimer <= 0f)
        {
            defenderMode = DefenderMode.Patrol;
        }
    }

    private float ScanThreatSignal(out Vector2 bestThreatPos)
    {
        bestThreatPos = rb.position;
        int sampleCount = Mathf.Max(8, defenderThreatSampleCount);
        float radius = Mathf.Max(0.2f, defenderThreatDetectRadius);
        int enemyCount = 0;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < sampleCount; i++)
        {
            float angle = (Mathf.PI * 2f * i) / sampleCount;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 samplePos = rb.position + dir * radius;
            if (!drawer.TryGetPixelColorAtWorld(samplePos, out Color32 c))
            {
                continue;
            }

            bool isEnemy = !c.Equals(color) && !drawer.IsBaseColor(c);
            if (!isEnemy)
            {
                continue;
            }

            enemyCount++;

            // 현재 의도와 가깝고 가까운 적을 우선 타깃으로 선정
            float align = currentIntent.sqrMagnitude > 1e-8f
                ? Mathf.Clamp01((Vector2.Dot(currentIntent.normalized, dir) + 1f) * 0.5f)
                : 0.5f;
            float score = align + 0.5f;
            if (score > bestScore)
            {
                bestScore = score;
                bestThreatPos = samplePos;
            }
        }

        return sampleCount > 0 ? enemyCount / (float)sampleCount : 0f;
    }

    private IEnumerator DrawColor()
    {
        var timer = 0f;
        while (timer < strideRate)
        {
            ColorDrawer.Instance.PaintAtWorldPosition(startPosition, ColorDrawer.Instance.GetScaledBrushRadius(timer), color);
            timer += Time.deltaTime;
            yield return null;
        }
    }

    /// <param name="strideOverride">0보다 크면 이동 속도(<see cref="stride"/>)로 적용. 생략 시 프리팹/인스펙터 값 유지.</param>
    public void Initialize(Color32 color, AgentRole role, Vector2 initialIntent, float strideOverride = -1f)
    {
        ResetPaintingState();

        this.color = color;
        this.role = role;
        if (strideOverride > 0f)
        {
            stride = Mathf.Clamp(strideOverride, 0.1f, 30f);
        }

        if (initialIntent.sqrMagnitude > 1e-8f)
        {
            currentIntent = initialIntent.normalized;
            transform.up = new Vector3(currentIntent.x, currentIntent.y, 0f);
        }
    }

    /// <summary>라운드 리스폰 시 strideRate 타이머·그리기 코루틴을 초기화합니다.</summary>
    private void ResetPaintingState()
    {
        if (drawColorCoroutine != null)
        {
            StopCoroutine(drawColorCoroutine);
            drawColorCoroutine = null;
        }

        currentStrideRate = 0f;
    }
}

public enum AgentRole
{
    Coverage,
    Denial,
    Defender,
}

public enum DefenderMode
{
    Patrol,
    Defend,
}

public enum MovementMode
{
    Normal,
    Evade,
}
