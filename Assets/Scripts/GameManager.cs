using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("Canvas Settings")]
    [SerializeField] private int textureWidth = 1080;
    [SerializeField] private int textureHeight = 1920;
    private Color32 baseColor = Color.white;

    [Header("Brush Settings")]
    [SerializeField] private int brushRadius = 12;
    [SerializeField] private Color32 drawColor = new Color32(255, 0, 0, 255);
    [SerializeField] private float maxBrushScale = 1.5f;
    [SerializeField] private float brushScaleUpDuration = 1.0f;
    [SerializeField] private float changeColorTimer = 10f;

    [Header("References")]
    [SerializeField] private ColorAreaCalculator colorAreaCalculator;
    [Header("GamePlay Settings")]
    [SerializeField] private float gameplayDuration = 60f;
    private float currentGameplayTime = 0f;

    private Texture2D backgroundCanvasTexture;
    private SpriteRenderer canvasRenderer;
    private Color32[] pixelBuffer;

    private List<Color32> colorList = new List<Color32>(){
        Color.red,
        new Color32(255 , 107, 0, 255), //orange
        Color.yellow,
        Color.green,
        Color.cyan,
        Color.blue,
        Color.magenta,
    };
    private float currentTimer;
    private int colorIndex = 0;
    private float mouseHoldTime = 0f;
    private Vector2 mouseDownScreenPosition = Vector2.zero;
    private readonly Dictionary<int, (Color32 color, Vector2 position)> touchStartScreenPositions = new Dictionary<int, (Color32 color, Vector2 position)>();
    private Color32 currentColor = Color.white;
    void Start()
    {
        currentTimer = changeColorTimer;
        canvasRenderer = GetComponent<SpriteRenderer>();
        if (canvasRenderer == null)
        {
            canvasRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        InitializeCanvasTexture();
        FitCanvasToScreen();
    }

    void Update()
    {
        currentTimer -= Time.deltaTime;
        if (currentTimer <= 0)
        {
            currentTimer = changeColorTimer;
            colorIndex++;
            colorIndex %= colorList.Count;
            drawColor = colorList[colorIndex];
        }
        HandleInput();
    }

    private void InitializeCanvasTexture()
    {
        backgroundCanvasTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
        backgroundCanvasTexture.filterMode = FilterMode.Point;
        backgroundCanvasTexture.wrapMode = TextureWrapMode.Clamp;

        pixelBuffer = new Color32[textureWidth * textureHeight];
        for (int i = 0; i < pixelBuffer.Length; i++)
        {
            pixelBuffer[i] = baseColor;
        }

        backgroundCanvasTexture.SetPixels32(pixelBuffer);
        backgroundCanvasTexture.Apply();

        Sprite sprite = Sprite.Create(
            backgroundCanvasTexture,
            new Rect(0f, 0f, textureWidth, textureHeight),
            new Vector2(0.5f, 0.5f),
            100f
        );
        canvasRenderer.sprite = sprite;

        if (colorAreaCalculator != null)
        {
            colorAreaCalculator.Initialize(pixelBuffer.Length);
        }
    }

    private void FitCanvasToScreen()
    {
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic)
        {
            Debug.LogWarning("Main Camera가 없거나 Orthographic이 아닙니다. 화면 맞춤이 제한됩니다.");
            return;
        }

        float worldScreenHeight = cam.orthographicSize * 2f;
        float worldScreenWidth = worldScreenHeight * cam.aspect;
        Vector2 spriteSize = canvasRenderer.sprite.bounds.size;

        transform.localScale = new Vector3(
            worldScreenWidth / spriteSize.x,
            worldScreenHeight / spriteSize.y,
            1f
        );
    }

    private void HandleInput()
    {
#if UNITY_EDITOR
        if (Input.GetMouseButtonDown(0))
        {
            mouseHoldTime = 0f;
            mouseDownScreenPosition = Input.mousePosition; // 홀드 동안 중심 좌표 고정
            currentColor = drawColor;
            PaintAtScreenPosition(mouseDownScreenPosition, brushRadius, currentColor);
        }
        if (Input.GetMouseButton(0))
        {
            mouseHoldTime += Time.deltaTime;
            int scaledRadius = GetScaledBrushRadius(mouseHoldTime);
            PaintAtScreenPosition(mouseDownScreenPosition, scaledRadius, currentColor); // 드래그처럼 이동하며 그리지 않음
        }
        if (Input.GetMouseButtonUp(0))
        {
            mouseHoldTime = 0f;
            mouseDownScreenPosition = Vector2.zero;
        }
#endif

#if UNITY_ANDROID || UNITY_IOS
        if (Input.touchCount > 0)
        {
            foreach (var touch in Input.touches)
            {
                if (touch.phase == TouchPhase.Began)
                {
                    touchHoldTimes[touch.fingerId] = 0f;
                    touchStartScreenPositions[touch.fingerId] = (drawColor, touch.position); // 홀드 동안 중심 좌표 고정
                    PaintAtScreenPosition(touchStartScreenPositions[touch.fingerId].position, brushRadius, drawColor);
                }

                if (touch.phase == TouchPhase.Stationary || touch.phase == TouchPhase.Moved)
                {
                    if (!touchHoldTimes.ContainsKey(touch.fingerId))
                    {
                        touchHoldTimes[touch.fingerId] = 0f;
                    }

                    touchHoldTimes[touch.fingerId] += Time.deltaTime;
                    int scaledRadius = GetScaledBrushRadius(touchHoldTimes[touch.fingerId]);
                    if (touchStartScreenPositions.TryGetValue(touch.fingerId, out var startPos))
                    {
                        PaintAtScreenPosition(startPos.position, scaledRadius, startPos.color);
                    }
                }

                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    touchHoldTimes.Remove(touch.fingerId);
                    touchStartScreenPositions.Remove(touch.fingerId);
                }
            }

            return;
        }
