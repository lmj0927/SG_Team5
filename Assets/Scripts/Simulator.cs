using UnityEngine;

public class Simulator : MonoBehaviour
{
    [SerializeField] private SimulateUser simulateUserPrefab;
    [SerializeField] private PlayerUser playerUser;
    private const int SimulateUserCount = 8; // 빨강 2 + 파랑 3 + 초록 3

    [Header("Team Spawn Anchors (Viewport)")]
    [SerializeField] private Vector2 blueSpawnViewport = new Vector2(0.15f, 0.85f);   // 왼쪽 상단
    [SerializeField] private Vector2 greenSpawnViewport = new Vector2(0.85f, 0.85f);  // 오른쪽 상단
    [SerializeField] private Vector2 redSpawnViewport = new Vector2(0.5f, 0.15f);     // 중앙 하단
    [Tooltip("같은 팀 스폰 시 앵커 기준 viewport 오프셋 반경.")]
    [SerializeField] private float teamSpawnOffsetRadius = 0.035f;
    [Tooltip("SimulateUser별 이동 속도(stride) 스폰 시 랜덤 범위(월드/초).")]
    [SerializeField] private Vector2 simulateUserStrideRange = new Vector2(0.8f, 1.5f);

    private static readonly Color32[] TeamColors =
    {
        new Color32(255, 0, 0, 255),
        new Color32(0, 255, 0, 255),
        new Color32(0, 0, 255, 255),
    };

    private static readonly AgentRole[] TeamRoles =
    {
        AgentRole.Coverage,
        AgentRole.Denial,
        AgentRole.Defender,
    };

    private Vector3 initialPlayerPosition;
    private Quaternion initialPlayerRotation;
    private bool hasCachedInitialPlayer;

    private void Awake()
    {
        RespawnSimulateUsers();
    }

    private void Start()
    {
        CacheInitialPlayerTransform();
    }

    private void CacheInitialPlayerTransform()
    {
        if (playerUser == null)
        {
            hasCachedInitialPlayer = false;
            return;
        }

        initialPlayerPosition = playerUser.transform.position;
        initialPlayerRotation = playerUser.transform.rotation;
        hasCachedInitialPlayer = true;
    }

    public void ResetRoundActors()
    {
        RespawnSimulateUsers();
        ResetPlayerToInitialTransform();
    }

    private void RespawnSimulateUsers()
    {
        if (simulateUserPrefab == null)
        {
            Debug.LogWarning($"{nameof(Simulator)}: {nameof(simulateUserPrefab)}가 비어 있습니다.");
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning($"{nameof(Simulator)}: Main Camera를 찾지 못해 스폰할 수 없습니다.");
            return;
        }

        ClearSpawnedUsers();

        // 파랑 3명: 왼쪽 상단, 역할 중복 없음
        AgentRole[] blueRoles = GetShuffledRoles();
        for (int i = 0; i < 3; i++)
        {
            SpawnAt(cam, blueSpawnViewport, i, 3, TeamColors[2], blueRoles[i]);
        }

        // 초록 3명: 오른쪽 상단, 역할 중복 없음
        AgentRole[] greenRoles = GetShuffledRoles();
        for (int i = 0; i < 3; i++)
        {
            SpawnAt(cam, greenSpawnViewport, i, 3, TeamColors[1], greenRoles[i]);
        }

        // 빨강 2명(+플레이어 1명 가정): 중앙 하단, 3개 역할 중 중복 없는 2개 랜덤 선택
        AgentRole[] redTwoRoles = PickTwoDistinctRoles();
        for (int i = 0; i < 2; i++)
        {
            SpawnAt(cam, redSpawnViewport, i, 2, TeamColors[0], redTwoRoles[i]);
        }

        if (transform.childCount != SimulateUserCount)
        {
            Debug.LogWarning($"{nameof(Simulator)}: 예상 스폰 수({SimulateUserCount})와 실제({transform.childCount})가 다릅니다.");
        }
    }

