using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

/// <summary>
/// Binds a UI Slider (0–1 linear amplitude) to an exposed AudioMixer volume parameter (dB).
/// </summary>
[RequireComponent(typeof(Slider))]
public class MixerVolumeSlider : MonoBehaviour
{
    [SerializeField] private AudioMixer mixer;
    [SerializeField] private string exposedParameter = "MasterVolume";
    [SerializeField] private float minDb = -80f;
    [SerializeField] private float maxDb = 0f;
    [SerializeField] private float defaultSliderValue = MainMenuSettingsKeys.DefaultLinearVolume;
    [SerializeField] private string playerPrefsKey = MainMenuSettingsKeys.MasterVolume;
    [SerializeField] private bool loadFromPlayerPrefsOnEnable = true;

    private Slider _slider;
    private bool _isApplyingFromMixer;

    private void Awake()
    {
        _slider = GetComponent<Slider>();
        _slider.onValueChanged.AddListener(OnSliderChanged);
        if (mixer != null)
            GameAudioSettings.RegisterMixer(mixer);
    }

    private AudioMixer Mx => GameAudioSettings.ResolveMixer(mixer);

    private void OnEnable()
    {
        if (loadFromPlayerPrefsOnEnable)
            ApplyPlayerPrefsToMixerAndSlider();
        else
            ApplyMixerToSlider();
    }

    private void OnDestroy()
    {
        if (_slider != null)
            _slider.onValueChanged.RemoveListener(OnSliderChanged);
    }

    private void ApplyMixerToSlider()
    {
        var mx = Mx;
        if (_slider == null || mx == null || string.IsNullOrEmpty(exposedParameter)) return;

        if (!mx.GetFloat(exposedParameter, out float db))
        {
            float safeDefault = Mathf.Clamp(defaultSliderValue, _slider.minValue, _slider.maxValue);
            _isApplyingFromMixer = true;
            _slider.SetValueWithoutNotify(safeDefault);
            _isApplyingFromMixer = false;
            mx.SetFloat(exposedParameter, SliderValueToDb(safeDefault));
            return;
        }

        float sliderValue = DbToSliderValue(db);
        _isApplyingFromMixer = true;
        _slider.SetValueWithoutNotify(sliderValue);
        _isApplyingFromMixer = false;
    }

    private void ApplyPlayerPrefsToMixerAndSlider()
    {
        var mx = Mx;
        if (_slider == null || mx == null || string.IsNullOrEmpty(exposedParameter)) return;

        float v = PlayerPrefs.GetFloat(playerPrefsKey, defaultSliderValue);
        v = Mathf.Clamp(v, _slider.minValue, _slider.maxValue);

        _isApplyingFromMixer = true;
        _slider.SetValueWithoutNotify(v);
        _isApplyingFromMixer = false;

        mx.SetFloat(exposedParameter, SliderValueToDb(v));
    }

    private void OnSliderChanged(float sliderValue)
    {
        var mx = Mx;
        if (_isApplyingFromMixer || mx == null || string.IsNullOrEmpty(exposedParameter)) return;
        mx.SetFloat(exposedParameter, SliderValueToDb(sliderValue));
    }

    public void SaveToPlayerPrefs()
    {
        if (_slider == null) _slider = GetComponent<Slider>();
        PlayerPrefs.SetFloat(playerPrefsKey, _slider.value);
    }

    /// <summary>Re-reads PlayerPrefs into the mixer and slider (e.g. after <see cref="GameSettingsRuntime.ApplyAllSavedSettingsToEngine"/>).</summary>
    public void RefreshFromPlayerPrefs()
    {
        if (_slider == null) _slider = GetComponent<Slider>();
        if (loadFromPlayerPrefsOnEnable)
            ApplyPlayerPrefsToMixerAndSlider();
        else
            ApplyMixerToSlider();
    }

    private float SliderValueToDb(float sliderValue)
    {
        float linear = Mathf.Clamp(sliderValue, _slider.minValue, _slider.maxValue);
        linear = Mathf.Max(linear, 1e-6f);
        float db = 20f * Mathf.Log10(linear);
        return Mathf.Clamp(db, minDb, maxDb);
    }

    private float DbToSliderValue(float db)
    {
        float linear = Mathf.Pow(10f, db / 20f);
        return Mathf.Clamp(linear, _slider.minValue, _slider.maxValue);
    }
}
