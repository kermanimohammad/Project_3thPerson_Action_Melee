using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Persists a generic float slider (e.g. sensitivity) to PlayerPrefs; load on enable, save via Apply.
/// </summary>
[RequireComponent(typeof(Slider))]
public class FloatSettingSlider : MonoBehaviour
{
    [SerializeField] private string playerPrefsKey = MainMenuSettingsKeys.MouseSensitivity;
    [SerializeField] private float defaultValue = MainMenuSettingsKeys.DefaultLinearVolume;

    private Slider _slider;

    private void Awake()
    {
        _slider = GetComponent<Slider>();
        _slider.onValueChanged.AddListener(OnSliderValueChanged);
    }

    private void OnDestroy()
    {
        if (_slider != null)
            _slider.onValueChanged.RemoveListener(OnSliderValueChanged);
    }

    private void OnSliderValueChanged(float value)
    {
        PlayerPrefs.SetFloat(playerPrefsKey, value);
    }

    private void OnEnable()
    {
        if (_slider == null) _slider = GetComponent<Slider>();
        float v = PlayerPrefs.GetFloat(playerPrefsKey, defaultValue);
        _slider.SetValueWithoutNotify(Mathf.Clamp(v, _slider.minValue, _slider.maxValue));
    }

    public void SaveToPlayerPrefs()
    {
        if (_slider == null) _slider = GetComponent<Slider>();
        PlayerPrefs.SetFloat(playerPrefsKey, _slider.value);
    }

    /// <summary>
    /// Updates slider UI from PlayerPrefs immediately (without saving).
    /// </summary>
    public void ForceRefreshFromPlayerPrefs()
    {
        if (_slider == null) _slider = GetComponent<Slider>();
        float v = PlayerPrefs.GetFloat(playerPrefsKey, defaultValue);
        _slider.SetValueWithoutNotify(Mathf.Clamp(v, _slider.minValue, _slider.maxValue));
    }
}
