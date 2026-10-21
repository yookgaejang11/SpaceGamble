using System.Collections;     // 슬롯머신 코루틴
using DG.Tweening;            // 멈출 때 튕기는 효과 (DOTween)
using TMPro;
using UnityEngine;

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

    [Header("슬롯머신")]
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
    [Tooltip("카드 공개 때 낮은 쪽(버튼을 눌러야 하는 쪽) 숫자 색")]
    public Color loseColor = new Color(1f, 0.3f, 0.25f);
    [Tooltip("카드 공개 때 내 칸이 도는 시간(초)")]
    public float revealRollTime = 0.8f;
    [Tooltip("멈출 때 '딱' 튕기는 크기. 0이면 안 튕김")]
    public float landPunch = 0.3f;

    [Header("사운드 id (SoundManager) — 비우면 소리 없음")]
    // 소리는 SoundManager가 관리. SoundManager 'Sounds' 목록의 id만 적는다
    public string tickClip = "slot_tick";   // 숫자 바뀔 때 '틱'
    public string lockClip = "slot_lock";   // 내 칸 멈출 때 '철컥'
    public string landClip = "slot_land";   // 숫자 확정 '딱'

    int shownCard = 0;      // 마지막으로 받은 상대 카드
    int pendingCard = 0;            // 받았지만 아직 공개 안 한 상대 카드
    int? revealedMine;              // 공개된 내 숫자 (null이면 아직 ??)
    int pendingMine = -1;           // 공개 연출 중인 내 숫자
    Coroutine rollRoutine;

    /// <summary>지금 슬롯머신이 도는 중인가 (CenterHologram이 기다릴 때 읽는다)</summary>
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

    protected override void OnClientLost() => StopRoll();

    protected override void OnDisable()
    {
        base.OnDisable();
        StopRoll();
    }


    /// <summary>내가 Player1 칸인가</summary>
    bool IAmP1 => client != null && client.myIndex.Value == 0;


    void HandlePhase(GamePhase phase)
    {
        // RoundStart가 아닌데 아직 돌고 있으면 끊는다 (연출 시간 초과 등)
        if (phase != GamePhase.RoundStart && phase != GamePhase.Open) StopRoll();   // Open엔 카드 공개가 돈다

        // 새 라운드 / 대기면 공개했던 내 숫자를 다시 숨긴다
        if (phase == GamePhase.RoundStart || phase == GamePhase.waiting)
            revealedMine = null;

        if (phase == GamePhase.waiting || phase == GamePhase.GameOver)
            DrawReady(client.isReady, client.opponentReady, client.opponentPresent);
        else if (phase == GamePhase.RoundStart)
        {
            if (!IsRolling)
                Set(hiddenMark, hiddenMark, dimColor, dimColor);
        }
        else
        {
            // 안전장치: 슬롯머신이 안 돌았어도(CenterHologram 미연결 등) 이때부턴 숫자를 보여준다
            shownCard = pendingCard;
            DrawCards(shownCard);
        }
    }

    void HandleOpponentCard(int card)
    {
        pendingCard = card;         // 저장만. 공개는 슬롯머신(PlayRoll)이 한다
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
        if (pendingMine > 0) revealedMine = pendingMine;    // 공개 도중 끊겨도 결과는 맞춰 둔다
    }


    /// <summary>
    /// CenterHologram의 Open 연출에서 부른다.
    /// 내 칸 "??"가 잠깐 돌다가 내 진짜 숫자로 딱 멈추고, 높은 쪽 / 낮은 쪽(누를 사람) 색으로 칠한다.
    /// </summary>
    public void PlayReveal(int myCard, int oppCard)
    {
        StopRoll();
        pendingMine = myCard;
        pendingCard = oppCard;
        shownCard = oppCard;
        if (!isActiveAndEnabled) { revealedMine = myCard; DrawCards(oppCard); return; }
        rollRoutine = StartCoroutine(RevealRoutine(myCard, oppCard));
    }

    IEnumerator RevealRoutine(int myCard, int oppCard)
    {
        // 상대 칸은 그대로 두고 내 칸만 돈다 (슬롯머신과 같은 감속)
        float t = 0f;
        int last = 0;
        while (t < revealRollTime)
        {
            float k = t / revealRollTime;
            float interval = Mathf.Lerp(fastInterval, slowInterval, k * k);
            last = RandomExcept(last);
            SetMine(last.ToString(), rollingColor);
            Play(tickClip);
            yield return new WaitForSeconds(interval);
            t += interval;
        }

        // 딱! 내 숫자 공개
        revealedMine = myCard;
        pendingMine = -1;
        DrawCards(oppCard);
        Punch(MyTexts);
        Play(landClip);

        yield return new WaitForSeconds(0.3f);
        rollRoutine = null;
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

    void Play(string id)                                    // SoundManager로
    {
        if (!string.IsNullOrEmpty(id)) SoundManager.Play(id);
    }


    void DrawCards(int opponentCard)
    {
        // 내 숫자가 공개됐으면 숫자로, 아니면 ??
        string mine = revealedMine.HasValue ? revealedMine.Value.ToString() : hiddenMark;
        string theirs = opponentCard.ToString();

        Color mineColor = normalColor, theirsColor = normalColor;
        if (revealedMine.HasValue)
        {
            // 높은 쪽 = 금색(살아남음), 낮은 쪽 = 빨강(버튼을 누름), 같으면 그대로
            int m = revealedMine.Value;
            if (m > opponentCard) { mineColor = revealColor; theirsColor = loseColor; }
            else if (m < opponentCard) { mineColor = loseColor; theirsColor = revealColor; }
        }

        Set(IAmP1 ? mine : theirs, IAmP1 ? theirs : mine,
            IAmP1 ? mineColor : theirsColor, IAmP1 ? theirsColor : mineColor);
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
