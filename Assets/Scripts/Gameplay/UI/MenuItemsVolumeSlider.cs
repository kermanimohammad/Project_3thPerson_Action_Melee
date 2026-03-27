using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

/// <summary>
/// Binds a UI Slider to an exposed AudioMixer volume parameter (dB).
/// The slider value is treated as linear amplitude (e.g. 0–0.14), not as a normalized 0–1 position.
/// </summary>
[RequireComponent(typeof(Slider))]
public class MenuItemsVolumeSlider : MonoBehaviour
{
    [SerializeField] private AudioMixer mixer;
    [SerializeField] private string exposedParameter = "MenuItemsVolume";
    [SerializeField] private float minDb = -80f;
    [SerializeField] private float maxDb = 0f;
    [SerializeField] private float defaultSliderValue = MainMenuSettingsKeys.DefaultMenuItemsVolume;

    private Slider _slider;
    private bool _isApplyingFromMixer;

    private void Awake()
    {
        _slider = GetComponent<Slider>();
        _slider.onValueChanged.AddListener(OnSliderChanged);
    }

    private void OnEnable()
    {
        ApplyMixerToSlider();
    }

    private void OnDestroy()
    {
        if (_slider != null)
            _slider.onValueChanged.RemoveListener(OnSliderChanged);
    }

    private void ApplyMixerToSlider()
    {
        if (_slider == null || mixer == null || string.IsNullOrEmpty(exposedParameter)) return;

        if (!mixer.GetFloat(exposedParameter, out float db))
        {
            float safeDefault = Mathf.Clamp(defaultSliderValue, _slider.minValue, _slider.maxValue);
            _isApplyingFromMixer = true;
            _slider.SetValueWithoutNotify(safeDefault);
            _isApplyingFromMixer = false;
            mixer.SetFloat(exposedParameter, SliderValueToDb(safeDefault));
            return;
        }

        float sliderValue = DbToSliderValue(db);

        _isApplyingFromMixer = true;
        _slider.SetValueWithoutNotify(sliderValue);
        _isApplyingFromMixer = false;
    }

    private void OnSliderChanged(float sliderValue)
    {
        if (_isApplyingFromMixer || mixer == null || string.IsNullOrEmpty(exposedParameter)) return;

        mixer.SetFloat(exposedParameter, SliderValueToDb(sliderValue));
    }

    public void SaveToPlayerPrefs()
    {
        if (_slider == null) _slider = GetComponent<Slider>();
        PlayerPrefs.SetFloat(MainMenuSettingsKeys.MenuItemsVolume, _slider.value);
    }

    private float SliderValueToDb(float sliderValue)
    {
        float linear = Mathf.Clamp(sliderValue, _slider.minValue, _slider.maxValue);
        // Silence at 0; avoid log(0).
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
