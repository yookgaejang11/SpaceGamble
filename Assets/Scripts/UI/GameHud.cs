using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 화면 고정 HUD. 캔버스(Screen Space - Overlay)에 붙인다.
///
/// 월드 표시면(홀로그램, 보드, 패널)으로 못 가는 것만 여기에 둔다.
///   - 방 코드 표시 + 키(기본 C)로 복사
///
/// 커서가 잠겨 있는 1인칭이라 복사 버튼 대신 키를 쓴다.
/// 선택 버튼은 SeatPanel, Press는 PressTarget, 나가기는 PauseMenu가 맡는다.
/// GameClient를 구독할 필요가 없어서 ClientUIBase를 쓰지 않는다.
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


    void OnEnable()
    {
        if (RelayManager.Instance != null)
            RelayManager.Instance.OnRoomCodeSpawned += HandleRoomCode;

        ShowRoomCode();
    }

    void OnDisable()
    {
        if (RelayManager.Instance != null)
            RelayManager.Instance.OnRoomCodeSpawned -= HandleRoomCode;
    }

    void Update()
    {
        // 방에서 나가면 코드가 지워진다 → 표시도 같이 갱신
        ShowRoomCode();

        string code = GetCode();
        if (string.IsNullOrEmpty(code)) return;

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

    /// <summary>입력칸에 글자를 치는 중이면 C 키를 복사로 쓰지 않는다</summary>
    static bool TypingInInputField()
    {
        var es = EventSystem.current;
        if (es == null || es.currentSelectedGameObject == null) return false;
        return es.currentSelectedGameObject.GetComponent<TMP_InputField>() != null;
    }

    void HandleRoomCode(string code) => ShowRoomCode();

    void ShowRoomCode()
    {
        string code = GetCode();
        bool has = !string.IsNullOrEmpty(code);

        if (roomCodeGroup != null && roomCodeGroup.activeSelf != has)
            roomCodeGroup.SetActive(has);

        if (roomCodeText != null && has) roomCodeText.text = code;
    }
}