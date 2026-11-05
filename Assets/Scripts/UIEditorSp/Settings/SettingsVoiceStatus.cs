using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 음성 서비스 연결 상태를 점 색과 글자로 보여준다.
/// SettingsPanel의 voiceStatusLabel은 연결되면 글자를 비우기 때문에,
/// 연결 문구를 계속 띄우려고 따로 둔다. (그래서 voiceStatusLabel 칸은 비워 둔다)
/// </summary>
public class SettingsVoiceStatus : MonoBehaviour
{
    public Image dot;
    public TextMeshProUGUI label;

    public string connectedText = "Voice service connected";
    public string connectingText = "Connecting to voice service...";

    public Color connectedColor = new Color(0.3f, 0.95f, 0.5f);
    public Color connectingColor = new Color(1f, 0.72f, 0.2f);

    void LateUpdate()
    {
        var vm = VoiceManager.Instance;
        bool ready = vm != null && vm.VivoxReady;

        if (dot != null) dot.color = ready ? connectedColor : connectingColor;
        if (label != null)
        {
            string want = ready ? connectedText : connectingText;
            if (label.text != want) label.text = want;
        }
    }
}
