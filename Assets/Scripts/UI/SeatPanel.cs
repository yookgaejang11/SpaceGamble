using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 좌석별 콘솔 패널. 좌석마다 하나씩 붙이고 seatIndex를 인스펙터에서 지정한다.
///
///   내 자리 패널   → 내 정보 (타이머, Fold 잔여, 내 선택) + 선택 버튼
///   상대 자리 패널 → 공개해도 되는 것만
///                    talkTime 중엔 "SELECTING" — 고민 중인 건 보이지만 내용은 모름
///                    Open 이후엔 상대 선택
///
/// 두 패널이 같은 스크립트를 쓰고, IsMine으로 갈린다.
/// 버튼(GO / FOLD / OVERLOAD)은 내 자리 패널에서만 켜지고 눌린다.
/// Press(누르기)는 3D 오브젝트 클릭 방식이라 PressTarget.cs가 맡는다.
/// </summary>
public class SeatPanel : ClientUIBase
{
    [Header("이 패널이 누구 자리인지 (0 또는 1)")]
    public int seatIndex;

    // ===== [추가] 선택 UI / 디버그 =====
    [Header("[추가] 선택 UI 묶음 (내 자리 + talkTime에서만 켜짐)")]
    [Tooltip("이 스크립트가 붙은 오브젝트 자신/부모를 넣으면 안 된다. 형제나 다른 오브젝트를 넣을 것")]
    public GameObject selectUI;

    [Header("[추가] 디버그")]
    [Tooltip("체크하면 단계가 바뀔 때마다 콘솔에 상태를 찍는다")]
    public bool debugLog = true;
    // ===== [추가] 끝 =====

    [Header("텍스트")]
    public TextMeshPro timerText;       // 내 자리에서만 사용
    public TextMeshPro foldLeftText;    // 내 자리에서만 사용
    public TextMeshPro choiceText;      // 양쪽 모두 사용

    [Header("내 선택 버튼 (내 자리에서만 / 안 쓰면 비워두기)")]
    public Button goButton;
    public Button foldButton;
    public Button overloadButton;

    [Header("선택 강조 (없으면 비워두기)")]
    public Image goHighlight;
    public Image foldHighlight;
    public Image overloadHighlight;

    [Header("색")]
    public Color normalColor = new Color(0.7f, 0.9f, 1f);
    public Color warnColor = new Color(1f, 0.5f, 0.3f);
    public Color dimColor = new Color(0.45f, 0.5f, 0.55f);

    [Header("상대 패널 문구")]
    public string selectingMark = "SELECTING";
    public string emptyMark = "OFFLINE";


    bool IsMine => client != null && client.myIndex.Value == seatIndex;


    // ────────────────────────── 버튼 연결 ──────────────────────────

    protected override void OnEnable()
    {
        base.OnEnable();

        if (goButton != null) goButton.onClick.AddListener(OnGo);
        if (foldButton != null) foldButton.onClick.AddListener(OnFold);
        if (overloadButton != null) overloadButton.onClick.AddListener(OnOverload);
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        if (goButton != null) goButton.onClick.RemoveListener(OnGo);
        if (foldButton != null) foldButton.onClick.RemoveListener(OnFold);
        if (overloadButton != null) overloadButton.onClick.RemoveListener(OnOverload);
    }

    // ===== [추가] 방을 나갈 때 =====

    /// <summary>방을 나가서 클라이언트가 사라졌을 때: 선택 UI를 끈다</summary>
    protected override void OnClientLost()
    {
        if (selectUI != null) selectUI.SetActive(false);
    }

    // 선택 클릭은 버튼마다 붙인 ChoiceTarget.cs가 조준점 레이캐스트로 처리한다
    // (커서가 잠겨 있어서 UI Button의 onClick은 1인칭 중에는 불리지 않는다)
    // ===== [추가] 끝 =====

    // 내 패널이 아니면 눌려도 아무 일도 하지 않는다 (안전장치)
    void OnGo() { if (IsMine) client.RequestGo(); }
    void OnFold() { if (IsMine) client.RequestFold(); }
    void OnOverload() { if (IsMine) client.RequestOverload(); }


    // ────────────────────────── 구독 ──────────────────────────

    protected override void Subscribe()
    {
        client.OnChangedPhase += HandlePhase;
        client.OnTimeChanged += HandleTime;
        client.OnMyFoldLeftChanged += HandleFoldLeft;
        client.OnMyChoiceChanged += HandleMyChoice;
        client.OnChoiceRevealed += HandleChoiceRevealed;
        client.OnOpponentPresentChanged += HandleOpponentPresent;
    }

    protected override void Unsubscribe()
    {
        client.OnChangedPhase -= HandlePhase;
        client.OnTimeChanged -= HandleTime;
        client.OnMyFoldLeftChanged -= HandleFoldLeft;
        client.OnMyChoiceChanged -= HandleMyChoice;
        client.OnChoiceRevealed -= HandleChoiceRevealed;
        client.OnOpponentPresentChanged -= HandleOpponentPresent;
    }