    private void ClearSpawnedUsers()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child == null)
            {
                continue;
            }

            // Destroy()는 프레임 끝까지 지연되어, 리셋 직후 gameplayPhase가 Normal이 되면
            // 기존 에이전트가 한 번 더 칠할 수 있음. 즉시 제거로 방지합니다.
            if (Application.isPlaying)
            {
                DestroyImmediate(child.gameObject);
            }
            else
            {
                Destroy(child.gameObject);
            }
        }
    }

    private void ResetPlayerToInitialTransform()
    {
        if (!hasCachedInitialPlayer || playerUser == null)
        {
            return;
        }

        playerUser.ResetForRound(initialPlayerPosition, initialPlayerRotation);
    }

    private void SpawnAt(Camera cam, Vector2 viewportAnchor, int memberIndex, int memberCount, Color32 teamColor, AgentRole role)
    {
        SimulateUser instance = Instantiate(simulateUserPrefab, transform);
        Vector2 viewport = viewportAnchor + GetSpawnOffsetViewport(memberIndex, memberCount);
        Vector3 worldPos = ViewportToWorldAtSimulationPlane(cam, viewport);
        Quaternion rotation = GetFacingToCenterRotation(worldPos);
        instance.transform.SetPositionAndRotation(worldPos, rotation);
        Vector2 initialIntent = GetInitialIntentToCenter(worldPos);
        float minStride = Mathf.Min(simulateUserStrideRange.x, simulateUserStrideRange.y);
        float maxStride = Mathf.Max(simulateUserStrideRange.x, simulateUserStrideRange.y);
        float randomStride = Random.Range(minStride, maxStride);
        instance.Initialize(teamColor, role, initialIntent, randomStride);
    }

    private Vector2 GetSpawnOffsetViewport(int memberIndex, int memberCount)
    {
        int count = Mathf.Max(1, memberCount);
        if (count == 1 || teamSpawnOffsetRadius <= 0f)
        {
            return Vector2.zero;
        }

        // 팀 인원들을 작은 원에 균등 배치해 겹침을 줄입니다.
        float baseAngle = (Mathf.PI * 2f * memberIndex) / count;
        float radius = Mathf.Max(0f, teamSpawnOffsetRadius);
        return new Vector2(Mathf.Cos(baseAngle), Mathf.Sin(baseAngle)) * radius;
    }

    private static AgentRole[] GetShuffledRoles()
    {
        AgentRole[] roles = (AgentRole[])TeamRoles.Clone();
        for (int i = roles.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (roles[i], roles[j]) = (roles[j], roles[i]);
        }

        return roles;
    }

    private static AgentRole[] PickTwoDistinctRoles()
    {
        AgentRole[] shuffled = GetShuffledRoles();
        return new[] { shuffled[0], shuffled[1] };
    }

    private static Quaternion GetFacingToCenterRotation(Vector3 worldPos)
    {
        Vector2 toCenter = GetInitialIntentToCenter(worldPos);
        if (toCenter.sqrMagnitude < 1e-8f)
        {
            toCenter = Vector2.up;
        }

        float zAngle = Mathf.Atan2(toCenter.y, toCenter.x) * Mathf.Rad2Deg - 90f;
        return Quaternion.Euler(0f, 0f, zAngle);
    }

    private static Vector2 GetInitialIntentToCenter(Vector3 worldPos)
    {
        Vector2 toCenter = ((Vector2)Vector3.zero - (Vector2)worldPos);
        return toCenter.sqrMagnitude > 1e-8f ? toCenter.normalized : Vector2.up;
    }

    private static Vector3 ViewportToWorldAtSimulationPlane(Camera cam, Vector2 viewport)
    {
        float vx = Mathf.Clamp01(viewport.x);
        float vy = Mathf.Clamp01(viewport.y);
        float depth = Mathf.Abs(cam.transform.position.z);
        if (depth < 0.01f)
        {
            depth = 10f;
        }

        return cam.ViewportToWorldPoint(new Vector3(vx, vy, depth));
    }
}
