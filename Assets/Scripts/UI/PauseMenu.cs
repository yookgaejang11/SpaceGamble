using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ESC 일시정지 메뉴.
///
///   ESC        : 메뉴 열기 / 닫기 (열면 커서가 풀리고 시점 회전이 멈춘다)
///   Resume     : 메뉴 닫기
///   Exit       : 방 나가기 (RelayManager.LeaveRoom)
///
/// 멀티게임이라 실제로 게임이 멈추지는 않는다. 내 시점과 입력만 멈춘다.
///
/// 붙이는 곳
///   항상 켜져 있는 오브젝트(예: 캔버스 루트)에 붙인다.
///   panel은 이 스크립트가 붙은 오브젝트의 "자식"이어야 한다 (panel을 끄면 스크립트도 꺼지므로).
///
/// 방 밖(로비)에서는 ESC가 동작하지 않는다.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    /// <summary>메뉴가 열려 있는가. 다른 입력 스크립트(PressTarget 등)가 읽는다</summary>
    public static bool IsOpen { get; private set; }

    [Header("UI")]
    public GameObject panel;
    public Button resumeButton;     // 없으면 비워두기
    public Button exitButton;

    [Header("키")]
    public KeyCode toggleKey = KeyCode.Escape;

    [Header("나가기")]
    [Tooltip("RelayManager.OnRoomQuited로 전달될 문구")]
    public string exitReason = "방을 나갔습니다.";


    void OnEnable()
    {
        if (resumeButton != null) resumeButton.onClick.AddListener(Close);
        if (exitButton != null) exitButton.onClick.AddListener(OnExit);

        IsOpen = false;
        if (panel != null) panel.SetActive(false);
    }

    void OnDisable()
    {
        if (resumeButton != null) resumeButton.onClick.RemoveListener(Close);
        if (exitButton != null) exitButton.onClick.RemoveListener(OnExit);

        if (IsOpen) Close();
    }

    void Update()
    {
        bool inRoom = RelayManager.Instance != null && RelayManager.Instance.isInRoom;

        // 방 밖이면 열려 있던 메뉴도 닫는다 (호스트가 끊어졌을 때 등)
        if (!inRoom)
        {
            if (IsOpen) ForceClose();
            return;
        }

        if (Input.GetKeyDown(toggleKey))
        {
            if (IsOpen) Close();
            else Open();
        }
    }


    // ────────────────────────── 열기 / 닫기 ──────────────────────────

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;

        if (panel != null) panel.SetActive(true);
        HeadLookSync.SetCursorFree(true);
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;

        if (panel != null) panel.SetActive(false);
        HeadLookSync.SetCursorFree(false);
    }

    /// <summary>방을 나간 상황: 커서를 다시 잠그면 안 된다 (로비 UI를 눌러야 함)</summary>
    void ForceClose()
    {
        IsOpen = false;

        if (panel != null) panel.SetActive(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }


    // ────────────────────────── 나가기 ──────────────────────────

    void OnExit()
    {
        if (RelayManager.Instance == null) return;

        ForceClose();
        RelayManager.Instance.LeaveRoom(exitReason);
    }
}
