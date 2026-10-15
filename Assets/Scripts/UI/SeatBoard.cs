using System.Collections;     // [추가] 슬롯머신 코루틴
using DG.Tweening;            // [추가] 멈출 때 튕기는 효과 (DOTween)
using TMPro;
using UnityEngine;

/// <summary>
/// Player1 / Player2 보드.
///
/// 양쪽 보드가 같은 내용을 보여주므로 스크립트 하나로 둘 다 처리한다.
/// 배열에 보드 두 개의 텍스트를 각각 넣으면 된다.
///   p1Texts = [위쪽 보드의 Player1 칸, 아래쪽 보드의 Player1 칸]
///   p2Texts = [위쪽 보드의 Player2 칸, 아래쪽 보드의 Player2 칸]
///
/// 내 칸은 서버가 값을 보낸 적이 없다. "??"는 하드코딩이다.
/// (숨겨야 하는 정보는 애초에 클라이언트로 오지 않는다)
///
/// ===== [추가] 슬롯머신 =====
/// 상대 카드는 받자마자 보여주지 않고 저장만 한다 (명세서: 값은 연출 안에서 공개).
/// RoundStart 연출 중에 CenterHologram이 PlayRoll()을 부르면
///   두 칸 다 숫자가 빠르게 돌다가 → 점점 느려지고 →
///   내 칸이 먼저 "??"에서 딱 멈추고 → 마지막에 상대 칸이 뽑은 숫자로 딱 멈춘다.
/// 연출 종료 신호(EndPresentation)는 보내지 않는다 (CenterHologram만 보낸다).
/// CenterHologram이 연결 안 돼 있어도 talkTime이 되면 숫자는 그냥 나온다 (안전장치).
/// ===== [추가] 끝 =====
/// </summary>
public class SeatBoard : ClientUIBase
{
    [Header("Player1 칸 (보드마다 하나씩)")]
    public TextMeshPro[] p1Texts;

    [Header("Player2 칸 (보드마다 하나씩)")]
    public TextMeshPro[] p2Texts;

    [Header("표시 문자열")]
    public string hiddenMark = "??";
    public string emptyMark = "---";
    public string readyMark = "READY";
    public string notReadyMark = "...";

    [Header("색")]
    public Color normalColor = Color.white;
    public Color readyColor = new Color(0.5f, 1f, 0.6f);
    public Color dimColor = new Color(0.5f, 0.5f, 0.5f);

    // ===== [추가] 슬롯머신 설정 =====
    [Header("[추가] 슬롯머신")]
    [Tooltip("전체 도는 시간(초). 상대 칸이 이 시간에 멈춘다")]
    public float rollTime = 1.4f;
    [Tooltip("내 칸(??)이 멈추는 시점 (전체 시간 대비 비율). 0.7이면 70% 지점에 먼저 멈춤")]
    [Range(0.1f, 1f)] public float myStopRatio = 0.7f;
    [Tooltip("처음 숫자 바뀌는 간격(초) — 작을수록 빠르게 돈다")]
    public float fastInterval = 0.04f;
    [Tooltip("멈추기 직전 숫자 바뀌는 간격(초) — 클수록 덜컥덜컥 느려진다")]
    public float slowInterval = 0.2f;
    [Tooltip("돌아가는 중 숫자 색")]
    public Color rollingColor = new Color(0.6f, 0.75f, 0.85f);
    [Tooltip("상대 숫자가 확정될 때 색 (강조)")]
    public Color revealColor = new Color(1f, 0.85f, 0.3f);
    [Tooltip("멈출 때 '딱' 튕기는 크기. 0이면 안 튕김")]
    public float landPunch = 0.3f;

    [Header("[추가] 사운드 — 없으면 비워두기")]
    public AudioSource sfx;
    public AudioClip tickClip;      // 숫자 바뀔 때 '틱'
    public AudioClip lockClip;      // 내 칸 멈출 때 '철컥'
    public AudioClip landClip;      // 상대 칸 확정 '딱'
    // ===== [추가] 끝 =====

