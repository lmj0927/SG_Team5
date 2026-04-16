using System.Collections;
using UnityEngine;

/// <summary>
/// 플레이어 입력으로 월드 이동하며 캔버스에 칠합니다.
/// 이동 방향이 있을 때 <see cref="Transform.up"/>이 그 방향을 가리키도록 회전합니다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class PlayerUser : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private Color32 color = new Color32(255, 0, 0, 255);
    [SerializeField] private float strideRate = 1f;
    [Tooltip("뷰포트 가장자리 안쪽 여백. SimulateUser와 동일한 방식으로 화면 밖 이탈을 막습니다.")]
    [SerializeField] private float viewportEdgeInset = 0.02f;
    [SerializeField] private float wallEscapeTriggerViewport = 0.08f;
    [SerializeField] private float wallRepulsionStrength = 1.1f;
    [Header("Separation (Phase 2)")]
    [SerializeField] private float separationQueryRadius = 1.2f;
    [SerializeField] private float separationPushStrength = 6f;
    [SerializeField] private float maxSeparationSpeed = 10f;

    private float currentStrideRate;
    private Vector2 startPosition;
    private Coroutine drawColorCoroutine;
    private Rigidbody2D rb;
    private CircleCollider2D selfCollider;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.freezeRotation = false;
        

        selfCollider = GetComponent<CircleCollider2D>();
        selfCollider.isTrigger = true;
    }

    /// <summary>라운드 리셋 시 strideRate 타이머·그리기 코루틴을 끊어, 흰 캔버스에 이전 위치가 찍히지 않게 합니다.</summary>
    public void ResetForRound(Vector3 position, Quaternion rotation)
    {
        if (drawColorCoroutine != null)
        {
            StopCoroutine(drawColorCoroutine);
            drawColorCoroutine = null;
        }

        currentStrideRate = 0f;
        startPosition = new Vector2(position.x, position.y);
        transform.SetPositionAndRotation(position, rotation);

        if (rb != null)
        {
            rb.position = new Vector2(position.x, position.y);
            rb.rotation = rotation.eulerAngles.z;
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    private void Update()
    {
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
        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        Vector2 desired = Vector2.zero;

        if (input.sqrMagnitude > 1e-6f)
        {
            Vector2 direction = input.normalized;
            desired = direction * moveSpeed;
            transform.up = new Vector3(direction.x, direction.y, 0f);
        }

        Vector2 separation = AgentSeparation2D.Compute(
            rb.position,
            selfCollider,
            separationQueryRadius,
            separationPushStrength,
            maxSeparationSpeed);

        Vector2 velocity = desired + separation;
        Camera cam = Camera.main;
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
                velocity = escapeDir * Mathf.Max(0.01f, moveSpeed) + wallRepulsion;
                transform.up = new Vector3(escapeDir.x, escapeDir.y, 0f);
            }
        }

        Vector2 delta = velocity * Time.fixedDeltaTime;
        Vector3 next = new Vector3(rb.position.x + delta.x, rb.position.y + delta.y, transform.position.z);

        rb.MovePosition(new Vector2(next.x, next.y));
    }

    private IEnumerator DrawColor()
    {
        ColorDrawer drawer = ColorDrawer.Instance;
        if (drawer == null)
        {
            yield break;
        }

        float timer = 0f;
        while (timer < strideRate)
        {
            drawer.PaintAtWorldPosition(startPosition, drawer.GetScaledBrushRadius(timer), color);
            timer += Time.deltaTime;
            yield return null;
        }
    }

    public void Initialize(float moveSpeed, float paintInterval, Color32 paintColor)
    {
        this.moveSpeed = moveSpeed;
        strideRate = Mathf.Max(0.01f, paintInterval);
        color = paintColor;
    }
}
