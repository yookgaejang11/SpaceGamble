using TMPro;
using UnityEngine;

/// <summary>
/// 좌석별 콘솔 패널. 좌석마다 하나씩 붙이고 seatIndex를 인스펙터에서 지정한다.
///
///   내 자리 패널   → 내 정보 (타이머, Fold 잔여, 내 선택)
///   상대 자리 패널 → 공개해도 되는 것만
///                    talkTime 중엔 "SELECTING" — 고민 중인 건 보이지만 내용은 모름
///                    Open 이후엔 상대 선택
///
/// 두 패널이 같은 스크립트를 쓰고, IsMine으로 갈린다.
/// </summary>
public class SeatPanel : ClientUIBase
{
    [Header("이 패널이 누구 자리인지 (0 또는 1)")]
    public int seatIndex;

    [Header("텍스트")]
    public TextMeshPro timerText;       // 내 자리에서만 사용
    public TextMeshPro foldLeftText;    // 내 자리에서만 사용
    public TextMeshPro choiceText;      // 양쪽 모두 사용

    [Header("색")]
    public Color normalColor = new Color(0.7f, 0.9f, 1f);
    public Color warnColor = new Color(1f, 0.5f, 0.3f);
    public Color dimColor = new Color(0.45f, 0.5f, 0.55f);

    [Header("상대 패널 문구")]
    public string selectingMark = "SELECTING";
    public string emptyMark = "OFFLINE";


    bool IsMine => client != null && client.myIndex.Value == seatIndex;


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
    }


    // ────────────────────────── 페이즈 ──────────────────────────

    void HandlePhase(GamePhase phase)
    {
        bool inGame = (phase != GamePhase.waiting && phase != GamePhase.GameOver);

        // 타이머와 Fold 잔여는 내 자리에만, 게임 중에만
        if (timerText != null)
            timerText.gameObject.SetActive(IsMine && phase == GamePhase.talkTime);

        if (foldLeftText != null)
        {
            foldLeftText.gameObject.SetActive(IsMine && inGame);
            if (IsMine && inGame) SetFoldLeft(client.foldLeft);
        }

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
        timerText.text = Mathf.CeilToInt(remain).ToString();
        timerText.color = (remain <= 5f) ? warnColor : normalColor;
    }

    void HandleFoldLeft(int left)
    {
        if (!IsMine) return;
        SetFoldLeft(left);
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
        choiceText.text = s;
        choiceText.color = c;
    }
}
