using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using UnityEngine;
using System;

public class RatioPanelUI : MonoBehaviour
{
    [SerializeField] private Image ratioFillImage;
    private Slider ratioSlider;

    public void Initialize(Color32 color)
    {
        ratioSlider = GetComponent<Slider>();
        ratioSlider.maxValue = 100;
        ratioFillImage.color = color;
    }
    
    public void UpdateRatio(float ratio)
    {
        ratioSlider.value = ratio;
    }
}