#endif
    }

    private int GetScaledBrushRadius(float holdTime)
    {
        float normalized = brushScaleUpDuration <= 0f ? 1f : Mathf.Clamp01(holdTime / brushScaleUpDuration);
        float scale = Mathf.Lerp(1f, maxBrushScale, normalized);
        return Mathf.Max(1, Mathf.RoundToInt(brushRadius * scale));
    }

    private void PaintAtScreenPosition(Vector2 screenPosition, int radius, Color32 color)
    {
        int centerX = Mathf.RoundToInt((screenPosition.x / Screen.width) * (textureWidth - 1));
        int centerY = Mathf.RoundToInt((screenPosition.y / Screen.height) * (textureHeight - 1));
        PaintCircle(centerX, centerY, radius, color);
    }

    private void PaintCircle(int centerX, int centerY, int radius, Color32 targetColor)
    {
        int radiusSq = radius * radius;
        bool hasChanged = false;

        int startX = Mathf.Max(0, centerX - radius);
        int endX = Mathf.Min(textureWidth - 1, centerX + radius);
        int startY = Mathf.Max(0, centerY - radius);
        int endY = Mathf.Min(textureHeight - 1, centerY + radius);

        for (int y = startY; y <= endY; y++)
        {
            int dy = y - centerY;
            for (int x = startX; x <= endX; x++)
            {
                int dx = x - centerX;
                if ((dx * dx) + (dy * dy) > radiusSq)
                {
                    continue;
                }

                int index = y * textureWidth + x;
                Color32 oldColor = pixelBuffer[index];
                if (oldColor.Equals(targetColor))
                {
                    continue;
                }

                pixelBuffer[index] = targetColor;
                hasChanged = true;

                if (colorAreaCalculator != null)
                {
                    colorAreaCalculator.OnPixelColorChanged(oldColor, targetColor);
                }
            }
        }

        if (!hasChanged)
        {
            return;
        }

        backgroundCanvasTexture.SetPixels32(pixelBuffer);
        backgroundCanvasTexture.Apply();
    }
}
