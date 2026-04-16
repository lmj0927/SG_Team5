using UnityEngine;

/// <summary>
/// 에이전트끼리 겹침을 줄이기 위한 간단한 분리(separation) 벡터.
/// <see cref="SimulateUser"/>, <see cref="PlayerUser"/> 등 동일 쿼리 반경 안의 피어 콜라이더에 대해 밀어냅니다.
/// </summary>
public static class AgentSeparation2D
{
    private const int MaxHits = 24;
    /// <summary>거리 제곱 하한. 너무 가까울 때 1/dist² 폭주로 분리·회전이 떨리는 것을 줄입니다.</summary>
    private const float MinDistSq = 0.09f;

    private static readonly Collider2D[] Hits = new Collider2D[MaxHits];

    /// <summary>뷰포트 경계 근접도 계산(0=안쪽, 1=경계/밖).</summary>
    public static float GetViewportEdgeProximity(Camera camera, Vector3 worldPosition, float viewportInset, float falloffViewport)
    {
        if (camera == null)
        {
            return 0f;
        }

        Vector3 vp = camera.WorldToViewportPoint(worldPosition);
        float inset = Mathf.Clamp(viewportInset, 0f, 0.49f);
        float minX = inset;
        float maxX = 1f - inset;
        float minY = inset;
        float maxY = 1f - inset;

        float dx = Mathf.Min(vp.x - minX, maxX - vp.x);
        float dy = Mathf.Min(vp.y - minY, maxY - vp.y);
        float nearest = Mathf.Min(dx, dy);
        float falloff = Mathf.Max(0.0001f, falloffViewport);
        return 1f - Mathf.Clamp01(nearest / falloff);
    }

    /// <summary>
    /// 경계를 "가상 에이전트"로 보고 반발 벡터를 계산합니다.
    /// triggerViewport는 뷰포트 단위(예: 0.08=경계 8% 구간)입니다.
    /// </summary>
    public static Vector2 ComputeBoundaryRepulsion(
        Camera camera,
        Vector3 worldPosition,
        float viewportInset,
        float triggerViewport,
        float repulsionStrength)
    {
        if (camera == null)
        {
            return Vector2.zero;
        }

        Vector3 vp = camera.WorldToViewportPoint(worldPosition);
        float inset = Mathf.Clamp(viewportInset, 0f, 0.49f);
        float trigger = Mathf.Clamp(triggerViewport, 0.0001f, 0.49f);
        float minX = inset;
        float maxX = 1f - inset;
        float minY = inset;
        float maxY = 1f - inset;

        Vector2 repel = Vector2.zero;
        float leftDist = vp.x - minX;
        if (leftDist < trigger)
        {
            float t = 1f - Mathf.Clamp01(leftDist / trigger);
            repel.x += t * repulsionStrength;
        }

        float rightDist = maxX - vp.x;
        if (rightDist < trigger)
        {
            float t = 1f - Mathf.Clamp01(rightDist / trigger);
            repel.x -= t * repulsionStrength;
        }

        float bottomDist = vp.y - minY;
        if (bottomDist < trigger)
        {
            float t = 1f - Mathf.Clamp01(bottomDist / trigger);
            repel.y += t * repulsionStrength;
        }

        float topDist = maxY - vp.y;
        if (topDist < trigger)
        {
            float t = 1f - Mathf.Clamp01(topDist / trigger);
            repel.y -= t * repulsionStrength;
        }

        return repel;
    }

