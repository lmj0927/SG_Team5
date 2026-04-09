using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimulateUser : MonoBehaviour
{
    [SerializeField] private float stride;
    [SerializeField] private Color32 color;
    [SerializeField] private MoveDirection moveDirection;
    [SerializeField] private float strideRate = 1f;
    private float currentStrideRate = 0f;
    private float theta;
    private Coroutine drawColorCoroutine;
    private Vector2 startPosition;
    private Simulator ownerSimulator;
    /// <summary>뷰포트 밖에서 스폰될 수 있으므로, 한 번이라도 화면 안에 들어온 뒤에만 밖으로 나갈 때 제거합니다.</summary>
    private bool hasEnteredViewport;

    public void SetOwner(Simulator simulator)
    {
        ownerSimulator = simulator;
    }

    void Start()
    {
        moveDirection = Random.value < 0.5f ? MoveDirection.Straight : MoveDirection.Circle;
    }

    void Update()
    {
        currentStrideRate += Time.deltaTime;
        if (currentStrideRate >= strideRate)
        {
            currentStrideRate = 0f;
            if (drawColorCoroutine != null)
            {
                StopCoroutine(drawColorCoroutine);
            }
            startPosition = transform.position;
            drawColorCoroutine = StartCoroutine(DrawColor());
        }
        switch (moveDirection)
        {
            case MoveDirection.Straight:
                transform.Translate(Vector2.right * stride * Time.deltaTime);
                break;
            case MoveDirection.Circle:
                theta += Mathf.PI * 2 / 50 *  Time.deltaTime;
                transform.Translate(new Vector2(Mathf.Cos(theta), Mathf.Sin(theta)) * stride * Time.deltaTime);
                break;
        }

        UpdateViewportEnteredState();

        if (hasEnteredViewport && IsOutsideViewport())
        {
            ownerSimulator?.NotifySimulateUserDestroyed();
            Destroy(gameObject);
        }
    }

    private void UpdateViewportEnteredState()
    {
        if (hasEnteredViewport)
        {
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        Vector3 vp = cam.WorldToViewportPoint(transform.position);
        if (vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f)
        {
            hasEnteredViewport = true;
        }
    }

    private bool IsOutsideViewport()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            return false;
        }

        Vector3 vp = cam.WorldToViewportPoint(transform.position);
        return vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f;
    }

    IEnumerator DrawColor()
    {
        var timer = 0f;
        while (timer < strideRate)
        {
            ColorDrawer.Instance.PaintAtWorldPosition(startPosition, ColorDrawer.Instance.GetScaledBrushRadius(timer), color);
            timer += Time.deltaTime;
            yield return null;
        }
    }

    public void Initialize(float stride, Color32 color)
    {
        this.stride = stride;
        strideRate = stride;
        this.color = color;
    }
}

public enum MoveDirection
{
    Straight,
    Circle,
}