    protected override void Refresh()
    {
        HandlePhase(client.curPhase);
        UpdateHighlight(client.myChoice);
    }


    // ────────────────────────── 페이즈 ──────────────────────────

    void HandlePhase(GamePhase phase)
    {
        bool inGame = (phase != GamePhase.waiting && phase != GamePhase.GameOver);
        bool canChoose = IsMine && phase == GamePhase.talkTime;

        // ===== [추가] 선택 UI 묶음: 내 자리 + talkTime에서만 켠다 =====
        if (selectUI != null) selectUI.SetActive(canChoose);

        if (debugLog)
            Debug.Log($"[SeatPanel {seatIndex}] phase={phase} myIndex={client.myIndex.Value} " +
                      $"IsMine={IsMine} canChoose={canChoose} selectUI={(selectUI != null ? selectUI.name : "NULL")}");
        // ===== [추가] 끝 =====

        // 타이머와 Fold 잔여는 내 자리에만, 게임 중에만
        if (timerText != null)
            timerText.gameObject.SetActive(canChoose);

        if (foldLeftText != null)
        {
            foldLeftText.gameObject.SetActive(IsMine && inGame);
            if (IsMine && inGame) SetFoldLeft(client.foldLeft);
        }

        // 버튼: 내 자리 + talkTime에만
        SetButton(goButton, canChoose);
        SetButton(overloadButton, canChoose);
        SetButton(foldButton, canChoose);
        if (foldButton != null && canChoose)
            foldButton.interactable = (client.foldLeft > 0);

        if (choiceText == null) return;

        if (!inGame)
        {
            // 대기 화면
            if (IsMine) SetChoice("", dimColor);
            else SetChoice(client.opponentPresent ? "" : emptyMark, dimColor);
            return;
        }

        switch (phase)
        {
            case GamePhase.RoundStart:
                SetChoice("", dimColor);
                break;

            case GamePhase.talkTime:
                // 내 패널엔 내 선택, 상대 패널엔 "고민 중"만
                if (IsMine) ShowChoice(client.myChoice);
                else SetChoice(selectingMark, dimColor);
                break;

                // Open 이후는 OnChoiceRevealed가 채운다
        }
    }


    // ────────────────────────── 값 ──────────────────────────

    void HandleTime(float remain)
    {
        if (!IsMine || timerText == null) return;
        timerText.text = Mathf.CeilToInt(remain).ToString() + "s";
        timerText.color = (remain <= 5f) ? warnColor : normalColor;
    }

    void HandleFoldLeft(int left)
    {
        if (!IsMine) return;
        SetFoldLeft(left);

        // Fold 버튼도 같이 갱신 (talkTime 중일 때만)
        if (foldButton != null && client.curPhase == GamePhase.talkTime)
            foldButton.interactable = (left > 0);
    }

    void SetFoldLeft(int left)
    {
        if (foldLeftText == null) return;
        foldLeftText.text = $"FOLD {left}";
        foldLeftText.color = (left > 0) ? normalColor : dimColor;
    }

    void HandleMyChoice(Choice c)
    {
        if (!IsMine) return;

        UpdateHighlight(c);

        if (client.curPhase != GamePhase.talkTime) return;
        ShowChoice(c);
    }

    void HandleChoiceRevealed(Choice mine, Choice opponent)
    {
        ShowChoice(IsMine ? mine : opponent);
    }

    void HandleOpponentPresent(bool present)
    {
        if (IsMine) return;
        if (client.curPhase == GamePhase.waiting || client.curPhase == GamePhase.GameOver)
            SetChoice(present ? "" : emptyMark, dimColor);
    }


    // ────────────────────────── 헬퍼 ──────────────────────────

    void ShowChoice(Choice c)
    {
        switch (c)
        {
            case Choice.Go: SetChoice("GO", normalColor); break;
            case Choice.Fold: SetChoice("FOLD", dimColor); break;
            case Choice.Overload: SetChoice("OVERLOAD", warnColor); break;
        }
    }

    void SetChoice(string s, Color c)
    {
        if (choiceText == null) return;
        choiceText.text = "Choice:" + s;
        choiceText.color = c;
    }

    void UpdateHighlight(Choice c)
    {
        if (!IsMine) return;
        if (goHighlight != null) goHighlight.enabled = (c == Choice.Go);
        if (foldHighlight != null) foldHighlight.enabled = (c == Choice.Fold);
        if (overloadHighlight != null) overloadHighlight.enabled = (c == Choice.Overload);
    }

    static void SetButton(Button b, bool on)
    {
        if (b == null) return;
        b.gameObject.SetActive(on);
        if (on) b.interactable = true;
    }
}