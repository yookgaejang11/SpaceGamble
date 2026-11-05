using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 슬라이더 값을 % 글자로 보여준다. (게임 사운드 Master / BGM / SFX용)
/// SetValueWithoutNotify로 값이 바뀌어도 맞도록 이벤트 대신 값을 직접 지켜본다.
/// </summary>
public class SettingsSliderPercent : MonoBehaviour
{
    public Slider slider;
    public TextMeshProUGUI label;

    int shown = -1;

    void LateUpdate()
    {
        if (slider == null || label == null) return;

        int percent = Mathf.RoundToInt(slider.normalizedValue * 100f);
        if (percent == shown) return;

        shown = percent;
        label.text = percent + "%";
    }
}
