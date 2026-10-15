using System.Collections;
using System.Collections.Generic;   // [추가] 사출 더미 목록
using TMPro;
using UnityEngine;
using UnityEngine.Playables;          // [추가] 게임 끝 타임라인 (PlayableDirector)

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
///
/// ===== [추가] 연출 지휘 =====
/// 이 스크립트가 연출의 "지휘자"다. 명세서(연출 작업 가이드) 규칙을 따른다.
///   1) 이벤트로 온 값(카드, 선택, 결과, 확률)은 저장만 한다
///   2) 페이즈가 RoundStart / Open / Result로 오면 코루틴을 시작하고,
///      코루틴 안에서 글자를 공개하고 레버/버튼/사출 애니메이션 트리거를 당긴다
///   3) 코루틴이 끝나면 EndPresentation()을 한 번만 보낸다
///   애니메이션은 각자 화면에서만 재생한다 (NetworkAnimator 쓰지 않음).
///   두 컴퓨터가 같은 이벤트를 받아 각자 같은 연출을 튼다.
/// ===== [추가] 끝 =====
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

    // [원래] 연출 시간 — 이제 각 연출 코루틴 안의 대기 시간이 기준이라 쓰지 않는다
    //[Header("연출 시간 (초)")]
    //public float roundStartTime = 2f;
    //public float openTime = 2f;
    //public float resultTime = 2.5f;

    // ===== [추가] 연출에 쓰는 오브젝트들 =====
    [Header("[추가] 레버 (0번 자리 / 1번 자리)")]
    [Tooltip("RoundStart에 양쪽 레버가 같이 당겨진다")]
    public Animator[] leverAnimators = new Animator[2];
    public string leverTrigger = "Pull";

    [Header("[추가] 사출 버튼 (0번 자리 / 1번 자리)")]
    [Tooltip("Result에 '누른 사람' 자리의 버튼만 눌림 애니메이션")]
    public Animator[] pressButtonAnimators = new Animator[2];
    public string pressTrigger = "Press";

    [Header("[추가] 우주인 애니메이션 (플레이어 프리팹 안의 Animator — 실행 중 자리 번호로 자동으로 찾음)")]
    [Tooltip("체크하면 RoundStart에 양쪽 우주인이 레버를 당기고, Result에 누른 사람이 버튼을 누른다")]
    public bool animateAstronaut = true;
    [Tooltip("astronaut_1 컨트롤러의 Trigger 이름 (lever_pull로 가는 조건)")]
    public string astronautPullTrigger = "Pull";
    [Tooltip("astronaut_1 컨트롤러의 Trigger 이름 (butten_push로 가는 조건)")]
    public string astronautPressTrigger = "Press";

    [Header("[추가] 손이 닿는 타이밍 맞추기 (초)")]
    [Tooltip("우주인이 lever_pull을 시작하고 몇 초 뒤에 레버를 트리거할지. " +
             "레버가 늦으면 줄이고, 0으로도 늦으면 음수(-0.3 등)로: 레버를 먼저 출발시키고 우주인이 그만큼 늦게 출발한다")]
    // [원래] public float leverDelay = 0f;
    // [변경] 클립 분석값: lever_pull에서 왼손이 레버를 끌어내리는 구간 = 1.1초~1.63초,
    //        lever_action은 0.47초까지 가만히 있다가 움직임 → 1.1 - 0.47 ≈ 0.65초 늦게 트리거
    public float leverDelay = 0.65f;
    [Tooltip("[추가] 우주인 동작 시작 후 몇 초 뒤에 레버 소리를 낼지 (레버가 실제로 움직이기 시작하는 순간 ≈ 1.1초)")]
    public float leverSoundDelay = 1.1f;
    [Tooltip("우주인이 butten_push를 시작하고 몇 초 뒤에 버튼이 눌리기 시작할지 (손가락이 버튼에 닿는 순간)")]
    public float buttonDelay = 0f;

    [Header("[추가] 경고등 (OVERLOAD일 때) — 없으면 비워두기")]
    public Animator alarmAnimator;
    public string alarmTrigger = "Alarm";

    [Header("[추가] 사출 더미")]
    [Tooltip("사출될 때 날아갈 연출용 모델 (리깅/애니메이터 필요 없음). 비워두면 원본만 숨긴다")]
    public GameObject ejectDummyPrefab;
    [Tooltip("더미가 생기는 위치 = 자리 위치 + 이 값 (자리 기준 로컬 좌표)")]
    public Vector3 dummyOffset = new Vector3(0f, 1f, 0f);
    public float ejectUpForce = 12f;
    public float ejectBackForce = 6f;
    public float ejectSpin = 8f;
    [Tooltip("더미가 사라지기까지 시간 (초). 재시작하면 그 전에도 지워진다")]
    public float dummyLifetime = 6f;

    // ===== [추가] 의자째 사출 (진짜 플레이어 모델 + 의자를 같이 날린다) =====
    [Header("[추가] 의자째 사출 — 체크하면 이 방식, 끄면 위의 더미 방식")]
    public bool flyWithChair = true;

    [Tooltip("자리별 의자 (0번 / 1번). 비워두면 플레이어만 날아간다. 플레이어 프리팹 밖(씬)에 있는 의자를 넣는다")]
    public Transform[] chairs = new Transform[2];

    [Tooltip("발사 전 덜덜 떨리는 시간(초)과 세기(m)")]
    public float shakeTime = 0.4f;
    public float shakeAmount = 0.03f;

    [Tooltip("위로 날아가는 시간(초)과 높이(m)")]
    public float flyTime = 1.6f;
    public float flyHeight = 25f;

    [Tooltip("가로 = 시간(0~1), 세로 = 높이 비율(0~1). 기본값: 처음엔 천천히, 갈수록 빨라짐")]
    public AnimationCurve flyCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0f),       // 시작: 기울기 0 → 천천히 출발
        new Keyframe(1f, 1f, 2.5f, 0f));    // 끝: 기울기 2.5 → 빠르게 치솟음

    [Tooltip("날아가면서 뒤로 젖혀지는 각도(도). 앞으로 숙여지면 음수로 바꾸기")]
    public float flyTiltAngle = 25f;

    [Tooltip("다 날아간 뒤 의자와 플레이어를 숨길지 (천장을 뚫고 보이는 것 방지)")]
    public bool hideAfterFlight = true;

    [Header("[추가] 천장 사출 문 (0번 자리 / 1번 자리) — 없으면 비워두기")]
    [Tooltip("자리 위 천장 문의 Animator. 사출되는 사람 자리의 문만 열린다")]
    public Animator[] hatchAnimators = new Animator[2];
    public string hatchTrigger = "Open";
    [Tooltip("문이 다 열리는 데 걸리는 시간(초). 이 시간 전에는 발사하지 않는다 (문에 머리 박는 것 방지)")]
    public float hatchOpenTime = 0.5f;

    [Header("[추가] 고개 축 쳐짐 (우주인 Animator의 Trigger)")]
    public string slumpTrigger = "Slump";
    [Tooltip("발사가 시작되고 몇 초 뒤에 고개가 쳐질지")]
    public float slumpDelay = 0.15f;
    // ===== [추가] 끝 =====

    [Header("[추가] 자리 보드 (Player1/Player2 숫자 칸) — RoundStart에 슬롯머신처럼 돌린다")]
    [Tooltip("SeatBoard가 붙은 오브젝트. 비워두면 보드는 talkTime에 그냥 숫자가 바뀐다")]
    public SeatBoard seatBoard;

    [Tooltip("[추가] 레버 당기기(우주인 동작) 시작 후 몇 초 뒤에 숫자가 돌기 시작할지. " +
             "레버가 '철컥' 내려가는 순간에 맞추면 된다. 보드와 홀로그램 스크램블이 같이 이 시간에 시작한다")]
    // [원래] public float rollStartDelay = 0.4f;
    public float rollStartDelay = 1.65f;    // [변경] 레버가 끝까지 내려가는 순간 (손이 1.63초에 다 당김)

    // ===== [추가] 레버 애니메이션 이벤트 =====
    [Header("[추가] 레버 애니메이션 이벤트 (lever_pull 클립의 OnLeverPull 이벤트에 맞춰 레버가 움직임)")]
    [Tooltip("체크하면 Lever Delay / Lever Sound Delay / Roll Start Delay 대신 클립 이벤트를 쓴다")]
    public bool useLeverEvent = true;
    [Tooltip("레버가 움직이기 시작하고 몇 초 뒤에 숫자가 돌기 시작할지 (레버가 다 내려가는 시간 ≈ 0.6초)")]
    public float rollAfterLever = 0.6f;
    [Tooltip("이 시간(초) 안에 이벤트가 안 오면 레버를 그냥 움직이고 진행한다 (멈춤 방지)")]
    public float leverEventTimeout = 3f;
    // ===== [추가] 끝 =====

    // ===== [추가] 게임 끝 연출: 우주인 비행 끝 → 타임라인 → EndGame 문구 =====
    [Header("[추가] 게임 끝 (GameOver) — 비행이 끝나면 타임라인, 그 다음 EndGame 문구")]
    [Tooltip("처형 타임라인. Play On Awake 끄고 Wrap Mode는 None. 비워두면 바로 문구로 넘어간다")]
    public PlayableDirector endTimeline;
    [Tooltip("비행이 끝나고 타임라인 시작 전 잠깐 쉬는 시간(초)")]
    public float beforeTimelineDelay = 0.3f;

    [Tooltip("EndGame 문구 묶음 (평소엔 꺼둠). CrtPopup이 붙어 있으면 TV 켜지듯 나온다. 비워두면 홀로그램 메시지만 바뀜")]
    public GameObject endGameRoot;
    [Tooltip("승패 문구를 쓸 텍스트들 (3D TextMeshPro / UI TextMeshProUGUI 둘 다 가능)")]
    public TMP_Text[] endGameTexts;
    public string winText = "SURVIVOR CONFIRMED";
    public string loseText = "CREW MEMBER EJECTED";
    // ===== [추가] 끝 =====

    [Header("[추가] 사운드 — 없으면 비워두기")]
    public AudioSource sfx;
    public AudioClip leverClip;
    public AudioClip scrambleTickClip;
    public AudioClip revealClip;
    public AudioClip overloadClip;
    public AudioClip chargeClip;
    public AudioClip ejectClip;
    public AudioClip safeClip;

    [Header("[추가] 디버그")]
    public bool debugLog = false;
    // ===== [추가] 끝 =====

    Coroutine presentation;

    // ===== [추가] 저장만 해두는 값 (명세서 7쪽) =====
    int pendingCard;
    int pendingRound;
    (Choice mine, Choice opp)? pendingReveal;
    (Result result, bool isMyTurn)? pendingDecision;
    bool? pendingOuted;
    (int before, int now)? pendingPer;

    int presserSeat = -1;                               // 이번 라운드에 누르는 사람 자리 (Open에서 정해짐)
    readonly List<GameObject> dummies = new List<GameObject>();
    readonly Dictionary<GameClient, bool> hiddenModels = new Dictionary<GameClient, bool>();

    // [추가] 의자째 사출 — 다음 라운드에 되돌리려고 기억해 두는 것들
    Coroutine flight;                                   // 날아가는 중인 코루틴
    GameClient flownClient;                             // 날아간 플레이어
    Transform flownChair;                               // 날아간 의자
    Vector3 chairStartPos;                              // 의자 원래 위치
    Quaternion chairStartRot;                           // 의자 원래 회전
    HeadLookSync flownLook;                             // 잠깐 꺼둔 시선 스크립트
    Animator flownAnim;                                 // 고개 쳐짐을 튼 Animator

    // [추가] 레버 이벤트 상태 (이번 라운드에 이미 움직였는지)
    readonly bool[] leverMoved = new bool[2];
    bool leverSoundDone;
    bool leverEventArrived;

    // [추가] 게임 끝 연출
    bool? pendingWin;                                   // 내가 이겼나 (GameOver 직전에 도착 → 저장만)
    Coroutine gameOverRoutine;
    // ===== [추가] 끝 =====


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
        client.OnNumberSelected += HandleOpponentCard;          // [추가]
        AstronautAnimEvents.LeverPulled += HandleLeverPulled;   // [추가] 레버 애니메이션 이벤트
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
        client.OnNumberSelected -= HandleOpponentCard;          // [추가]
        AstronautAnimEvents.LeverPulled -= HandleLeverPulled;   // [추가]
    }

    protected override void Refresh()
    {
        HandlePhase(client.curPhase);
    }

    // ===== [추가] 방을 나갈 때 / 꺼질 때 정리 (명세서 9쪽) =====
    protected override void OnClientLost()
    {
        StopPresentation();
        StopGameOver();                                 // [추가]
        ResetStage();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        StopPresentation();
        StopGameOver();                                 // [추가]
        ResetStage();
    }
    // ===== [추가] 끝 =====


    // ────────────────────────── 페이즈 ──────────────────────────

    void HandlePhase(GamePhase phase)
    {
        SetActiveTexts(perTexts, phase != GamePhase.waiting);
        SetActiveTexts(roundTexts, phase != GamePhase.waiting && phase != GamePhase.GameOver);

        // ===== [추가] 연출 페이즈가 아니면 진행 중이던 연출을 끊는다 (명세서 14쪽) =====
        if (phase != GamePhase.RoundStart && phase != GamePhase.Open && phase != GamePhase.Result)
            StopPresentation();

        // GameOver가 아니면 게임 끝 연출(타임라인/문구)도 끊고 숨긴다 (재시작, 방 대기 등)
        if (phase != GamePhase.GameOver)
            StopGameOver();
        // ===== [추가] 끝 =====

        switch (phase)
        {
            case GamePhase.waiting:
                ResetStage();                                   // [추가] 더미 제거 + 모델 복구
                ShowWaitingMessage();
                break;

            case GamePhase.RoundStart:
                // [원래] SetMessage("SURVIVAL PRIORITY CALCULATION", normalColor);
                // [원래] StartPresentation(roundStartTime);
                ResetStage();                                   // [추가] GameOver → 재시작일 때 모델 복구
                StartPresentation(phase);                       // [변경]
                break;

            case GamePhase.talkTime:
                SetMessage("", normalColor);
                break;

            case GamePhase.Open:
                // [원래] StartPresentation(openTime);
                StartPresentation(phase);                       // [변경]
                break;

            case GamePhase.Press:
                SetMessage("AWAITING INPUT", warnColor);
                break;

            case GamePhase.Result:
                // [원래] StartPresentation(resultTime);
                StartPresentation(phase);                       // [변경]
                break;

            case GamePhase.GameOver:
                // [원래] break;      // 메시지는 OnGameWin에서 설정됨
                StartGameOver();                                // [변경] 비행 끝 → 타임라인 → EndGame 문구
                break;
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
    // [변경] 값은 받자마자 화면에 쓰지 않고 저장만 한다. 공개는 연출 코루틴이 한다.

    // [원래] void HandleRound(int round) => SetTexts(roundTexts, $"ROUND {round}");
    void HandleRound(int round)
    {
        pendingRound = round;

        // 1라운드 = 새 게임. 서버가 첫 확률을 안 보내주므로 규칙값으로 보여준다 (명세서 14쪽)
        if (round == 1)
        {
            var rule = GameManager.Instance != null ? GameManager.Instance.gameRule : null;
            if (rule != null) ShowPer(rule.basicPer, normalColor);
        }
    }

    void HandleOpponentCard(int card) => pendingCard = card;   // [추가]

    /// <summary>
    /// [추가] 우주인 lever_pull 클립의 OnLeverPull 이벤트 → 그 자리 레버를 움직인다.
    /// 각자 화면에서 각자 우주인 애니메이션이 부르므로 네트워크와 상관없이 양쪽 다 맞는다.
    /// </summary>
    void HandleLeverPulled(int seat)
    {
        if (!useLeverEvent || client == null || client.curPhase != GamePhase.RoundStart) return;
        if (seat < 0 || seat >= 2 || seat >= leverAnimators.Length || leverMoved[seat]) return;

        leverMoved[seat] = true;
        leverEventArrived = true;
        Trigger(leverAnimators[seat], leverTrigger);

        if (!leverSoundDone) { leverSoundDone = true; Play(leverClip); }   // 두 자리가 동시라 소리는 한 번만
        Log($"레버 이벤트: 자리 {seat}");
    }


    void HandlePer(int before, int now)
    {
        // [원래] SetTexts(perTexts, $"{now}%");
        // [원래] Color c = (now > before) ? warnColor : normalColor;
        // [원래] foreach (var t in perTexts)
        // [원래]     if (t != null) t.color = c;
        pendingPer = (before, now);                             // [변경]
    }

    void HandleChoiceRevealed(Choice mine, Choice opponent)
    {
        // [원래] if (mine == Choice.Overload || opponent == Choice.Overload)
        // [원래]     SetMessage("SYSTEM OVERLOAD", warnColor);
        pendingReveal = (mine, opponent);                       // [변경]
    }

    void HandleRoundDecided(Result result, bool isMyTurn)
    {
        // [원래] if (result == Result.draw)
        // [원래]     SetMessage("SURVIVAL PRIORITY EQUAL", normalColor);
        // [원래] else if (result == Result.fold)
        // [원래]     SetMessage("ROUND ABORTED", normalColor);
        pendingDecision = (result, isMyTurn);                   // [변경]
    }

    void HandleOuted(bool outed)
    {
        // [원래] SetMessage(outed ? "EJECTION SYSTEM ACTIVATED" : "PRESSURE LOCK HELD",
        // [원래]            outed ? warnColor : normalColor);
        pendingOuted = outed;                                   // [변경]
    }

    // [원래] GameOver는 연출이 없는 페이즈라 받자마자 보여준다
    // [변경] 이제 GameOver에도 연출(타임라인)이 있으므로 저장만 하고, 타임라인이 끝난 뒤 보여준다
    void HandleGameWin(bool win)
    {
        // [원래] SetMessage(win ? "SURVIVOR CONFIRMED" : "CREW MEMBER EJECTED",
        // [원래]            win ? normalColor : warnColor);
        pendingWin = win;                                       // [변경]
    }

    // ===== [추가] 게임 끝 연출 =====
    //
    // 순서: (Result 연출에서 시작된) 우주인 비행이 끝날 때까지 기다림
    //       → 처형 타임라인 재생 → 끝나면 EndGame 문구
    // GameOver는 서버가 연출 끝 신호를 기다리지 않는 페이즈라 EndPresentation을 보내지 않는다.

    void StartGameOver()
    {
        StopGameOver();
        gameOverRoutine = StartCoroutine(GameOverRoutine());
    }

    IEnumerator GameOverRoutine()
    {
        // 1) 비행이 아직 진행 중이면 끝날 때까지 (flight는 비행 코루틴이 끝나면 null이 된다)
        while (flight != null) yield return null;

        if (beforeTimelineDelay > 0f)
            yield return new WaitForSeconds(beforeTimelineDelay);

        // 2) 처형 타임라인
        if (endTimeline != null)
        {
            Log("처형 타임라인 시작");
            endTimeline.time = 0;
            endTimeline.Play();

            // 길이만큼 기다린다. Wrap Mode가 Hold여도 멈추지 않고 넘어가도록 시간으로도 끊는다
            float endAt = Time.time + (float)endTimeline.duration;
            yield return null;
            while (Time.time < endAt && endTimeline.state == PlayState.Playing)
                yield return null;
        }

        // 3) EndGame 문구
        ShowEndGame(pendingWin ?? false);
        gameOverRoutine = null;
    }

    void ShowEndGame(bool win)
    {
        string s = win ? winText : loseText;
        Color c = win ? normalColor : warnColor;

        SetMessage(s, c);                                       // 홀로그램에도 (원래 하던 것)

        if (endGameTexts != null)
            foreach (var t in endGameTexts)
                if (t != null) { t.text = s; t.color = c; }

        if (endGameRoot != null)
        {
            var crt = endGameRoot.GetComponent<CrtPopup>();
            if (crt != null) crt.Show();                        // TV 켜지듯
            else endGameRoot.SetActive(true);
        }
        Log($"EndGame 문구: {s}");
    }

    /// <summary>게임 끝 연출 중단 + 문구 숨김 (재시작 / 대기 / 방 나감)</summary>
    void StopGameOver()
    {
        if (gameOverRoutine != null) { StopCoroutine(gameOverRoutine); gameOverRoutine = null; }

        if (endTimeline != null && endTimeline.state == PlayState.Playing)
            endTimeline.Stop();

        if (endGameRoot != null)
        {
            var crt = endGameRoot.GetComponent<CrtPopup>();
            if (crt != null) crt.HideImmediate();
            else endGameRoot.SetActive(false);
        }

        // pendingWin은 여기서 지우지 않는다.
        // 승패(OnGameWin)는 GameOver 페이즈보다 "먼저" 도착하는데, StartGameOver도 이 함수를 부르므로
        // 여기서 지우면 방금 받은 승패가 날아간다. 다음 게임의 승패가 오면 덮어써진다.
    }
    // ===== [추가] 끝 =====


    // ────────────────────────── 연출 ──────────────────────────

    // [원래]
    //void StartPresentation(float seconds)
    //{
    //    if (presentation != null) StopCoroutine(presentation);
    //    presentation = StartCoroutine(PresentationRoutine(seconds));
    //}
    //
    //IEnumerator PresentationRoutine(float seconds)
    //{
    //    // 여기에 실제 연출을 넣는다 (깜빡임, 사운드, 숫자 스크램블 등)
    //    yield return new WaitForSeconds(seconds);
    //
    //    presentation = null;
    //    if (client != null) client.EndPresentation();
    //}

    // ===== [변경] 페이즈별 연출 (명세서 8~10쪽) =====

    void StartPresentation(GamePhase phase)
    {
        StopPresentation();
        presentation = StartCoroutine(PresentationRoutine(phase));
    }

    void StopPresentation()
    {
        if (presentation != null) { StopCoroutine(presentation); presentation = null; }
    }

    IEnumerator PresentationRoutine(GamePhase phase)
    {
        Log($"연출 시작: {phase}");

        switch (phase)
        {
            case GamePhase.RoundStart: yield return RoundStartRoutine(); break;
            case GamePhase.Open: yield return OpenRoutine(); break;
            case GamePhase.Result: yield return ResultRoutine(); break;
        }

        presentation = null;

        // 끝났을 때도 여전히 같은 페이즈일 때만, 딱 한 번 보낸다
        if (client != null && client.curPhase == phase)
        {
            Log($"연출 끝 → EndPresentation ({phase})");
            client.EndPresentation();
        }
    }

    /// <summary>약 2.5초: ROUND n → 레버 → 숫자 스크램블 → 상대 카드 확정</summary>
    IEnumerator RoundStartRoutine()
    {
        presserSeat = -1;

        SetMessage("SURVIVAL PRIORITY CALCULATION", normalColor);
        SetTexts(roundTexts, $"ROUND {pendingRound}");
        yield return new WaitForSeconds(0.5f);

        // [변경] 우주인 동작을 먼저 시작하고, 손이 닿는 시점(leverDelay)에 레버가 움직인다
        // ===== [변경] 레버: 애니메이션 이벤트 방식 / 시간 방식 =====
        leverMoved[0] = leverMoved[1] = false;
        leverSoundDone = false;
        leverEventArrived = false;

        if (useLeverEvent && animateAstronaut)
        {
            // 이벤트 방식: 우주인만 출발시키고, 레버는 클립 이벤트(OnLeverPull)가 오면 HandleLeverPulled가 움직인다
            Trigger(GetAstronautAnimator(0), astronautPullTrigger);
            Trigger(GetAstronautAnimator(1), astronautPullTrigger);

            float waited = 0f;
            while (!leverEventArrived && waited < leverEventTimeout)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            if (!leverEventArrived)     // 이벤트가 안 옴 → 레버를 지금 움직이고 그냥 진행 (멈추지 않게)
            {
                Log("레버 이벤트가 안 옴 (클립 이벤트 / AstronautAnimEvents 위치 확인)");
                for (int i = 0; i < leverAnimators.Length && i < 2; i++)
                    if (!leverMoved[i]) { leverMoved[i] = true; Trigger(leverAnimators[i], leverTrigger); }
                if (!leverSoundDone) { leverSoundDone = true; Play(leverClip); }
            }

            // 레버가 다 내려갈 때까지 기다렸다가 숫자 시작
            yield return new WaitForSeconds(rollAfterLever);
        }
        else
        {
            // 시간 방식 (이벤트를 안 쓸 때)
            // [추가] Lever Delay가 음수면 = 레버를 먼저 출발시키고, 우주인을 그만큼 늦게 출발시킨다
            float astroWait = Mathf.Max(0f, -leverDelay);
            float leverWait = Mathf.Max(0f, leverDelay);

            if (animateAstronaut)
            {
                // [원래] Trigger(GetAstronautAnimator(0), astronautPullTrigger);
                // [원래] Trigger(GetAstronautAnimator(1), astronautPullTrigger);
                TriggerLater(GetAstronautAnimator(0), astronautPullTrigger, astroWait);
                TriggerLater(GetAstronautAnimator(1), astronautPullTrigger, astroWait);
            }
            // [원래] foreach (var a in leverAnimators) TriggerLater(a, leverTrigger, leverDelay);
            foreach (var a in leverAnimators) TriggerLater(a, leverTrigger, leverWait);
            // [원래] Play(leverClip);
            PlayLater(leverClip, astroWait + leverSoundDelay);
            // [원래] yield return new WaitForSeconds(0.4f);
            yield return new WaitForSeconds(astroWait + rollStartDelay);
        }
        // ===== [변경] 끝 =====

        // [추가] 자리 보드(Player1/Player2 칸) 슬롯머신 시작 — 홀로그램 스크램블과 동시에 돈다
        if (seatBoard != null) seatBoard.PlayRoll(pendingCard);

        // 숫자 스크램블
        float t = 0f, tick = 0f;
        // [원래] while (t < 0.8f)
        while (t < 0.8f || (seatBoard != null && seatBoard.IsRolling))   // [변경] 보드가 멈출 때까지 같이 돈다
        {
            t += Time.deltaTime;
            tick += Time.deltaTime;
            if (tick >= 0.08f)
            {
                tick = 0f;
                SetMessage($"OPPONENT PRIORITY  {Random.Range(1, 11)}", normalColor);
                Play(scrambleTickClip);
            }
            yield return null;
        }

        SetMessage($"OPPONENT PRIORITY  {pendingCard}", normalColor);
        Play(revealClip);
        yield return new WaitForSeconds(0.8f);
    }

    /// <summary>최대 약 3초: 선택 공개 → (OVERLOAD 경고) → 누를 사람 공개</summary>
    IEnumerator OpenRoutine()
    {
        // 1) 선택 공개
        if (pendingReveal.HasValue)
        {
            var (mine, opp) = pendingReveal.Value;
            SetMessage($"YOU {ChoiceLabel(mine)}   OPPONENT {ChoiceLabel(opp)}", normalColor);
            Play(revealClip);
            yield return new WaitForSeconds(1.0f);

            if (mine == Choice.Overload || opp == Choice.Overload)
            {
                SetMessage("SYSTEM OVERLOAD", warnColor);
                Trigger(alarmAnimator, alarmTrigger);
                Play(overloadClip);
                yield return new WaitForSeconds(0.8f);
            }
        }

        // 2) 누를 사람 공개
        if (pendingDecision.HasValue)
        {
            var (result, myTurn) = pendingDecision.Value;

            if (result == Result.draw)
                SetMessage("SURVIVAL PRIORITY EQUAL", normalColor);
            else if (result == Result.fold)
                SetMessage("ROUND ABORTED", normalColor);
            else
            {
                // 누르는 사람 자리 = 내가 누르면 내 자리, 아니면 상대 자리 (양쪽 화면에서 같은 쪽이 된다)
                int me = client.myIndex.Value;
                presserSeat = myTurn ? me : 1 - me;
                SetMessage(myTurn ? "YOU CONTROL THE EJECTION" : "OPPONENT CONTROLS THE EJECTION", warnColor);
            }
            yield return new WaitForSeconds(1.2f);
        }

        pendingReveal = null;
        pendingDecision = null;
    }

    /// <summary>약 4초: 버튼 눌림 → 서스펜스 → 사출 or 생존 → 확률 변동</summary>
    IEnumerator ResultRoutine()
    {
        // 1) 누른 사람 자리의 버튼이 눌린다
        // [변경] 누른 사람 우주인이 버튼을 누르는 동작을 먼저 시작하고,
        //        손가락이 닿는 시점(buttonDelay)에 그 자리 버튼이 눌린다
        if (animateAstronaut && presserSeat >= 0)
            Trigger(GetAstronautAnimator(presserSeat), astronautPressTrigger);

        if (presserSeat >= 0 && presserSeat < pressButtonAnimators.Length)
            TriggerLater(pressButtonAnimators[presserSeat], pressTrigger, buttonDelay);

        SetMessage("EJECTION SYSTEM CHARGING...", warnColor);
        Play(chargeClip);
        yield return new WaitForSeconds(1.5f);

        // 2) 사고 여부 공개
        bool outed = pendingOuted ?? false;
        SetMessage(outed ? "EJECTION SYSTEM ACTIVATED" : "PRESSURE LOCK HELD",
                   outed ? warnColor : normalColor);

        if (outed)
        {
            Play(ejectClip);
            // [원래] Eject(presserSeat);                        // 누른 사람이 사출된다
            if (flyWithChair) StartEjectFlight(presserSeat);    // [변경] 의자째 위로 + 고개 쳐짐
            else { OpenHatch(presserSeat); Eject(presserSeat); }   // [변경] 체크 끄면 예전 더미 방식 (+ 천장 문)
        }
        else
        {
            Play(safeClip);
        }
        yield return new WaitForSeconds(1.0f);

        // 3) 확률 변동 공개 (사출됐으면 before == now)
        if (pendingPer.HasValue)
        {
            var (before, now) = pendingPer.Value;
            ShowPer(now, (now > before) ? warnColor : normalColor);
            yield return new WaitForSeconds(1.0f);
        }

        pendingOuted = null;
        pendingPer = null;
    }


    // ────────────────────────── 사출 / 무대 정리 ──────────────────────────

    /// <summary>
    /// 그 자리 사람의 원본 모델을 숨기고, 연출용 더미를 날린다.
    /// 원본(GameClient의 자식)을 파괴하면 GameClient까지 사라지므로 Renderer만 끈다 (명세서 6번).
    /// </summary>
    void Eject(int seat)
    {
        if (seat < 0) return;

        GameClient target = FindClientBySeat(seat);
        if (target != null) SetModelVisible(target, false);

        if (ejectDummyPrefab == null) return;

        Transform seatT = (GameManager.Instance != null && GameManager.Instance.seats.Length > seat)
            ? GameManager.Instance.seats[seat] : null;
        if (seatT == null && target != null) seatT = target.transform;
        if (seatT == null) return;

        Vector3 pos = seatT.TransformPoint(dummyOffset);
        GameObject dummy = Instantiate(ejectDummyPrefab, pos, seatT.rotation);
        dummies.Add(dummy);

        var rb = dummy.GetComponent<Rigidbody>();
        if (rb == null) rb = dummy.AddComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.AddForce(Vector3.up * ejectUpForce - seatT.forward * ejectBackForce, ForceMode.VelocityChange);
        rb.AddTorque(Random.onUnitSphere * ejectSpin, ForceMode.VelocityChange);

        Destroy(dummy, dummyLifetime);
        Log($"사출: 자리 {seat}");
    }

    /// <summary>재시작/대기/퇴장 시: 더미 제거 + 숨겼던 모델 복구</summary>
    void ResetStage()
    {
        StopEjectFlight();                                  // [추가] 날아간 의자/플레이어 원위치
        ResetHatches();                                     // [추가] 천장 문 닫기

        foreach (var d in dummies) if (d != null) Destroy(d);
        dummies.Clear();

        foreach (var kv in hiddenModels)
            if (kv.Key != null) SetRenderers(kv.Key, true);
        hiddenModels.Clear();

        presserSeat = -1;
    }

    // ===== [추가] 의자째 사출 =====
    //
    // 흐름: 덜덜 떨림(shakeTime) → 위로 발사(flyTime) + 고개 쳐짐 → 숨김
    // 각자 화면에서 똑같이 계산해서 움직인다 (네트워크로 위치를 주고받지 않음).
    // 떨림만 랜덤이라 양쪽 화면이 미세하게 다르지만, 날아가는 경로는 같다.

    /// <summary>그 자리의 플레이어와 의자를 같이 위로 날린다</summary>
    void StartEjectFlight(int seat)
    {
        if (seat < 0) return;
        StopEjectFlight();                                  // 혹시 이전 것이 남아 있으면 먼저 되돌림

        GameClient target = FindClientBySeat(seat);
        if (target == null) return;

        flownClient = target;
        flownChair = (chairs != null && seat < chairs.Length) ? chairs[seat] : null;
        if (flownChair != null)
        {
            chairStartPos = flownChair.position;
            chairStartRot = flownChair.rotation;
        }

        // ★ 중요: HeadLookSync는 매 프레임(LateUpdate) 머리 본 회전을 덮어쓴다.
        //   켜둔 채로 두면 고개 쳐짐 애니메이션의 머리 움직임이 지워진다 → 잠깐 끈다.
        flownLook = target.GetComponentInChildren<HeadLookSync>();
        if (flownLook != null) flownLook.enabled = false;

        flownAnim = target.GetComponentInChildren<Animator>();

        OpenHatch(seat);                                    // [추가] 천장 문이 열리면서 떨림 시작

        flight = StartCoroutine(EjectFlightRoutine(target.transform, flownChair));
        Log($"의자째 사출: 자리 {seat}");
    }

    IEnumerator EjectFlightRoutine(Transform player, Transform chair)
    {
        // 기준점(pivot) = 의자가 있으면 의자, 없으면 플레이어.
        // 기준점을 옮기고 돌리면, 의자와 플레이어가 한 덩어리처럼 같이 따라간다.
        Transform pivotT = (chair != null) ? chair : player;
        Vector3 pivot = pivotT.position;
        Vector3 tiltAxis = pivotT.right;                    // 옆 방향 축으로 뒤로 젖힌다

        Vector3 pStart = player.position;
        Quaternion pRot = player.rotation;
        Vector3 cStart = (chair != null) ? chair.position : Vector3.zero;
        Quaternion cRot = (chair != null) ? chair.rotation : Quaternion.identity;

        // 1) 발사 직전 덜덜 떨림 (천장 문이 다 열릴 때까지는 계속 떤다)
        // [원래] while (t < shakeTime)
        float shakeDuration = Mathf.Max(shakeTime, hatchOpenTime);         // [추가]
        float t = 0f;
        while (t < shakeDuration)                                          // [변경]
        {
            t += Time.deltaTime;
            Vector3 jitter = Random.insideUnitSphere * shakeAmount;
            Place(player, chair, pivot + jitter, Quaternion.identity, pivot, pStart, pRot, cStart, cRot);
            yield return null;
        }

        // 2) 발사 + 고개 쳐짐
        TriggerLater(flownAnim, slumpTrigger, slumpDelay);

        t = 0f;
        while (t < flyTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / flyTime);                           // 진행도 0 → 1

            Vector3 newPivot = pivot + Vector3.up * (flyCurve.Evaluate(k) * flyHeight);
            // 오른쪽 축 기준 음수 회전 = 윗부분이 뒤로 넘어감
            Quaternion tilt = Quaternion.AngleAxis(-flyTiltAngle * k, tiltAxis);

            Place(player, chair, newPivot, tilt, pivot, pStart, pRot, cStart, cRot);
            yield return null;
        }

        // 3) 화면 밖으로 나갔으니 숨긴다 (다음 RoundStart / waiting에서 ResetStage가 되돌림)
        if (hideAfterFlight)
        {
            if (flownClient != null) SetRenderers(flownClient, false);
            if (chair != null) SetRenderersOf(chair, false);
        }

        flight = null;
    }

    /// <summary>
    /// 기준점을 newPivot으로 옮기고 tilt만큼 돌렸을 때 의자/플레이어가 있어야 할 곳에 놓는다.
    /// (기준점에서 떨어진 거리(pStart - pivot)도 같이 돌려야 한 덩어리처럼 기울어진다)
    /// </summary>
    static void Place(Transform player, Transform chair, Vector3 newPivot, Quaternion tilt,
                      Vector3 pivot, Vector3 pStart, Quaternion pRot, Vector3 cStart, Quaternion cRot)
    {
        player.SetPositionAndRotation(newPivot + tilt * (pStart - pivot), tilt * pRot);
        if (chair != null)
            chair.SetPositionAndRotation(newPivot + tilt * (cStart - pivot), tilt * cRot);
    }

    /// <summary>날아간 의자/플레이어를 원래대로 (위치, 보이기, 시선 스크립트, 애니메이션)</summary>
    void StopEjectFlight()
    {
        if (flight != null) { StopCoroutine(flight); flight = null; }

        if (flownChair != null)
        {
            flownChair.SetPositionAndRotation(chairStartPos, chairStartRot);
            SetRenderersOf(flownChair, true);
        }

        if (flownClient != null)                            // 방을 나가 파괴됐으면 null로 판정됨
        {
            flownClient.MoveSeat();                         // 동료 코드의 public 함수: 원래 자리로 이동
            SetRenderers(flownClient, true);
        }

        if (flownLook != null) flownLook.enabled = true;   // 시선 다시 켬

        if (flownAnim != null)
        {
            flownAnim.Rebind();                             // Animator를 처음 상태(Idle)로 되돌림
            flownAnim.Update(0f);
        }

        flownClient = null;
        flownChair = null;
        flownLook = null;
        flownAnim = null;
    }

    /// <summary>[추가] 그 자리 위 천장 문을 연다</summary>
    void OpenHatch(int seat)
    {
        if (hatchAnimators == null || seat < 0 || seat >= hatchAnimators.Length) return;
        Trigger(hatchAnimators[seat], hatchTrigger);
        Log($"천장 문 열기: 자리 {seat}");                    // [추가] 코드가 연 건지 확인용
    }

    /// <summary>[추가] 천장 문을 전부 닫힌 처음 상태로 (Animator 기본 State = 닫힘이어야 함)</summary>
    void ResetHatches()
    {
        if (hatchAnimators == null) return;
        foreach (var a in hatchAnimators)
        {
            if (a == null || !a.isActiveAndEnabled) continue;
            a.Rebind();
            a.Update(0f);
        }
    }

    static void SetRenderersOf(Transform root, bool on)
    {
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            r.enabled = on;
    }
    // ===== [추가] 끝 =====

    /// <summary>내 컴퓨터에 있는 GameClient 중 그 자리 번호를 가진 것 (양쪽 플레이어 모두 존재한다)</summary>
    static GameClient FindClientBySeat(int seat)
    {
        foreach (var c in FindObjectsByType<GameClient>(FindObjectsSortMode.None))
            if (c.IsSpawned && c.myIndex.Value == seat) return c;
        return null;
    }

    /// <summary>
    /// [추가] 그 자리 우주인의 Animator.
    /// 우주인은 플레이어 프리팹(GameClient) 안에 있어서 Inspector로 연결할 수 없으므로 실행 중에 찾는다.
    /// </summary>
    static Animator GetAstronautAnimator(int seat)
    {
        GameClient c = FindClientBySeat(seat);
        return c != null ? c.GetComponentInChildren<Animator>() : null;
    }

    void SetModelVisible(GameClient c, bool visible)
    {
        SetRenderers(c, visible);
        if (!visible) hiddenModels[c] = true;
    }

    static void SetRenderers(GameClient c, bool on)
    {
        foreach (var r in c.GetComponentsInChildren<Renderer>(true))
            r.enabled = on;
    }


    // ────────────────────────── 헬퍼 ──────────────────────────

    void SetMessage(string s, Color c)
    {
        SetTexts(messageTexts, s);
        foreach (var t in messageTexts)
            if (t != null) t.color = c;
    }

    // [추가] 확률 표시 (글자 + 색)
    void ShowPer(int per, Color c)
    {
        SetTexts(perTexts, $"{per}%");
        if (perTexts == null) return;
        foreach (var t in perTexts)
            if (t != null) t.color = c;
    }

    // [추가]
    static string ChoiceLabel(Choice c) =>
        c == Choice.Go ? "GO" : c == Choice.Fold ? "FOLD" : "OVERLOAD";

    // [추가] Animator가 비어 있거나 꺼져 있으면 조용히 넘어간다
    static void Trigger(Animator a, string trigger)
    {
        if (a == null || !a.isActiveAndEnabled || string.IsNullOrEmpty(trigger)) return;
        a.SetTrigger(trigger);
    }

    // [추가] delay초 뒤에 트리거 (0이면 바로). 연출 흐름(대기 시간)은 막지 않는다
    void TriggerLater(Animator a, string trigger, float delay)
    {
        if (a == null) return;
        if (delay <= 0f) { Trigger(a, trigger); return; }
        StartCoroutine(TriggerAfter(a, trigger, delay));
    }

    IEnumerator TriggerAfter(Animator a, string trigger, float delay)
    {
        yield return new WaitForSeconds(delay);
        Trigger(a, trigger);
    }

    // [추가] 사운드가 비어 있으면 조용히 넘어간다
    // [추가] delay초 뒤에 소리 (0이면 바로)
    void PlayLater(AudioClip clip, float delay)
    {
        if (clip == null) return;
        if (delay <= 0f) { Play(clip); return; }
        StartCoroutine(PlayAfter(clip, delay));
    }

    IEnumerator PlayAfter(AudioClip clip, float delay)
    {
        yield return new WaitForSeconds(delay);
        Play(clip);
    }

    void Play(AudioClip clip)
    {
        if (sfx != null && clip != null) sfx.PlayOneShot(clip);
    }

    // [추가]
    void Log(string msg)
    {
        if (debugLog) Debug.Log($"[CenterHologram] {msg}");
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