using UnityEngine.UI;
using UnityEngine;

public class RatioPanelUI : MonoBehaviour
{
    [SerializeField] private Image ratioFillImage;
    private Slider ratioSlider;
    private Color baseFillColor;

    public void Initialize(Color32 color)
    {
        ratioSlider = GetComponent<Slider>();
        ratioSlider.maxValue = 100;
        baseFillColor = color;
        ratioFillImage.color = baseFillColor;
    }
    
    public void UpdateRatio(float ratio)
    {
        ratioSlider.value = ratio;
    }
}
