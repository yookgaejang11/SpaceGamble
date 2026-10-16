using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

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
