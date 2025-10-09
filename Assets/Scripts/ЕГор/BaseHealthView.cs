using System;
using UnityEngine;
using UnityEngine.UI;

public class BaseHealthView : MonoBehaviour
{
    [SerializeField] private BaseTarget baseTarget;
    [SerializeField] private Slider _sliderHealth;
    private void OnDisable()
    {
        baseTarget.OnHealthChanged -= HandleHealthChanged;
    }
    private void OnEnable()
    {
        baseTarget.OnHealthChanged += HandleHealthChanged;
    }

    private void HandleHealthChanged(float obj)
    {
        _sliderHealth.value= obj;

    }
}
