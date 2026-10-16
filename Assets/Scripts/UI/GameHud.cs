using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 화면 고정 HUD. 캔버스(Screen Space - Overlay)에 붙인다.
///
/// 방 코드 표시 + 키(기본 C)로 복사.
///   - 페이즈가 waiting일 때만 보인다 (게임이 시작되면 숨김)
///   - 안 보일 때는 복사 키도 동작하지 않는다
///
/// 커서가 잠겨 있는 1인칭이라 복사 버튼 대신 키를 쓴다.
/// 선택 버튼은 SeatPanel, Press는 PressTarget, 나가기는 PauseMenu가 맡는다.
///
/// 이벤트를 구독하지 않고 매 프레임 GameClient.Local.curPhase를 읽는다.
/// (값 비교 몇 번이라 비용이 거의 없고, 방 입장/퇴장 타이밍을 신경 쓸 필요가 없다)
/// </summary>
public class GameHud : MonoBehaviour
{
    [Header("방 코드")]
    public GameObject roomCodeGroup;
    public TextMeshProUGUI roomCodeText;

    [Header("복사 키")]
    public KeyCode copyKey = KeyCode.C;
    [Tooltip("키 안내 / 복사 완료 문구를 띄울 텍스트. 없으면 비워두기")]
    public TextMeshProUGUI hintText;
    public string hintMessage = "[C] COPY";
    public string copiedMessage = "COPIED!";
    public float copiedShowTime = 1.5f;

    float copiedUntil;


    void Update()
    {
        string code = GetCode();
        bool shown = ShouldShow(code);

        if (roomCodeGroup != null && roomCodeGroup.activeSelf != shown)
            roomCodeGroup.SetActive(shown);

        if (!shown) return;

        if (roomCodeText != null) roomCodeText.text = code;

        if (Input.GetKeyDown(copyKey) && !TypingInInputField())
        {
            GUIUtility.systemCopyBuffer = code;
            copiedUntil = Time.unscaledTime + copiedShowTime;
        }

        if (hintText != null)
            hintText.text = (Time.unscaledTime < copiedUntil) ? copiedMessage : hintMessage;
    }


    static string GetCode()
        => (RelayManager.Instance != null) ? RelayManager.Instance.joinCode : null;

    /// <summary>코드가 있고, 내 클라이언트가 있고, 페이즈가 waiting일 때만</summary>
    static bool ShouldShow(string code)
    {
        if (string.IsNullOrEmpty(code)) return false;

        var client = GameClient.Local;
        return client != null && client.curPhase == GamePhase.waiting;
    }

    /// <summary>입력칸에 글자를 치는 중이면 C 키를 복사로 쓰지 않는다</summary>
    static bool TypingInInputField()
    {
        var es = EventSystem.current;
        if (es == null || es.currentSelectedGameObject == null) return false;
        return es.currentSelectedGameObject.GetComponent<TMP_InputField>() != null;
    }
}