    int shownCard = 0;      // 마지막으로 받은 상대 카드
    // [추가]
    int pendingCard = 0;            // 받았지만 아직 공개 안 한 상대 카드
    Coroutine rollRoutine;

    /// <summary>[추가] 지금 슬롯머신이 도는 중인가 (CenterHologram이 기다릴 때 읽는다)</summary>
    public bool IsRolling => rollRoutine != null;


    protected override void Subscribe()
    {
        client.OnChangedPhase += HandlePhase;
        client.OnNumberSelected += HandleOpponentCard;
        client.OnReadyChanged += HandleReady;
        client.OnOpponentPresentChanged += HandleOpponentPresent;
    }

    protected override void Unsubscribe()
    {
        client.OnChangedPhase -= HandlePhase;
        client.OnNumberSelected -= HandleOpponentCard;
        client.OnReadyChanged -= HandleReady;
        client.OnOpponentPresentChanged -= HandleOpponentPresent;
    }

    protected override void Refresh()
    {
        HandlePhase(client.curPhase);
    }

    // ===== [추가] 방 나감 / 꺼짐 정리 =====
    protected override void OnClientLost() => StopRoll();

    protected override void OnDisable()
    {
        base.OnDisable();
        StopRoll();
    }
    // ===== [추가] 끝 =====


    /// <summary>내가 Player1 칸인가</summary>
    bool IAmP1 => client != null && client.myIndex.Value == 0;


    void HandlePhase(GamePhase phase)
    {
        // [추가] RoundStart가 아닌데 아직 돌고 있으면 끊는다 (연출 시간 초과 등)
        if (phase != GamePhase.RoundStart) StopRoll();

        if (phase == GamePhase.waiting || phase == GamePhase.GameOver)
            DrawReady(client.isReady, client.opponentReady, client.opponentPresent);
        // ===== [추가] RoundStart: 슬롯머신이 돌기 전까지는 두 칸 다 ?? =====
        else if (phase == GamePhase.RoundStart)
        {
            if (!IsRolling)
                Set(hiddenMark, hiddenMark, dimColor, dimColor);
        }
        // ===== [추가] 끝 =====
        else
        {
            // [추가] 안전장치: 슬롯머신이 안 돌았어도(CenterHologram 미연결 등) 이때부턴 숫자를 보여준다
            shownCard = pendingCard;
            DrawCards(shownCard);
        }
    }

    void HandleOpponentCard(int card)
    {
        // [원래] shownCard = card;
        // [원래] DrawCards(card);
        pendingCard = card;         // [변경] 저장만. 공개는 슬롯머신(PlayRoll)이 한다
    }

    void HandleReady(bool mine, bool opponent)
    {
        if (client.curPhase == GamePhase.waiting || client.curPhase == GamePhase.GameOver)
            DrawReady(mine, opponent, client.opponentPresent);
    }

    void HandleOpponentPresent(bool present)
    {
        if (client.curPhase == GamePhase.waiting || client.curPhase == GamePhase.GameOver)
            DrawReady(client.isReady, client.opponentReady, present);
    }


    // ===== [추가] 슬롯머신 =====

    /// <summary>CenterHologram의 RoundStart 연출에서 부른다</summary>
    public void PlayRoll(int card)
    {
        pendingCard = card;
        StopRoll();
        if (!isActiveAndEnabled) { shownCard = card; DrawCards(card); return; }
        rollRoutine = StartCoroutine(RollRoutine(card));
    }

