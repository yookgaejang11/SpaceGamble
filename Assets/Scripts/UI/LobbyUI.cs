using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyUI : MonoBehaviour
{
    [SerializeField] GameObject LobbyUi;
    [SerializeField] GameObject InGameUI;
    [SerializeField] Button CreateBtn;
    [SerializeField] Button JoinBtn;
    [SerializeField] Button LeaveBtn;
    public TMP_InputField joinCode;

    [Header("알림 팝업 (방 가입 실패 / 연결 끊김)")]
    [Tooltip("LobbyUi, InGameUI의 자식이 아니어야 한다 (그 둘은 입장/퇴장 때 꺼진다). 이 스크립트가 붙은 오브젝트의 자식을 권장")]
    [SerializeField] GameObject noticePanel;
    [Tooltip("제목 / 내용 텍스트. 제목은 안 쓰면 비워두기")]
    [SerializeField] TMP_Text noticeTitle;
    [SerializeField] TMP_Text noticeMessage;
    [SerializeField] Button noticeOkBtn;

    void Start()
    {
        // RelayManager.Awake가 끝난 뒤인 Start에서 구독한다
        var relay = RelayManager.Instance;
        if (relay != null)
        {
            relay.OnRoomConnectFailed += HandleConnectFailed;
            relay.OnRoomQuited += HandleRoomQuited;
        }

        if (LeaveBtn != null) LeaveBtn.onClick.AddListener(ExitBtn);
        if (noticeOkBtn != null) noticeOkBtn.onClick.AddListener(CloseNotice);
        CloseNotice();
    }

    void OnDestroy()
    {
        var relay = RelayManager.Instance;
        if (relay != null)
        {
            relay.OnRoomConnectFailed -= HandleConnectFailed;
            relay.OnRoomQuited -= HandleRoomQuited;
        }

        if (noticeOkBtn != null) noticeOkBtn.onClick.RemoveListener(CloseNotice);
    }


    // ────────────────────────── 알림 ──────────────────────────

    // 방 만들기 / 가입 실패
    void HandleConnectFailed(string reason) => ShowNotice("Connection Failed", reason);

    // 호스트가 나가는 등 연결이 끊겨서 방에서 나가짐
    void HandleRoomQuited(string reason) => ShowNotice("Connection Lost", reason);

    void ShowNotice(string title, string message)
    {
        if (noticePanel == null) return;

        if (noticeTitle != null) noticeTitle.text = title;
        if (noticeMessage != null) noticeMessage.text = message;
        noticePanel.SetActive(true);
    }

    public void CloseNotice()
    {
        if (noticePanel != null) noticePanel.SetActive(false);
    }


    // ────────────────────────── 버튼 ──────────────────────────

    public void ExitBtn()
    {
        Application.Quit();
    }

    public async void JoinRoom()
    {
        string code = joinCode != null ? joinCode.text.Trim() : "";
        if (code.Length == 0)
        {
            ShowNotice("Connection Failed", "방 코드를 입력해주세요.");
            return;
        }

        CloseNotice();
        await RelayManager.Instance.JoinRoom(code);     // 실패하면 OnRoomConnectFailed로 알림이 뜬다
    }

    public async void CreateRoomID()
    {
        CloseNotice();

        // [수정] CreateRoom을 한 번만 부른다 (두 번째 호출은 이미 방이 있어서 null을 돌려줬다)
        string code = await RelayManager.Instance.CreateRoom();
        if (code == null) return;                       // 실패 알림은 OnRoomConnectFailed가 처리
    }


    void Update()
    {
        if (RelayManager.Instance == null) return;

        if (RelayManager.Instance.isInRoom)
        {
            CreateBtn.interactable = false;
            JoinBtn.interactable = false;
            if (LobbyUi != null)
            {
                LobbyUi.SetActive(false);
            }
            if (InGameUI != null)
            {
                InGameUI.SetActive(true);
            }
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            CreateBtn.interactable = true;
            JoinBtn.interactable = true;
            if (LobbyUi != null)
            {
                LobbyUi.SetActive(true);
            }
            if (InGameUI != null)
            {
                InGameUI.SetActive(false);
            }
        }
    }
}