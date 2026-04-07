using UnityEngine;
using System.Collections.Generic;

public class ColorAreaCalculator : MonoBehaviour
{
    private readonly Dictionary<Color32, int> colorPixelCounts = new Dictionary<Color32, int>();
    private int totalPixels;
    [SerializeField] private RatioPanelUI ratioPanelUIPrefabs;
    private Dictionary<Color32, RatioPanelUI> ratioPanelUIList = new Dictionary<Color32, RatioPanelUI>();
    [SerializeField] private Transform ratioPanelUIParent;
    private Color32 baseColor = Color.white;
    
    [Header("UI Ordering")]
    [SerializeField] private float reorderIntervalSeconds = 0.1f;
    private float lastReorderTime = -Mathf.Infinity;

    public void Initialize(int pixelCount)
    {
        totalPixels = pixelCount;
        colorPixelCounts.Clear();
        colorPixelCounts[baseColor] = pixelCount;
    }

    public void OnPixelColorChanged(Color32 oldColor, Color32 newColor)
    {
        if (oldColor.Equals(newColor))
        {
            return;
        }

        if (!colorPixelCounts.ContainsKey(oldColor))
        {
            colorPixelCounts[oldColor] = 0;

            var instance = Instantiate(ratioPanelUIPrefabs, ratioPanelUIParent);
            instance.Initialize(oldColor);
            ratioPanelUIList[oldColor] = instance;
            Debug.Log($"oldColor: {oldColor}");
        }

        if (!colorPixelCounts.ContainsKey(newColor))
        {
            colorPixelCounts[newColor] = 0;

            var instance2 = Instantiate(ratioPanelUIPrefabs, ratioPanelUIParent);
            instance2.Initialize(newColor);
            ratioPanelUIList[newColor] = instance2;
            Debug.Log($"newColor: {newColor}");
        }

        colorPixelCounts[oldColor] = Mathf.Max(0, colorPixelCounts[oldColor] - 1);
        colorPixelCounts[newColor] += 1;

        if (ratioPanelUIList.TryGetValue(oldColor, out RatioPanelUI ratioPanelUI))
        {
            ratioPanelUI.UpdateRatio(GetColorRatio(oldColor));

        }
        if (ratioPanelUIList.TryGetValue(newColor, out RatioPanelUI ratioPanelUI2))
        {
            ratioPanelUI2.UpdateRatio(GetColorRatio(newColor));
        }

        TryReorderPanelsByPixelCounts();
    }

    private void TryReorderPanelsByPixelCounts()
    {
        // 픽셀 단위 변경이 매우 잦기 때문에 UI 정렬은 일정 간격으로만 수행합니다.
        if (reorderIntervalSeconds <= 0f)
        {
            ReorderPanelsByPixelCounts();
            return;
        }

        if (Time.time - lastReorderTime < reorderIntervalSeconds)
        {
            return;
        }

        lastReorderTime = Time.time;
        ReorderPanelsByPixelCounts();
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