    IEnumerator RollRoutine(int card)
    {
        float myStopAt = rollTime * myStopRatio;
        bool myLocked = false;
        float t = 0f;
        int lastMine = 0, lastTheirs = 0;

        while (t < rollTime)
        {
            // 진행도에 따라 간격이 점점 길어진다 = 점점 느려지는 슬롯머신
            float k = t / rollTime;
            float interval = Mathf.Lerp(fastInterval, slowInterval, k * k);

            // 내 칸: 멈출 시점이 되면 ?? 로 철컥
            if (!myLocked && t >= myStopAt)
            {
                myLocked = true;
                SetMine(hiddenMark, normalColor);
                Punch(MyTexts);
                Play(lockClip);
            }

            // 아직 도는 칸들에 랜덤 숫자 (직전 숫자와 겹치지 않게 해서 '바뀌는 느낌'을 살린다)
            if (!myLocked) { lastMine = RandomExcept(lastMine); SetMine(lastMine.ToString(), rollingColor); }
            lastTheirs = RandomExcept(lastTheirs);
            SetTheirs(lastTheirs.ToString(), rollingColor);
            Play(tickClip);

            yield return new WaitForSeconds(interval);
            t += interval;
        }

        // 혹시 내 칸이 아직이면 (myStopRatio = 1 등) 여기서 잠근다
        if (!myLocked) SetMine(hiddenMark, normalColor);

        // 상대 칸 확정: 강조색으로 딱 → 잠깐 뒤 기본색
        shownCard = card;
        SetTheirs(card.ToString(), revealColor);
        Punch(TheirTexts);
        Play(landClip);

        yield return new WaitForSeconds(0.3f);
        DrawCards(card);

        rollRoutine = null;
    }

    /// <summary>도는 걸 멈추고 최종 값으로 맞춘다</summary>
    void StopRoll()
    {
        if (rollRoutine == null) return;
        StopCoroutine(rollRoutine);
        rollRoutine = null;
        shownCard = pendingCard;
    }

    // 내 칸 / 상대 칸 배열 (내가 P1이면 p1Texts가 내 칸)
    TextMeshPro[] MyTexts => IAmP1 ? p1Texts : p2Texts;
    TextMeshPro[] TheirTexts => IAmP1 ? p2Texts : p1Texts;

    void SetMine(string s, Color c) => Apply(MyTexts, s, c);
    void SetTheirs(string s, Color c) => Apply(TheirTexts, s, c);

    static int RandomExcept(int prev)
    {
        if (prev < 1 || prev > 10) return Random.Range(1, 11);   // 처음(직전 숫자 없음)엔 1~10 아무거나
        int n = Random.Range(1, 10);            // 1~9 중 하나 뽑고
        return (n >= prev) ? n + 1 : n;         // prev 이상이면 +1 → 1~10 중 prev만 빼고 고른 셈
    }

    void Punch(TextMeshPro[] arr)
    {
        if (arr == null || landPunch <= 0f) return;
        foreach (var t in arr)
        {
            if (t == null) continue;
            t.transform.DOKill(true);           // 이전 튕김이 남아 있으면 끝내고 원래 크기로
            t.transform.DOPunchScale(Vector3.one * landPunch, 0.25f, 6, 0.5f);
        }
    }

    void Play(AudioClip clip)
    {
        if (sfx != null && clip != null) sfx.PlayOneShot(clip);
    }
    // ===== [추가] 끝 =====


    // ────────────────────────── 그리기 ──────────────────────────

    void DrawCards(int opponentCard)
    {
        string mine = hiddenMark;
        string theirs = opponentCard.ToString();

        Set(IAmP1 ? mine : theirs, IAmP1 ? theirs : mine,
            normalColor, normalColor);
    }

    void DrawReady(bool myReady, bool oppReady, bool oppPresent)
    {
        string mine = myReady ? readyMark : notReadyMark;
        Color mineColor = myReady ? readyColor : dimColor;

        string theirs = !oppPresent ? emptyMark : (oppReady ? readyMark : notReadyMark);
        Color theirsColor = !oppPresent ? dimColor : (oppReady ? readyColor : dimColor);

        Set(IAmP1 ? mine : theirs,
            IAmP1 ? theirs : mine,
            IAmP1 ? mineColor : theirsColor,
            IAmP1 ? theirsColor : mineColor);
    }

    void Set(string p1, string p2, Color c1, Color c2)
    {
        Apply(p1Texts, p1, c1);
        Apply(p2Texts, p2, c2);
    }

    static void Apply(TextMeshPro[] arr, string s, Color c)
    {
        if (arr == null) return;
        foreach (var t in arr)
        {
            if (t == null) continue;
            t.text = s;
            t.color = c;
        }
    }
}