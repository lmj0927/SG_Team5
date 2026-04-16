using UnityEngine;

/// <summary>
/// 메인 카메라 뷰포트에 대응하는 월드 XY 범위 계산 및 위치 클램프.
/// 뷰포트 이탈 방지는 <see cref="SimulateUser"/> 등 이동 주체에서 호출합니다.
/// </summary>
public static class ArenaBounds
{
    /// <summary>
    /// 뷰포트 [inset, 1-inset] 구간을 월드 XY AABB로 변환합니다.
    /// </summary>
    /// <param name="camera">보통 <c>Camera.main</c></param>
    /// <param name="worldZ">클램프할 오브젝트의 월드 Z (평면 깊이)</param>
    /// <param name="viewportInset">0~0.5, 뷰포트 가장자리 안쪽 여백 (0.02 ≈ 2%)</param>
    public static bool TryGetViewportWorldBoundsXY(Camera camera, float worldZ, float viewportInset, out Vector2 min, out Vector2 max)
    {
        min = default;
        max = default;

        if (camera == null)
        {
            return false;
        }

        float inset = Mathf.Clamp(viewportInset, 0f, 0.49f);
        float minVx = inset;
        float maxVx = 1f - inset;
        float minVy = inset;
        float maxVy = 1f - inset;

        if (minVx >= maxVx || minVy >= maxVy)
        {
            return false;
        }

        float depth = Mathf.Abs(camera.transform.position.z - worldZ);
        if (depth < 0.01f)
        {
            depth = 10f;
        }

        Vector3 v00 = camera.ViewportToWorldPoint(new Vector3(minVx, minVy, depth));
        Vector3 v10 = camera.ViewportToWorldPoint(new Vector3(maxVx, minVy, depth));
        Vector3 v01 = camera.ViewportToWorldPoint(new Vector3(minVx, maxVy, depth));
        Vector3 v11 = camera.ViewportToWorldPoint(new Vector3(maxVx, maxVy, depth));

        float minX = Mathf.Min(v00.x, v10.x, v01.x, v11.x);
        float maxX = Mathf.Max(v00.x, v10.x, v01.x, v11.x);
        float minY = Mathf.Min(v00.y, v10.y, v01.y, v11.y);
        float maxY = Mathf.Max(v00.y, v10.y, v01.y, v11.y);

        min = new Vector2(minX, minY);
        max = new Vector2(maxX, maxY);
        return true;
    }

    /// <summary>월드 위치의 XY를 뷰포트(여백 적용) 안으로 제한합니다. Z는 유지합니다.</summary>
    public static Vector3 ClampWorldPositionToViewportXY(Camera camera, Vector3 worldPosition, float viewportInset)
    {
        if (!TryGetViewportWorldBoundsXY(camera, worldPosition.z, viewportInset, out Vector2 min, out Vector2 max))
        {
            return worldPosition;
        }

        float x = Mathf.Clamp(worldPosition.x, min.x, max.x);
        float y = Mathf.Clamp(worldPosition.y, min.y, max.y);
        return new Vector3(x, y, worldPosition.z);
    }

    /// <summary>
    /// 경계 근처에서 바깥쪽 속도 성분을 제거하고 안쪽으로 유도합니다.
    /// 사후 위치 클램프 반복으로 생기는 경계 핑퐁을 줄이기 위한 사전 속도 보정입니다.
    /// </summary>
    public static Vector2 ConstrainVelocityInsideViewportXY(
        Camera camera,
        Vector3 worldPosition,
        Vector2 velocity,
        float viewportInset,
        float boundaryMargin,
        float returnStrength,
        float outwardDamping = 0.25f)
    {
        if (!TryGetViewportWorldBoundsXY(camera, worldPosition.z, viewportInset, out Vector2 min, out Vector2 max))
        {
            return velocity;
        }

        float margin = Mathf.Max(0f, boundaryMargin);
        float strength = Mathf.Max(0f, returnStrength);
        float damping = Mathf.Clamp01(outwardDamping);
        Vector2 adjusted = velocity;

        // X축: 경계 바깥으로 향하는 성분을 제거하고, margin 안에서는 안쪽으로 살짝 당긴다.
        if (worldPosition.x <= min.x + margin && adjusted.x < 0f)
        {
            adjusted.x *= damping;
        }
        else if (worldPosition.x >= max.x - margin && adjusted.x > 0f)
        {
            adjusted.x *= damping;
        }

        if (margin > 1e-6f)
        {
            if (worldPosition.x < min.x + margin)
            {
                float t = 1f - Mathf.Clamp01((worldPosition.x - min.x) / margin);
                adjusted.x += t * strength;
            }
            else if (worldPosition.x > max.x - margin)
            {
                float t = 1f - Mathf.Clamp01((max.x - worldPosition.x) / margin);
                adjusted.x -= t * strength;
            }
        }

        // Y축 동일 처리
        if (worldPosition.y <= min.y + margin && adjusted.y < 0f)
        {
            adjusted.y *= damping;
        }
        else if (worldPosition.y >= max.y - margin && adjusted.y > 0f)
        {
            adjusted.y *= damping;
        }

        if (margin > 1e-6f)
        {
            if (worldPosition.y < min.y + margin)
            {
                float t = 1f - Mathf.Clamp01((worldPosition.y - min.y) / margin);
                adjusted.y += t * strength;
            }
            else if (worldPosition.y > max.y - margin)
            {
                float t = 1f - Mathf.Clamp01((max.y - worldPosition.y) / margin);
                adjusted.y -= t * strength;
            }
        }

        return adjusted;
    }

    /// <summary>
    /// 경계를 "가상 에이전트"처럼 보고, 가까워질수록 안쪽으로 밀어내는 반발 벡터를 계산합니다.
    /// </summary>
    public static Vector2 ComputeWallRepulsion(
        Camera camera,
        Vector3 worldPosition,
        float viewportInset,
        float repulsionRange,
        float repulsionStrength)
    {
        if (!TryGetViewportWorldBoundsXY(camera, worldPosition.z, viewportInset, out Vector2 min, out Vector2 max))
        {
            return Vector2.zero;
        }

        float range = Mathf.Max(0.001f, repulsionRange);
        float strength = Mathf.Max(0f, repulsionStrength);
        Vector2 repel = Vector2.zero;

        float leftDist = worldPosition.x - min.x;
        if (leftDist < range)
        {
            float t = 1f - Mathf.Clamp01(leftDist / range);
            repel.x += t * strength;
        }

        float rightDist = max.x - worldPosition.x;
        if (rightDist < range)
        {
            float t = 1f - Mathf.Clamp01(rightDist / range);
            repel.x -= t * strength;
        }

        float bottomDist = worldPosition.y - min.y;
        if (bottomDist < range)
        {
            float t = 1f - Mathf.Clamp01(bottomDist / range);
            repel.y += t * strength;
        }

        float topDist = max.y - worldPosition.y;
        if (topDist < range)
        {
            float t = 1f - Mathf.Clamp01(topDist / range);
            repel.y -= t * strength;
        }

        return repel;
    }
}
