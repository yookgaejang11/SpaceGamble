using UnityEngine;
using UnityEngine.Android;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
    /// <summary>메뉴가 열려 있는가. 다른 입력 스크립트(PressTarget 등)가 읽는다</summary>
    public static bool IsOpen { get; private set; }

    [Header("UI")]
    public GameObject panel;
    public Button resumeButton;     // 없으면 비워두기
    public Button exitButton;
    public GameObject AskUI;

    [Header("키")]
    public KeyCode toggleKey = KeyCode.Escape;

    [Header("나가기")]
    [Tooltip("RelayManager.OnRoomQuited로 전달될 문구")]
    public string exitReason = "";
    


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
        if (AskUI != null) AskUI.SetActive(false);
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
        if (AskUI != null) AskUI.SetActive(false);
        ForceClose();
        RelayManager.Instance.LeaveRoom(string.IsNullOrEmpty(exitReason) ? null : exitReason);
    }
}