    /// <summary>
    /// 경계로 "박고 있는" 상황이면 즉시 탈출 방향(유턴/측면턴)을 반환합니다.
    /// </summary>
    public static bool TryGetBoundaryEscapeDirection(
        Camera camera,
        Vector3 worldPosition,
        Vector2 heading,
        float viewportInset,
        float triggerViewport,
        out Vector2 escapeDirection)
    {
        escapeDirection = Vector2.zero;
        if (camera == null || heading.sqrMagnitude < 1e-8f)
        {
            return false;
        }

        Vector3 vp = camera.WorldToViewportPoint(worldPosition);
        float inset = Mathf.Clamp(viewportInset, 0f, 0.49f);
        float trigger = Mathf.Clamp(triggerViewport, 0.0001f, 0.49f);
        float minX = inset;
        float maxX = 1f - inset;
        float minY = inset;
        float maxY = 1f - inset;

        Vector2 dir = heading.normalized;
        bool shouldTurn = false;

        // 경계 가까이 + 바깥 성분으로 진행 중이면 성분 반전(확 꺾기)
        if (vp.x - minX < trigger && dir.x < 0f)
        {
            dir.x = Mathf.Abs(dir.x);
            shouldTurn = true;
        }
        else if (maxX - vp.x < trigger && dir.x > 0f)
        {
            dir.x = -Mathf.Abs(dir.x);
            shouldTurn = true;
        }

        if (vp.y - minY < trigger && dir.y < 0f)
        {
            dir.y = Mathf.Abs(dir.y);
            shouldTurn = true;
        }
        else if (maxY - vp.y < trigger && dir.y > 0f)
        {
            dir.y = -Mathf.Abs(dir.y);
            shouldTurn = true;
        }

        if (!shouldTurn)
        {
            return false;
        }

        if (dir.sqrMagnitude < 1e-8f)
        {
            dir = -heading.normalized;
        }

        escapeDirection = dir.normalized;
        return true;
    }

    /// <param name="selfPosition">현재 Rigidbody2D 위치</param>
    /// <param name="selfCollider">자기 자신(제외)</param>
    /// <param name="queryRadius">이웃 탐색 반경(월드 단위)</param>
    /// <param name="pushStrength">가속도 스케일</param>
    /// <param name="maxSpeed">반환 벡터 속도 상한(월드/초)</param>
    public static Vector2 Compute(
        Vector2 selfPosition,
        Collider2D selfCollider,
        float queryRadius,
        float pushStrength,
        float maxSpeed)
    {
        if (selfCollider == null || queryRadius <= 0f)
        {
            return Vector2.zero;
        }

        int count = Physics2D.OverlapCircleNonAlloc(selfPosition, queryRadius, Hits);
        if (count <= 0)
        {
            return Vector2.zero;
        }

        Vector2 sum = Vector2.zero;
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = Hits[i];
            if (hit == null || hit == selfCollider)
            {
                continue;
            }

            if (!IsSeparationPeer(hit))
            {
                continue;
            }

            Vector2 otherPos = hit.attachedRigidbody != null
                ? hit.attachedRigidbody.position
                : (Vector2)hit.transform.position;

            Vector2 diff = selfPosition - otherPos;
            float distSq = diff.sqrMagnitude;
            if (distSq < 1e-8f)
            {
                float angle = i * 2.3999632f;
                sum += new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                continue;
            }

            float inv = 1f / Mathf.Max(distSq, MinDistSq);
            sum += diff * inv;
        }

        Vector2 push = sum * pushStrength;
        if (push.sqrMagnitude > maxSpeed * maxSpeed)
        {
            push = push.normalized * maxSpeed;
        }

        return push;
    }

    /// <summary>
    /// 지정 방향으로 짧게 이동한다고 가정했을 때의 충돌 리스크(0 이상)를 추정합니다.
    /// 값이 높을수록 해당 방향이 혼잡/충돌 가능성이 큽니다.
    /// </summary>
    public static float EstimateCollisionRisk(
        Vector2 selfPosition,
        Vector2 direction,
        float lookAheadDistance,
        Collider2D selfCollider,
        float queryRadius)
    {
        if (selfCollider == null || queryRadius <= 0f || direction.sqrMagnitude < 1e-8f)
        {
            return 0f;
        }

        Vector2 predicted = selfPosition + direction.normalized * Mathf.Max(0f, lookAheadDistance);
        int count = Physics2D.OverlapCircleNonAlloc(predicted, queryRadius, Hits);
        if (count <= 0)
        {
            return 0f;
        }

        float risk = 0f;
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = Hits[i];
            if (hit == null || hit == selfCollider || !IsSeparationPeer(hit))
            {
                continue;
            }

            Vector2 otherPos = hit.attachedRigidbody != null
                ? hit.attachedRigidbody.position
                : (Vector2)hit.transform.position;
            float dist = Vector2.Distance(predicted, otherPos);
            float localRisk = 1f - Mathf.Clamp01(dist / queryRadius);
            risk += localRisk;
        }

        return risk;
    }

    private static bool IsSeparationPeer(Collider2D c)
    {
        return c.GetComponentInParent<SimulateUser>() != null || c.GetComponentInParent<PlayerUser>() != null;
    }
}
