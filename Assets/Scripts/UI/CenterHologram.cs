using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 가운데 공용 홀로그램. 두 플레이어가 같이 보는 정보만 띄운다.
///   - 사고 확률
///   - 시스템 메시지
///   - 승패
///
/// 양쪽에서 똑같이 읽히게 하려면:
///   홀로그램 안에 같은 텍스트를 두 개 두고 서로 반대 방향을 보게 배치한 뒤
///   (앞면용 / 뒷면용, Y 회전 180도 차이) 아래 배열에 둘 다 넣는다.
///   그러면 한 번의 갱신으로 양면이 같은 내용이 된다.
///
/// 연출 종료 신호(EndPresentation)는 이 스크립트에서만 보낸다.
/// 여러 곳에서 보내면 중복된다.
/// </summary>
public class CenterHologram : ClientUIBase
{
    [Header("확률 (앞면 / 뒷면)")]
    public TextMeshPro[] perTexts;

    [Header("시스템 메시지 (앞면 / 뒷면)")]
    public TextMeshPro[] messageTexts;

    [Header("라운드 (앞면 / 뒷면) — 안 쓰면 비워두기")]
    public TextMeshPro[] roundTexts;

    [Header("색")]
    public Color normalColor = new Color(0.6f, 0.9f, 1f);
    public Color warnColor = new Color(1f, 0.4f, 0.3f);

    [Header("연출 시간 (초)")]
    public float roundStartTime = 2f;
    public float openTime = 2f;
    public float resultTime = 2.5f;

    Coroutine presentation;


    // ────────────────────────── 구독 ──────────────────────────

    protected override void Subscribe()
    {
        client.OnChangedPhase += HandlePhase;
        client.OnRoundChanged += HandleRound;
        client.OnPerChanged += HandlePer;
        client.OnChoiceRevealed += HandleChoiceRevealed;
        client.OnRoundDecided += HandleRoundDecided;
        client.OnOuted += HandleOuted;
        client.OnGameWin += HandleGameWin;
        client.OnOpponentPresentChanged += HandleOpponentPresent;
    }

    protected override void Unsubscribe()
    {
        client.OnChangedPhase -= HandlePhase;
        client.OnRoundChanged -= HandleRound;
        client.OnPerChanged -= HandlePer;
        client.OnChoiceRevealed -= HandleChoiceRevealed;
        client.OnRoundDecided -= HandleRoundDecided;
        client.OnOuted -= HandleOuted;
        client.OnGameWin -= HandleGameWin;
        client.OnOpponentPresentChanged -= HandleOpponentPresent;
    }

    protected override void Refresh()
    {
        HandlePhase(client.curPhase);
    }


    // ────────────────────────── 페이즈 ──────────────────────────

    void HandlePhase(GamePhase phase)
    {
        SetActiveTexts(perTexts, phase != GamePhase.waiting);
        SetActiveTexts(roundTexts, phase != GamePhase.waiting && phase != GamePhase.GameOver);

        switch (phase)
        {
            case GamePhase.waiting:
                ShowWaitingMessage();
                break;

            case GamePhase.RoundStart:
                SetMessage("SURVIVAL PRIORITY CALCULATION", normalColor);
                StartPresentation(roundStartTime);
                break;

            case GamePhase.talkTime:
                SetMessage("", normalColor);
                break;

            case GamePhase.Open:
                StartPresentation(openTime);
                break;

            case GamePhase.Press:
                SetMessage("AWAITING INPUT", warnColor);
                break;

            case GamePhase.Result:
                StartPresentation(resultTime);
                break;

            case GamePhase.GameOver:
                break;      // 메시지는 OnGameWin에서 설정됨
        }
    }

    void ShowWaitingMessage()
    {
        bool present = (client != null && client.opponentPresent);
        SetMessage(present ? "PRESS F4 TO READY" : "WAITING FOR CREW", normalColor);
    }

    void HandleOpponentPresent(bool present)
    {
        if (client != null && client.curPhase == GamePhase.waiting)
            ShowWaitingMessage();
    }


    // ────────────────────────── 값 ──────────────────────────

    void HandleRound(int round) => SetTexts(roundTexts, $"ROUND {round}");

    void HandlePer(int before, int now)
    {
        SetTexts(perTexts, $"{now}%");

        Color c = (now > before) ? warnColor : normalColor;
        foreach (var t in perTexts)
            if (t != null) t.color = c;
    }

    void HandleChoiceRevealed(Choice mine, Choice opponent)
    {
        if (mine == Choice.Overload || opponent == Choice.Overload)
            SetMessage("SYSTEM OVERLOAD", warnColor);
    }

    void HandleRoundDecided(Result result, bool isMyTurn)
    {
        if (result == Result.draw)
            SetMessage("SURVIVAL PRIORITY EQUAL", normalColor);
        else if (result == Result.fold)
            SetMessage("ROUND ABORTED", normalColor);
        // 누를 사람이 정해진 경우는 OVERLOAD 메시지를 유지하거나
        // Press 페이즈의 AWAITING INPUT으로 넘어간다
    }

    void HandleOuted(bool outed)
    {
        SetMessage(outed ? "EJECTION SYSTEM ACTIVATED" : "PRESSURE LOCK HELD",
                   outed ? warnColor : normalColor);
    }

    void HandleGameWin(bool win)
    {
        SetMessage(win ? "SURVIVOR CONFIRMED" : "CREW MEMBER EJECTED",
                   win ? normalColor : warnColor);
    }


    // ────────────────────────── 연출 ──────────────────────────

    void StartPresentation(float seconds)
    {
        if (presentation != null) StopCoroutine(presentation);
        presentation = StartCoroutine(PresentationRoutine(seconds));
    }

    IEnumerator PresentationRoutine(float seconds)
    {
        // 여기에 실제 연출을 넣는다 (깜빡임, 사운드, 숫자 스크램블 등)
        yield return new WaitForSeconds(seconds);

        presentation = null;
        if (client != null) client.EndPresentation();
    }


    // ────────────────────────── 헬퍼 ──────────────────────────

    void SetMessage(string s, Color c)
    {
        SetTexts(messageTexts, s);
        foreach (var t in messageTexts)
            if (t != null) t.color = c;
    }

    static void SetTexts(TextMeshPro[] arr, string s)
    {
        if (arr == null) return;
        foreach (var t in arr)
            if (t != null) t.text = s;
    }

    static void SetActiveTexts(TextMeshPro[] arr, bool on)
    {
        if (arr == null) return;
        foreach (var t in arr)
            if (t != null) t.gameObject.SetActive(on);
    }
}
