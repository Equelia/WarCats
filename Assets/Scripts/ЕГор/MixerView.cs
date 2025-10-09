using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class MixerView : MonoBehaviour
{
    [SerializeField] private AudioMixer _audioMixer;
    [SerializeField] private Slider _sliderGeneral;
    [SerializeField] private Slider _sliderMusic;
    [SerializeField] private Slider _sliderFX;
    private void Awake()
    {
        _sliderGeneral.onValueChanged.AddListener((float value)=>_audioMixer.SetFloat("Master", value <= 0.0001f ? -80f : Mathf.Log10(value) * 20));
        _sliderMusic.onValueChanged.AddListener((float value)=>_audioMixer.SetFloat("Music", value <= 0.0001f ? -80f : Mathf.Log10(value) * 20));
        _sliderFX.onValueChanged.AddListener((float value)=>_audioMixer.SetFloat("FX", value <= 0.0001f ? -80f : Mathf.Log10(value) * 20));
    }
}
