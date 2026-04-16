using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ColorDrawer : Singleton<ColorDrawer>
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
    [SerializeField] private Simulator simulator;
    [Header("GamePlay Settings")]
    [SerializeField] private float gameplayDuration = 60f;
    [Tooltip("종료 연출: 왼쪽 하단(0,0)에서 원 반지름이 초당 증가하는 속도(텍스처 픽셀 기준).")]
    [SerializeField] private float dominantCoverRadiusPerSecond = 900f;
    [SerializeField] private TMP_Text victoryText;
    [Tooltip("Victory 텍스트 알파가 0→1로 올라가는 시간(초)")]
    [SerializeField] private float victoryFadeInDuration = 1f;
    [Tooltip("Victory를 띄운 뒤 리셋까지 대기 시간(초)")]
    [SerializeField] private float victoryResetDelaySeconds = 2f;
    private float currentGameplayTime = 0f;

    private enum CanvasGameplayPhase
    {
        Normal,
        DominantCoverSweep,
        VictoryHold,
    }

    private CanvasGameplayPhase gameplayPhase = CanvasGameplayPhase.Normal;
    private float dominantSweepRadius;
    private Color32 dominantSweepColor;
    private readonly List<Color32> dominantColorQuery = new List<Color32>();
    private float victoryTimer;

    private Texture2D backgroundCanvasTexture;
    private SpriteRenderer canvasRenderer;
    private Color32[] pixelBuffer;
    /// <summary>부분 <see cref="Texture2D.SetPixels32(int,int,int,int,Color32[])"/> 업로드용 재사용 버퍼.</summary>
    private Color32[] regionUploadBuffer;

    // RGB
    private List<Color32> colorList = new List<Color32>(){
        Color.red,
        Color.green,
        Color.blue,
    };
    private float currentTimer;
    private int colorIndex = 0;
    //private float mouseHoldTime = 0f;
    private Vector2 mouseDownScreenPosition = Vector2.zero;
    private readonly Dictionary<int, float> touchHoldTimes = new Dictionary<int, float>();
    private readonly Dictionary<int, (Color32 color, Vector2 position)> touchStartScreenPositions = new Dictionary<int, (Color32 color, Vector2 position)>();
    private Color32 currentColor = Color.white;
    protected override void Initialize()
    {
        base.Initialize();
        currentTimer = changeColorTimer;
        canvasRenderer = GetComponent<SpriteRenderer>();
        if (canvasRenderer == null)
        {
            canvasRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        InitializeCanvasTexture();
        FitCanvasToScreen();

        if (victoryText != null)
        {
            SetVictoryTextAlpha(0f);
            victoryText.gameObject.SetActive(false);
        }

        if (simulator == null)
        {
            simulator = FindFirstObjectByType<Simulator>();
        }
    }

    void Update()
    {
        switch (gameplayPhase)
        {
            case CanvasGameplayPhase.Normal:
                UpdateNormalGameplay();
                break;
            case CanvasGameplayPhase.DominantCoverSweep:
                UpdateDominantCoverSweep();
                break;
            case CanvasGameplayPhase.VictoryHold:
                UpdateVictoryHold();
                break;
        }
    }

    private void UpdateNormalGameplay()
    {
        currentTimer -= Time.deltaTime;
        if (currentTimer <= 0)
        {
            currentTimer = changeColorTimer;
            colorIndex++;
            colorIndex %= colorList.Count;
            drawColor = colorList[colorIndex];
        }

        currentGameplayTime += Time.deltaTime;
        if (gameplayDuration > 0f && currentGameplayTime >= gameplayDuration)
        {
            BeginDominantCoverEndgame();
            return;
        }

        //HandleInput();
    }

    private void BeginDominantCoverEndgame()
    {
        if (colorAreaCalculator != null)
        {
            colorAreaCalculator.FlushPendingPixelChanges();
            dominantColorQuery.Clear();
            colorAreaCalculator.GetMostPaintedColors(dominantColorQuery);
        }

        if (dominantColorQuery.Count > 0)
        {
            dominantSweepColor = dominantColorQuery[0];
        }
        else
        {
            dominantSweepColor = drawColor;
        }

        gameplayPhase = CanvasGameplayPhase.DominantCoverSweep;
        dominantSweepRadius = 0f;
    }

    private void UpdateDominantCoverSweep()
    {
        int maxR = GetMaxCoverRadiusFromBottomLeft();
        dominantSweepRadius += dominantCoverRadiusPerSecond * Time.deltaTime;
        int r = Mathf.Clamp(Mathf.CeilToInt(dominantSweepRadius), 1, maxR);

        PaintCircle(0, 0, r, dominantSweepColor, registerWithCalculator: false);

        if (dominantSweepRadius >= maxR)
        {
            BeginVictoryHold();
        }
    }

    private void BeginVictoryHold()
    {
        gameplayPhase = CanvasGameplayPhase.VictoryHold;
        victoryTimer = 0f;

        if (victoryText == null)
        {
            return;
        }

        victoryText.gameObject.SetActive(true);
        victoryText.text = $"{GetRgbColorName(dominantSweepColor)} Victory";
        victoryText.color = new Color(255, 255, 255, 0f);
        SetVictoryTextAlpha(0f);
    }

    private void UpdateVictoryHold()
    {
        victoryTimer += Time.deltaTime;

        if (victoryText != null)
        {
            float dur = Mathf.Max(0.0001f, victoryFadeInDuration);
            float a = Mathf.Clamp01(victoryTimer / dur);
            SetVictoryTextAlpha(a);
        }

        if (victoryTimer >= Mathf.Max(0f, victoryResetDelaySeconds))
        {
            FinishEndgameResetToBase();
        }
    }

    private int GetMaxCoverRadiusFromBottomLeft()
    {
        long dx = textureWidth - 1;
        long dy = textureHeight - 1;
        return Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(dx * dx + dy * dy)));
    }

    private void FinishEndgameResetToBase()
    {
        if (victoryText != null)
        {
            SetVictoryTextAlpha(0f);
            victoryText.gameObject.SetActive(false);
        }

        for (int i = 0; i < pixelBuffer.Length; i++)
        {
            pixelBuffer[i] = baseColor;
        }

        backgroundCanvasTexture.SetPixels32(pixelBuffer);
        backgroundCanvasTexture.Apply(false, false);

        if (colorAreaCalculator != null)
        {
            colorAreaCalculator.ResetPanelsAndInitialize(pixelBuffer.Length);
        }

        if (simulator != null)
        {
            simulator.ResetRoundActors();
        }

        gameplayPhase = CanvasGameplayPhase.Normal;
        currentGameplayTime = 0f;
    }

    private void SetVictoryTextAlpha(float alpha)
    {
        if (victoryText == null)
        {
            return;
        }

        Color c = victoryText.color;
        c.a = Mathf.Clamp01(alpha);
        victoryText.color = c;
    }

    private string GetRgbColorName(Color32 color)
    {
        // Color32는 0~255 채널이기 때문에, 실제 사용 중인 RGB 리스트와 동일한 값을 직접 비교한다.
        if (color.r == 255 && color.g == 0 && color.b == 0) return "Red";
        if (color.r == 0 && color.g == 255 && color.b == 0) return "Green";
        if (color.r == 0 && color.g == 0 && color.b == 255) return "Blue";
        return "RGB";
    }

    private void LateUpdate()
    {
        if (gameplayPhase != CanvasGameplayPhase.Normal)
        {
            return;
        }

        if (colorAreaCalculator != null)
        {
            colorAreaCalculator.FlushPendingPixelChanges();
        }
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
        backgroundCanvasTexture.Apply(false, false);

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

//     private void HandleInput()
//     {
// #if UNITY_EDITOR
//         if (Input.GetMouseButtonDown(0))
//         {
//             mouseHoldTime = 0f;
//             mouseDownScreenPosition = Input.mousePosition; // 홀드 동안 중심 좌표 고정
//             currentColor = drawColor;
//             PaintAtScreenPosition(mouseDownScreenPosition, brushRadius, currentColor);
//         }
//         if (Input.GetMouseButton(0))
//         {
//             mouseHoldTime += Time.deltaTime;
//             int scaledRadius = GetScaledBrushRadius(mouseHoldTime);
//             PaintAtScreenPosition(mouseDownScreenPosition, scaledRadius, currentColor); // 드래그처럼 이동하며 그리지 않음
//         }
//         if (Input.GetMouseButtonUp(0))
//         {
//             mouseHoldTime = 0f;
//             mouseDownScreenPosition = Vector2.zero;
//         }
// #endif

// #if UNITY_ANDROID || UNITY_IOS
//         if (Input.touchCount > 0)
//         {
//             foreach (var touch in Input.touches)
//             {
//                 if (touch.phase == TouchPhase.Began)
//                 {
//                     touchHoldTimes[touch.fingerId] = 0f;
//                     touchStartScreenPositions[touch.fingerId] = (drawColor, touch.position); // 홀드 동안 중심 좌표 고정
//                     PaintAtScreenPosition(touchStartScreenPositions[touch.fingerId].position, brushRadius, drawColor);
//                 }

//                 if (touch.phase == TouchPhase.Stationary || touch.phase == TouchPhase.Moved)
//                 {
//                     if (!touchHoldTimes.ContainsKey(touch.fingerId))
//                     {
//                         touchHoldTimes[touch.fingerId] = 0f;
//                     }

//                     touchHoldTimes[touch.fingerId] += Time.deltaTime;
//                     int scaledRadius = GetScaledBrushRadius(touchHoldTimes[touch.fingerId]);
//                     if (touchStartScreenPositions.TryGetValue(touch.fingerId, out var startPos))
//                     {
//                         PaintAtScreenPosition(startPos.position, scaledRadius, startPos.color);
//                     }
//                 }

//                 if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
//                 {
//                     touchHoldTimes.Remove(touch.fingerId);
//                     touchStartScreenPositions.Remove(touch.fingerId);
//                 }
//             }

//             return;
//         }
// #endif
//     }

    public int GetScaledBrushRadius(float holdTime)
    {
        float normalized = brushScaleUpDuration <= 0f ? 1f : Mathf.Clamp01(holdTime / brushScaleUpDuration);
        float scale = Mathf.Lerp(1f, maxBrushScale, normalized);
        return Mathf.Max(1, Mathf.RoundToInt(brushRadius * scale));
    }

    private void PaintAtScreenPosition(Vector2 screenPosition, int radius, Color32 color)
    {
        if (gameplayPhase != CanvasGameplayPhase.Normal)
        {
            return;
        }

        int centerX = Mathf.RoundToInt((screenPosition.x / Screen.width) * (textureWidth - 1));
        int centerY = Mathf.RoundToInt((screenPosition.y / Screen.height) * (textureHeight - 1));
        PaintCircle(centerX, centerY, radius, color, registerWithCalculator: true);
    }

    /// <summary>
    /// 월드 좌표(예: SimulateUser의 transform.position)를 캔버스 스프라이트와 동일한 기준으로
    /// 텍스처 픽셀 좌표로 변환한 뒤 칠합니다. <see cref="PaintAtScreenPosition"/> 과 같은 텍스처 축(아래 y=0)을 사용합니다.
    /// </summary>
    public void PaintAtWorldPosition(Vector2 worldPosition, int radius, Color32 color)
    {
        if (gameplayPhase != CanvasGameplayPhase.Normal)
        {
            return;
        }

        if (!TryWorldToTexturePixel(worldPosition, out int centerX, out int centerY))
        {
            return;
        }

        PaintCircle(centerX, centerY, radius, color, registerWithCalculator: true);
    }

    /// <summary>
    /// AI 지각용 읽기 API: 월드 좌표의 현재 픽셀 색상을 반환합니다.
    /// </summary>
    public bool TryGetPixelColorAtWorld(Vector2 worldPosition, out Color32 color)
    {
        color = baseColor;
        if (gameplayPhase != CanvasGameplayPhase.Normal)
        {
            return false;
        }

        if (!TryWorldToTexturePixel(worldPosition, out int px, out int py))
        {
            return false;
        }

        int index = py * textureWidth + px;
        if (index < 0 || index >= pixelBuffer.Length)
        {
            return false;
        }

        color = pixelBuffer[index];
        return true;
    }

    public bool IsBaseColor(Color32 color)
    {
        return color.Equals(baseColor);
    }

    public bool IsInNormalGameplayPhase()
    {
        return gameplayPhase == CanvasGameplayPhase.Normal;
    }

    /// <summary>
    /// 월드 점을 이 오브젝트의 스프라이트 로컬 경계 안에서 0~1로 정규화한 뒤 텍스처 인덱스로 변환합니다.
    /// </summary>
    private bool TryWorldToTexturePixel(Vector2 worldPosition, out int centerX, out int centerY)
    {
        centerX = 0;
        centerY = 0;

        if (canvasRenderer == null || canvasRenderer.sprite == null)
        {
            return false;
        }

        Bounds spriteBounds = canvasRenderer.sprite.bounds;
        if (spriteBounds.size.x <= Mathf.Epsilon || spriteBounds.size.y <= Mathf.Epsilon)
        {
            return false;
        }

        // 스프라이트가 놓인 평면(z)에 맞춰 역변환 (2D에서 transform.position.z 기준)
        Vector3 worldPoint = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);

        // 로컬 경계: min.y = 아래쪽, max.y = 위쪽 → PaintAtScreenPosition 과 같이 아래가 텍스처 y=0
        float nx = Mathf.InverseLerp(spriteBounds.min.x, spriteBounds.max.x, localPoint.x);
        float ny = Mathf.InverseLerp(spriteBounds.min.y, spriteBounds.max.y, localPoint.y);
        nx = Mathf.Clamp01(nx);
        ny = Mathf.Clamp01(ny);

        centerX = Mathf.RoundToInt(nx * (textureWidth - 1));
        centerY = Mathf.RoundToInt(ny * (textureHeight - 1));
        centerX = Mathf.Clamp(centerX, 0, textureWidth - 1);
        centerY = Mathf.Clamp(centerY, 0, textureHeight - 1);
        return true;
    }

    private void PaintCircle(int centerX, int centerY, int radius, Color32 targetColor, bool registerWithCalculator)
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
            int dySq = dy * dy;
            int rowOffset = y * textureWidth;
            for (int x = startX; x <= endX; x++)
            {
                int dx = x - centerX;
                if ((dx * dx) + dySq > radiusSq)
                {
                    continue;
                }

                int index = rowOffset + x;
                Color32 oldColor = pixelBuffer[index];
                if (oldColor.Equals(targetColor))
                {
                    continue;
                }

                pixelBuffer[index] = targetColor;
                hasChanged = true;

                if (registerWithCalculator && colorAreaCalculator != null)
                {
                    colorAreaCalculator.RegisterPixelColorChange(oldColor, targetColor);
                }
            }
        }

        if (!hasChanged)
        {
            return;
        }

        UploadTextureRegion(startX, startY, endX, endY);
    }

    private void UploadTextureRegion(int startX, int startY, int endX, int endY)
    {
        int blockWidth = endX - startX + 1;
        int blockHeight = endY - startY + 1;
        int required = blockWidth * blockHeight;
        if (regionUploadBuffer == null || regionUploadBuffer.Length < required)
        {
            regionUploadBuffer = new Color32[required];
        }

        int write = 0;
        for (int y = startY; y <= endY; y++)
        {
            int row = y * textureWidth;
            for (int x = startX; x <= endX; x++)
            {
                regionUploadBuffer[write++] = pixelBuffer[row + x];
            }
        }

        backgroundCanvasTexture.SetPixels32(startX, startY, blockWidth, blockHeight, regionUploadBuffer);
        backgroundCanvasTexture.Apply(false, false);
    }

    public Color32 GetRandomColor()
    {
        return colorList[Random.Range(0, colorList.Count)];
    }
}
