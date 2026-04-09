using UnityEngine;
using System.Collections.Generic;

public class ColorAreaCalculator : MonoBehaviour
{
    private readonly Dictionary<Color32, int> colorPixelCounts = new Dictionary<Color32, int>();
    private int totalPixels;

    /// <summary>픽셀별 즉시 UI 갱신 대신 모았다가 <see cref="FlushPendingPixelChanges"/>에서 한 번에 처리합니다.</summary>
    private readonly List<(Color32 oldColor, Color32 newColor)> pendingPixelChanges = new List<(Color32, Color32)>(8192);
    private readonly HashSet<Color32> touchedColorsScratch = new HashSet<Color32>();
    [SerializeField] private RatioPanelUI ratioPanelUIPrefabs;
    private Dictionary<Color32, RatioPanelUI> ratioPanelUIList = new Dictionary<Color32, RatioPanelUI>();
    [SerializeField] private Transform ratioPanelUIParent;
    private Color32 baseColor = Color.white;

    public void Initialize(int pixelCount)
    {
        totalPixels = pixelCount;
        colorPixelCounts.Clear();
        colorPixelCounts[baseColor] = pixelCount;
        pendingPixelChanges.Clear();
    }

    /// <summary>그리기 루프에서 호출: 큐에만 쌓고, UI는 <see cref="FlushPendingPixelChanges"/>에서 처리합니다.</summary>
    public void RegisterPixelColorChange(Color32 oldColor, Color32 newColor)
    {
        if (oldColor.Equals(newColor))
        {
            return;
        }

        pendingPixelChanges.Add((oldColor, newColor));
    }

    /// <summary>프레임 끝 등에서 한 번 호출해 누적된 색 변화를 집계·UI 반영합니다.</summary>
    public void FlushPendingPixelChanges()
    {
        if (pendingPixelChanges.Count == 0)
        {
            return;
        }

        foreach (var delta in pendingPixelChanges)
        {
            EnsureColorKeyAndPanel(delta.oldColor);
            EnsureColorKeyAndPanel(delta.newColor);
        }

        foreach (var delta in pendingPixelChanges)
        {
            colorPixelCounts[delta.oldColor] = Mathf.Max(0, colorPixelCounts[delta.oldColor] - 1);
            colorPixelCounts[delta.newColor] += 1;
        }

        touchedColorsScratch.Clear();
        foreach (var delta in pendingPixelChanges)
        {
            touchedColorsScratch.Add(delta.oldColor);
            touchedColorsScratch.Add(delta.newColor);
        }

        pendingPixelChanges.Clear();

        foreach (var color in touchedColorsScratch)
        {
            if (ratioPanelUIList.TryGetValue(color, out RatioPanelUI panel))
            {
                panel.UpdateRatio(GetColorRatio(color));
            }
        }

        ReorderPanelsByPixelCounts();
    }

    private void EnsureColorKeyAndPanel(Color32 color)
    {
        if (colorPixelCounts.ContainsKey(color))
        {
            return;
        }

        colorPixelCounts[color] = 0;
        var instance = Instantiate(ratioPanelUIPrefabs, ratioPanelUIParent);
        instance.Initialize(color);
        ratioPanelUIList[color] = instance;
    }

    private void ReorderPanelsByPixelCounts()
    {
        // ratioPanelUIList의 value(RatioPanelUI)만 sibling 순서를 재정렬합니다.
        var panels = new List<(Color32 color, RatioPanelUI panel)>(ratioPanelUIList.Count);
        foreach (var kvp in ratioPanelUIList)
        {
            if (kvp.Value != null)
            {
                panels.Add((kvp.Key, kvp.Value));
            }
        }

        // 픽셀 수가 많은 색이 위(앞쪽)에 오도록 내림차순 정렬
        panels.Sort((a, b) =>
        {
            int countA = colorPixelCounts.TryGetValue(a.color, out int vA) ? vA : 0;
            int countB = colorPixelCounts.TryGetValue(b.color, out int vB) ? vB : 0;

            int byCount = countB.CompareTo(countA);
            if (byCount != 0) return byCount;

            // 동률일 때는 색 값으로 안정적으로(결정적으로) 정렬
            int keyA = (a.color.r << 16) | (a.color.g << 8) | a.color.b;
            int keyB = (b.color.r << 16) | (b.color.g << 8) | b.color.b;
            return keyB.CompareTo(keyA);
        });

        // 패널들이 아닌 다른 자식이 있어도, 패널들의 "블록" 범위 안에서만 sibling 인덱스를 바꾸기 위함
        int minSiblingIndex = int.MaxValue;
        for (int i = 0; i < panels.Count; i++)
        {
            minSiblingIndex = Mathf.Min(minSiblingIndex, panels[i].panel.transform.GetSiblingIndex());
        }

        for (int i = 0; i < panels.Count; i++)
        {
            panels[i].panel.transform.SetSiblingIndex(minSiblingIndex + i);
        }
    }

    public float GetColorRatio(Color32 targetColor)
    {
        if (totalPixels <= 0)
        {
            return 0f;
        }

        int count = 0;
        if (colorPixelCounts.TryGetValue(targetColor, out int value))
        {
            count = value;
        }

        return (float)count / totalPixels * 100f;
    }

    /// <summary>
    /// 캔버스를 단색으로 되돌린 뒤 집계·비율 UI를 처음 상태와 같이 맞출 때 사용합니다.
    /// </summary>
    public void ResetPanelsAndInitialize(int pixelCount)
    {
        foreach (var kvp in ratioPanelUIList)
        {
            if (kvp.Value != null)
            {
                Destroy(kvp.Value.gameObject);
            }
        }

        ratioPanelUIList.Clear();
        Initialize(pixelCount);
    }

    public void GetMostPaintedColors(List<Color32> results)
    {
        if (results == null)
        {
            return;
        }

        results.Clear();
        int maxCount = 0;

        foreach (var kvp in colorPixelCounts)
        {
            Color32 color = kvp.Key;
            int count = kvp.Value;

            // "칠해진 색" 기준이므로 배경색/0개는 제외
            if (color.Equals(baseColor) || count <= 0)
            {
                continue;
            }

            if (count > maxCount)
            {
                maxCount = count;
                results.Clear();
                results.Add(color);
                continue;
            }

            if (count == maxCount && maxCount > 0)
            {
                results.Add(color);
            }
        }
    }
}
