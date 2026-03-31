using UnityEngine;
using System.Collections.Generic;

public class ColorAreaCalculator : MonoBehaviour
{
    private readonly Dictionary<Color32, int> colorPixelCounts = new Dictionary<Color32, int>();
    private int totalPixels;

    public void Initialize(int pixelCount, Color32 baseColor)
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
        }

        if (!colorPixelCounts.ContainsKey(newColor))
        {
            colorPixelCounts[newColor] = 0;
        }

        colorPixelCounts[oldColor] = Mathf.Max(0, colorPixelCounts[oldColor] - 1);
        colorPixelCounts[newColor] += 1;
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

        return (float)count / totalPixels;
    }

    public void LogColorRatio(Color32 targetColor)
    {
        float ratio = GetColorRatio(targetColor) * 100f;
        Debug.Log($"Color [{targetColor.r}, {targetColor.g}, {targetColor.b}] Ratio: {ratio:F2}%");
    }
}
