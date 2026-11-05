using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Toggle을 좌우로 움직이는 스위치 모양으로 보여준다.
/// SettingsPanel이 SetIsOnWithoutNotify로 값을 바꾸면 이벤트가 안 오므로 값을 직접 지켜본다.
/// </summary>
public class SettingsToggleSwitch : MonoBehaviour
{
    public Toggle toggle;
    public Image track;
    public RectTransform knob;

    [Tooltip("꺼짐/켜짐일 때 손잡이의 X 위치")]
    public float offX = 26f;
    public float onX = 80f;

    public Color offColor = new Color(0.20f, 0.23f, 0.27f);
    public Color onColor = new Color(0.16f, 0.83f, 1f);
    public float speed = 18f;

    float t;

    void OnEnable()
    {
        // 창이 열릴 때는 애니메이션 없이 바로 맞춘다
        t = toggle != null && toggle.isOn ? 1f : 0f;
        Apply();
    }

    void LateUpdate()
    {
        if (toggle == null) return;

        float want = toggle.isOn ? 1f : 0f;
        if (Mathf.Approximately(t, want)) return;

        // 일시정지 중에도 움직이도록 unscaled
        t = Mathf.MoveTowards(t, want, speed * Time.unscaledDeltaTime);
        Apply();
    }

    void Apply()
    {
        if (knob != null) knob.anchoredPosition = new Vector2(Mathf.Lerp(offX, onX, t), 0f);
        if (track != null) track.color = Color.Lerp(offColor, onColor, t);
    }
}
