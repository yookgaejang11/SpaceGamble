using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 고정 HUD. 캔버스(Screen Space - Overlay)에 붙인다.
///
/// 월드 표시면(홀로그램, 보드, 패널)으로 못 가는 것만 여기에 둔다.
///   - 방 코드 (정확히 읽고 복사해야 하므로)
///   - 버튼 (GO / FOLD / OVERLOAD / PRESS)
///
/// 버튼 대신 키 입력을 쓸 거면 버튼 부분은 지워도 된다.
/// </summary>
public class GameHud : ClientUIBase
{
    [Header("방 코드")]
    public GameObject roomCodeGroup;
    public TextMeshProUGUI roomCodeText;
    public Button copyButton;

    [Header("버튼")]
    public Button goButton;
    public Button foldButton;
    public Button overloadButton;
    public Button pressButton;

    [Header("선택 강조 (없으면 비워두기)")]
    public Image goHighlight;
    public Image foldHighlight;
    public Image overloadHighlight;


    protected override void OnEnable()
    {
        base.OnEnable();

        if (goButton != null) goButton.onClick.AddListener(OnGo);
        if (foldButton != null) foldButton.onClick.AddListener(OnFold);
        if (overloadButton != null) overloadButton.onClick.AddListener(OnOverload);
        if (pressButton != null) pressButton.onClick.AddListener(OnPress);
        if (copyButton != null) copyButton.onClick.AddListener(CopyCode);

        if (RelayManager.Instance != null)
            RelayManager.Instance.OnRoomCodeSpawned += HandleRoomCode;

        ShowRoomCode();
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        if (goButton != null) goButton.onClick.RemoveListener(OnGo);
        if (foldButton != null) foldButton.onClick.RemoveListener(OnFold);
        if (overloadButton != null) overloadButton.onClick.RemoveListener(OnOverload);
        if (pressButton != null) pressButton.onClick.RemoveListener(OnPress);
        if (copyButton != null) copyButton.onClick.RemoveListener(CopyCode);

        if (RelayManager.Instance != null)
            RelayManager.Instance.OnRoomCodeSpawned -= HandleRoomCode;
    }

    protected override void Subscribe()
    {
        client.OnChangedPhase += HandlePhase;
        client.OnMyFoldLeftChanged += HandleFoldLeft;
        client.OnMyChoiceChanged += HandleMyChoice;
        client.OnRoundDecided += HandleRoundDecided;
        client.OnOuted += HandleOuted;
    }

    protected override void Unsubscribe()
    {
        client.OnChangedPhase -= HandlePhase;
        client.OnMyFoldLeftChanged -= HandleFoldLeft;
        client.OnMyChoiceChanged -= HandleMyChoice;
        client.OnRoundDecided -= HandleRoundDecided;
        client.OnOuted -= HandleOuted;
    }

    protected override void Refresh()
    {
        HandlePhase(client.curPhase);
        HandleMyChoice(client.myChoice);
    }


    // ────────────────────────── 입력 ──────────────────────────

    void OnGo() { if (client != null) client.RequestGo(); }
    void OnFold() { if (client != null) client.RequestFold(); }
    void OnOverload() { if (client != null) client.RequestOverload(); }
    void OnPress() { if (client != null) client.RequestPress(); }


    // ────────────────────────── 방 코드 ──────────────────────────

    void HandleRoomCode(string code) => ShowRoomCode();

    void ShowRoomCode()
    {
        string code = (RelayManager.Instance != null) ? RelayManager.Instance.joinCode : null;
        bool has = !string.IsNullOrEmpty(code);

        if (roomCodeGroup != null) roomCodeGroup.SetActive(has);
        if (roomCodeText != null && has) roomCodeText.text = code;
    }

    void CopyCode()
    {
        string code = (RelayManager.Instance != null) ? RelayManager.Instance.joinCode : null;
        if (!string.IsNullOrEmpty(code))
            GUIUtility.systemCopyBuffer = code;
    }


    // ────────────────────────── 상태 ──────────────────────────

    void HandlePhase(GamePhase phase)
    {
        bool canChoose = (phase == GamePhase.talkTime);

        SetActive(goButton, canChoose);
        SetActive(overloadButton, canChoose);
        SetActive(foldButton, canChoose);

        if (foldButton != null && canChoose)
            foldButton.interactable = (client != null && client.foldLeft > 0);

        SetActive(pressButton, false);      // Press 지목은 OnRoundDecided에서
    }

    void HandleFoldLeft(int left)
    {
        if (foldButton == null) return;
        if (client != null && client.curPhase == GamePhase.talkTime)
            foldButton.interactable = (left > 0);
    }

    void HandleMyChoice(Choice c)
    {
        if (goHighlight != null) goHighlight.enabled = (c == Choice.Go);
        if (foldHighlight != null) foldHighlight.enabled = (c == Choice.Fold);
        if (overloadHighlight != null) overloadHighlight.enabled = (c == Choice.Overload);
    }

    void HandleRoundDecided(Result result, bool isMyTurn)
    {
        bool pressed = (result != Result.draw && result != Result.fold);
        SetActive(pressButton, pressed && isMyTurn);
    }

    void HandleOuted(bool outed)
    {
        SetActive(pressButton, false);
    }


    static void SetActive(Button b, bool on)
    {
        if (b == null) return;
        b.gameObject.SetActive(on);
        if (on) b.interactable = true;
    }
}
