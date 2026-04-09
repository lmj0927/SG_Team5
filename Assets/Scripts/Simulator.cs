using UnityEngine;

public class Simulator : MonoBehaviour
{
    [SerializeField] private SimulateUser simulateUserPrefab;
    [SerializeField] private int simulateUserCount = 100;
    [Tooltip("뷰포트 경계(0 또는 1) 밖으로 살짝 밀어 스폰합니다.")]
    [SerializeField] private float spawnOutsideViewport = 0.02f;

    private enum SpawnEdge
    {
        Left,
        Right,
        Bottom,
        Top
    }

    private void Start()
    {
        if (simulateUserPrefab == null)
        {
            Debug.LogWarning($"{nameof(Simulator)}: {nameof(simulateUserPrefab)}가 비어 있습니다.");
            return;
        }

        int count = Mathf.Max(0, simulateUserCount);
        for (int i = 0; i < count; i++)
        {
            SpawnSimulateUser();
        }
    }

    /// <summary>
    /// 화면 밖으로 나가 제거되기 직전에 <see cref="SimulateUser"/>에서 호출됩니다.
    /// </summary>
    public void NotifySimulateUserDestroyed()
    {
        if (simulateUserPrefab == null)
        {
            return;
        }

        SpawnSimulateUser();
    }

    private void SpawnSimulateUser()
    {
        SimulateUser instance = Instantiate(simulateUserPrefab, transform);
        GetEdgeSpawnWorldPositionAndRotation(out Vector3 worldPos, out float rotationZ);
        instance.transform.SetPositionAndRotation(worldPos, Quaternion.Euler(0f, 0f, rotationZ));
        instance.SetOwner(this);
        var stride = Random.Range(0.5f, 1.5f);
        instance.Initialize(stride, ColorDrawer.Instance.GetRandomColor());
    }

    /// <summary>
    /// 뷰포트 가장자리 근처에 스폰: x가 0 또는 1이면 y는 0~1 랜덤, y가 0 또는 1이면 x는 0~1 랜덤.
    /// <see cref="SimulateUser"/>가 로컬 <c>Vector2.right</c>로 이동하므로, 가장자리에 맞춰 Z 회전(도)을 줍니다.
    /// </summary>
    private void GetEdgeSpawnWorldPositionAndRotation(out Vector3 worldPos, out float rotationZ)
    {
        worldPos = new Vector3(0f, 0f, 0f);
        rotationZ = 0f;

        Camera cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        float outside = Mathf.Max(0f, spawnOutsideViewport);
        float vx;
        float vy;
        SpawnEdge edge;

        // 세로 변(x 고정 0 또는 1) vs 가로 변(y 고정 0 또는 1)
        if (Random.value < 0.5f)
        {
            bool left = Random.value < 0.5f;
            edge = left ? SpawnEdge.Left : SpawnEdge.Right;
            vx = left ? 0f : 1f;
            vy = Random.Range(0f, 1f);
            if (left)
            {
                vx -= outside;
            }
            else
            {
                vx += outside;
            }
        }
        else
        {
            bool bottom = Random.value < 0.5f;
            edge = bottom ? SpawnEdge.Bottom : SpawnEdge.Top;
            vx = Random.Range(0f, 1f);
            vy = bottom ? 0f : 1f;
            if (bottom)
            {
                vy -= outside;
            }
            else
            {
                vy += outside;
            }
        }

        float depth = Mathf.Abs(cam.transform.position.z);
        if (depth < 0.01f)
        {
            depth = 10f;
        }

        worldPos = cam.ViewportToWorldPoint(new Vector3(vx, vy, depth));

        // 로컬 right가 화면 안쪽으로 향하도록 (Straight = Vector2.right 기준)
        rotationZ = edge switch
        {
            SpawnEdge.Left => Random.Range(-45f, 45f),
            SpawnEdge.Bottom => Random.Range(45f, 135f),
            // 오른쪽: 대략 화면 중심(180°) 쪽으로
            SpawnEdge.Right => Random.Range(135f, 225f),
            // 위쪽: 아래쪽(45~135)과 대칭으로 아래를 향하는 범위
            SpawnEdge.Top => Random.Range(225f, 315f),
            _ => 0f
        };
    }
}
