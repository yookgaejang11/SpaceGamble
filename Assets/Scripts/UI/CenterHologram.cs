using System.Collections;
using System.Collections.Generic;   // 사출 더미 목록
using TMPro;
using UnityEngine;
using UnityEngine.Playables;          // 게임 끝 타임라인 (PlayableDirector)

public class CenterHologram : ClientUIBase
{
    /// <summary>
    /// 처형 타임라인까지 다 끝나서 "이제 승패 문구를 띄워도 된다"는 신호 (true = 내가 이김).
    /// GameResultText 같은 다른 스크립트가 이걸 듣고 EndGame 문구를 켠다.
    /// </summary>
    public static event System.Action<bool> EndGameReady;

    [Header("확률 (앞면 / 뒷면)")]
    public TextMeshPro[] perTexts;

    [Header("시스템 메시지 (앞면 / 뒷면)")]
    public TextMeshPro[] messageTexts;

    [Header("라운드 (앞면 / 뒷면) — 안 쓰면 비워두기")]
    public TextMeshPro[] roundTexts;

    [Header("색")]
    public Color normalColor = new Color(0.6f, 0.9f, 1f);
    public Color warnColor = new Color(1f, 0.4f, 0.3f);


    [Header("레버 (0번 자리 / 1번 자리)")]
    [Tooltip("RoundStart에 양쪽 레버가 같이 당겨진다")]
    public Animator[] leverAnimators = new Animator[2];
    public string leverTrigger = "Pull";

    [Header("사출 버튼 (0번 자리 / 1번 자리)")]
    [Tooltip("Result에 '누른 사람' 자리의 버튼만 눌림 애니메이션")]
    public Animator[] pressButtonAnimators = new Animator[2];
    public string pressTrigger = "Press";

    [Header("우주인 애니메이션 (플레이어 프리팹 안의 Animator — 실행 중 자리 번호로 자동으로 찾음)")]
    [Tooltip("체크하면 RoundStart에 양쪽 우주인이 레버를 당기고, Result에 누른 사람이 버튼을 누른다")]
    public bool animateAstronaut = true;
    [Tooltip("astronaut_1 컨트롤러의 Trigger 이름 (lever_pull로 가는 조건)")]
    public string astronautPullTrigger = "Pull";
    [Tooltip("astronaut_1 컨트롤러의 Trigger 이름 (butten_push로 가는 조건)")]
    public string astronautPressTrigger = "Press";

    [Header("손이 닿는 타이밍 맞추기 (초)")]
    [Tooltip("우주인이 lever_pull을 시작하고 몇 초 뒤에 레버를 트리거할지. " +
             "레버가 늦으면 줄이고, 0으로도 늦으면 음수(-0.3 등)로: 레버를 먼저 출발시키고 우주인이 그만큼 늦게 출발한다")]
    // 클립 분석값: lever_pull에서 왼손이 레버를 끌어내리는 구간 = 1.1초~1.63초,
    //        lever_action은 0.47초까지 가만히 있다가 움직임 → 1.1 - 0.47 ≈ 0.65초 늦게 트리거
    public float leverDelay = 0.65f;
    [Tooltip("우주인 동작 시작 후 몇 초 뒤에 레버 소리를 낼지 (레버가 실제로 움직이기 시작하는 순간 ≈ 1.1초)")]
    public float leverSoundDelay = 1.1f;
    [Tooltip("우주인이 butten_push를 시작하고 몇 초 뒤에 버튼이 눌리기 시작할지 (손가락이 버튼에 닿는 순간)")]
    public float buttonDelay = 0f;

    [Header("경고등 (OVERLOAD일 때) — 없으면 비워두기")]
    public Animator alarmAnimator;
    public string alarmTrigger = "Alarm";

    [Header("사출 더미")]
    [Tooltip("사출될 때 날아갈 연출용 모델 (리깅/애니메이터 필요 없음). 비워두면 원본만 숨긴다")]
    public GameObject ejectDummyPrefab;
    [Tooltip("더미가 생기는 위치 = 자리 위치 + 이 값 (자리 기준 로컬 좌표)")]
    public Vector3 dummyOffset = new Vector3(0f, 1f, 0f);
    public float ejectUpForce = 12f;
    public float ejectBackForce = 6f;
    public float ejectSpin = 8f;
    [Tooltip("더미가 사라지기까지 시간 (초). 재시작하면 그 전에도 지워진다")]
    public float dummyLifetime = 6f;

    [Header("의자째 사출 — 체크하면 이 방식, 끄면 위의 더미 방식")]
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

    [Header("천장 사출 문 (0번 자리 / 1번 자리) — 없으면 비워두기")]
    [Tooltip("자리 위 천장 문의 Animator. 사출되는 사람 자리의 문만 열린다")]
    public Animator[] hatchAnimators = new Animator[2];
    public string hatchTrigger = "Open";
    [Tooltip("문이 다 열리는 데 걸리는 시간(초). 이 시간 전에는 발사하지 않는다 (문에 머리 박는 것 방지)")]
    public float hatchOpenTime = 0.5f;

    [Header("고개 축 쳐짐 (우주인 Animator의 Trigger)")]
    public string slumpTrigger = "Slump";
    [Tooltip("발사가 시작되고 몇 초 뒤에 고개가 쳐질지")]
    public float slumpDelay = 0.15f;

    [Header("의자 밑 불꽃 (0번 자리 / 1번 자리)")]
    [Tooltip("의자 자식으로 넣은 불꽃 ParticleSystem. 발사 순간 재생, 다음 라운드에 꺼짐. " +
             "Play On Awake 끄기, Simulation Space = World (불꽃 꼬리가 뒤에 남게)")]
    public ParticleSystem[] thrusterFx = new ParticleSystem[2];

    [Header("자리 보드 (Player1/Player2 숫자 칸) — RoundStart에 슬롯머신처럼 돌린다")]
    [Tooltip("SeatBoard가 붙은 오브젝트. 비워두면 보드는 talkTime에 그냥 숫자가 바뀐다")]
    public SeatBoard seatBoard;

    [Header("카드 공개 (Open 연출, 버튼 누르기 전)")]
    [Tooltip("체크하면 FOLD로 끝난 라운드에도 숫자를 공개한다")]
    public bool revealOnFold = false;

    [Header("누를 차례 표시 (0번 자리 / 1번 자리)")]
    [Tooltip("누를 사람 자리에서만 켜질 오브젝트 (빨간 조명, 화살표, 깜빡이는 링 등). 평소엔 꺼둔다")]
    public GameObject[] presserMarkers = new GameObject[2];
    public string youPressText = "YOU MUST PRESS THE BUTTON";
    public string oppPressText = "OPPONENT MUST PRESS THE BUTTON";
    [Tooltip("Press 단계 문구 (내 차례 / 상대 차례)")]
    public string pressNowText = "PRESS THE BUTTON";
    public string waitPressText = "WAITING FOR OPPONENT";

    [Header("누를 사람 버튼 빨간불 (0번 자리 / 1번 자리)")]
    [Tooltip("각 자리 사출 버튼에 붙인 Light (Point Light 추천). 평소엔 꺼둔다. 누를 사람 쪽만 깜빡인다")]
    public Light[] pressButtonLights = new Light[2];
    public Color blinkColor = Color.red;
    [Tooltip("켜져 있는 시간 / 꺼져 있는 시간 (초)")]
    public float blinkOnTime = 0.25f;
    public float blinkOffTime = 0.25f;

    [Header("OVERLOAD 확률 표시")]
    [Tooltip("확률 숫자가 원래 값 → 올라간 값으로 올라가는 시간(초)")]
    public float overloadCountTime = 0.6f;

    [Header("사출 사이렌")]
    [Tooltip("사출될 때 켤 사이렌 오브젝트들 (불 2개 등). 평소엔 꺼둔다 (오브젝트째 꺼도 됨)")]
    public GameObject[] sirenObjects = new GameObject[2];
    [Tooltip("SoundManager Sounds 목록의 사이렌 id (반복 재생)")]
    public string sndSiren = "siren";
    [Tooltip("사이렌이 울린 뒤 몇 초 후에 발사할지 = 사출 연출이 이만큼 길어진다")]
    public float sirenLeadTime = 2f;
    [Tooltip("비행이 끝나고 몇 초 더 울리다가 끌지")]
    public float sirenTailTime = 1f;
    [Tooltip("사이렌 오브젝트를 Y축으로 돌리는 속도(도/초). 경광등처럼 빙글빙글. 0이면 안 돌림 (이미 애니메이션이 있으면 0)")]
    public float sirenSpinSpeed = 0f;

    [Tooltip("레버 당기기(우주인 동작) 시작 후 몇 초 뒤에 숫자가 돌기 시작할지. " +
             "레버가 '철컥' 내려가는 순간에 맞추면 된다. 보드와 홀로그램 스크램블이 같이 이 시간에 시작한다")]
    public float rollStartDelay = 1.65f;    // 레버가 끝까지 내려가는 순간 (손이 1.63초에 다 당김)

    [Header("레버 애니메이션 이벤트 (lever_pull 클립의 OnLeverPull 이벤트에 맞춰 레버가 움직임)")]
    [Tooltip("체크하면 Lever Delay / Lever Sound Delay / Roll Start Delay 대신 클립 이벤트를 쓴다")]
    public bool useLeverEvent = true;
    [Tooltip("레버가 움직이기 시작하고 몇 초 뒤에 숫자가 돌기 시작할지 (레버가 다 내려가는 시간 ≈ 0.6초)")]
    public float rollAfterLever = 0.6f;
    [Tooltip("이 시간(초) 안에 이벤트가 안 오면 레버를 그냥 움직이고 진행한다 (멈춤 방지)")]
    public float leverEventTimeout = 3f;

    [Header("게임 끝 (GameOver) — 비행이 끝나면 타임라인, 그 다음 EndGame 문구")]
    [Tooltip("처형 타임라인. Play On Awake 끄고 Wrap Mode는 None. 비워두면 바로 문구로 넘어간다")]
    public PlayableDirector endTimeline;

    // 승자 / 패자 화면을 따로 — 비워두면 위의 endTimeline을 쓴다
    [Tooltip("진 사람(사출된 사람) 화면에서 재생할 타임라인")]
    public PlayableDirector loseTimeline;
    [Tooltip("이긴 사람 화면에서 재생할 타임라인")]
    public PlayableDirector winTimeline;
    [Tooltip("타임라인 동안 실제 플레이어 모델(우주인)을 숨긴다. 컷신용 더미 우주인과 겹쳐 보이지 않게")]
    public bool hidePlayersDuringTimeline = true;

    [Tooltip("비행이 끝나고 타임라인 시작 전 잠깐 쉬는 시간(초)")]
    public float beforeTimelineDelay = 0.3f;

    [Tooltip("EndGame 문구 묶음 (평소엔 꺼둠). CrtPopup이 붙어 있으면 TV 켜지듯 나온다. 비워두면 홀로그램 메시지만 바뀜")]
    public GameObject endGameRoot;
    [Tooltip("승패 문구를 쓸 텍스트들 (3D TextMeshPro / UI TextMeshProUGUI 둘 다 가능)")]
    public TMP_Text[] endGameTexts;
    public string winText = "SURVIVOR CONFIRMED";
    public string loseText = "CREW MEMBER EJECTED";
    [Tooltip("EndGame 문구 색 (이겼을 때 / 졌을 때)")]
    public Color endWinColor = new Color(0.55f, 0.85f, 1f);
    public Color endLoseColor = new Color(1f, 0.35f, 0.3f);

    [Header("사운드 id (SoundManager의 Sounds 목록 이름) — 비우면 소리 없음")]
    // 소리는 전부 SoundManager가 관리한다. 여기엔 SoundManager 'Sounds' 목록의 id(이름)만 적는다
    [Tooltip("SoundManager의 Sounds 목록에 있는 id")]
    public string sndLever = "lever";
    public string sndScrambleTick = "scramble_tick";
    public string sndReveal = "reveal";
    public string sndOverload = "overload";
    public string sndCharge = "charge";
    public string sndEject = "eject";
    public string sndSafe = "safe";

    [Header("디버그")]
    public bool debugLog = false;

    Coroutine presentation;

    int pendingCard;
    int pendingRound;
    (Choice mine, Choice opp)? pendingReveal;
    (Result result, bool isMyTurn)? pendingDecision;
    bool? pendingOuted;
    (int before, int now)? pendingPer;

    int presserSeat = -1;                               // 이번 라운드에 누르는 사람 자리 (Open에서 정해짐)
    (int mine, int opp)? pendingCards;                  // 공개될 양쪽 카드 (EndTalk에서 도착 → 저장만)
    int shownPer;                                       // 화면에 보이는 기본 사출 확률 (OVERLOAD 계산 기준)
    bool overloadShown;                                 // 이번 Open에서 올라간 확률을 보여줬나
    Coroutine blinkRoutine;                             // 버튼 빨간불 깜빡임
    Light blinkingLight;
    Coroutine sirenSpinRoutine;                         // 사이렌 회전
    bool sirenOn;
    readonly List<GameObject> dummies = new List<GameObject>();
    readonly Dictionary<GameClient, bool> hiddenModels = new Dictionary<GameClient, bool>();

    // 의자째 사출 — 다음 라운드에 되돌리려고 기억해 두는 것들
    Coroutine flight;                                   // 날아가는 중인 코루틴
    GameClient flownClient;                             // 날아간 플레이어
    Transform flownChair;                               // 날아간 의자
    Vector3 chairStartPos;                              // 의자 원래 위치
    Quaternion chairStartRot;                           // 의자 원래 회전
    HeadLookSync flownLook;                             // 잠깐 꺼둔 시선 스크립트
    Animator flownAnim;                                 // 고개 쳐짐을 튼 Animator
    int flownSeat = -1;                                 // 날아간 자리 (불꽃용)

    // 레버 이벤트 상태 (이번 라운드에 이미 움직였는지)
    readonly bool[] leverMoved = new bool[2];
    bool leverSoundDone;
    bool leverEventArrived;

    // 게임 끝 연출
    bool? pendingWin;                                   // 내가 이겼나 (GameOver 직전에 도착 → 저장만)
    PlayableDirector playingTimeline;                   // 지금 재생 중인 타임라인 (승자/패자 중 하나)
    Coroutine gameOverRoutine;


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
        client.OnNumberSelected += HandleOpponentCard;
        client.OnCardsRevealed += HandleCardsRevealed;          // 카드 공개
        AstronautAnimEvents.LeverPulled += HandleLeverPulled;   // 레버 애니메이션 이벤트
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
        client.OnNumberSelected -= HandleOpponentCard;
        client.OnCardsRevealed -= HandleCardsRevealed;
        AstronautAnimEvents.LeverPulled -= HandleLeverPulled;
    }

    protected override void Refresh()
    {
        HandlePhase(client.curPhase);
    }

    protected override void OnClientLost()
    {
        StopPresentation();
        StopGameOver();
        ResetStage();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        StopPresentation();
        StopGameOver();
        ResetStage();
    }


    // ────────────────────────── 페이즈 ──────────────────────────

    void HandlePhase(GamePhase phase)
    {
        SetActiveTexts(perTexts, phase != GamePhase.waiting);
        SetActiveTexts(roundTexts, phase != GamePhase.waiting && phase != GamePhase.GameOver);

        if (phase != GamePhase.RoundStart && phase != GamePhase.Open && phase != GamePhase.Result)
            StopPresentation();

        // GameOver가 아니면 게임 끝 연출(타임라인/문구)도 끊고 숨긴다 (재시작, 방 대기 등)
        if (phase != GamePhase.GameOver)
            StopGameOver();

        switch (phase)
        {
            case GamePhase.waiting:
                ResetStage();                                   // 더미 제거 + 모델 복구
                ShowWaitingMessage();
                break;

            case GamePhase.RoundStart:
                ResetStage();                                   // GameOver → 재시작일 때 모델 복구
                StartPresentation(phase);
                break;

            case GamePhase.talkTime:
                SetMessage("", normalColor);
                break;

            case GamePhase.Open:
                StartPresentation(phase);
                break;

            case GamePhase.Press:
                // 내 차례인지에 따라 문구가 다르다
                bool myPress = client != null && presserSeat == client.myIndex.Value;
                SetMessage(myPress ? pressNowText : waitPressText, warnColor);
                ShowPresserMarker(presserSeat);                 // 혹시 꺼졌으면 다시
                break;

            case GamePhase.Result:
                StartPresentation(phase);
                break;

            case GamePhase.GameOver:
                StartGameOver();                                // 비행 끝 → 타임라인 → EndGame 문구
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
    // 값은 받자마자 화면에 쓰지 않고 저장만 한다. 공개는 연출 코루틴이 한다.

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

    void HandleOpponentCard(int card) => pendingCard = card;

    void HandleCardsRevealed(int mine, int opp) => pendingCards = (mine, opp);   // 저장만, 공개는 Open 연출

    /// <summary>
    /// 우주인 lever_pull 클립의 OnLeverPull 이벤트 → 그 자리 레버를 움직인다.
    /// 각자 화면에서 각자 우주인 애니메이션이 부르므로 네트워크와 상관없이 양쪽 다 맞는다.
    /// </summary>
    void HandleLeverPulled(int seat)
    {
        if (!useLeverEvent || client == null || client.curPhase != GamePhase.RoundStart) return;
        if (seat < 0 || seat >= 2 || seat >= leverAnimators.Length || leverMoved[seat]) return;

        leverMoved[seat] = true;
        leverEventArrived = true;
        Trigger(leverAnimators[seat], leverTrigger);

        if (!leverSoundDone) { leverSoundDone = true; Play(sndLever); }   // 두 자리가 동시라 소리는 한 번만
        Log($"레버 이벤트: 자리 {seat}");
    }


    void HandlePer(int before, int now)
    {
        pendingPer = (before, now);
    }

    void HandleChoiceRevealed(Choice mine, Choice opponent)
    {
        pendingReveal = (mine, opponent);
    }

    void HandleRoundDecided(Result result, bool isMyTurn)
    {
        pendingDecision = (result, isMyTurn);
    }

    void HandleOuted(bool outed)
    {
        pendingOuted = outed;
    }

    // 이제 GameOver에도 연출(타임라인)이 있으므로 저장만 하고, 타임라인이 끝난 뒤 보여준다
    void HandleGameWin(bool win)
    {
        pendingWin = win;
    }

    // ────────────────────────── 게임 끝 ──────────────────────────

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

        // 비행이 끝나고 조금 뒤 사이렌 끄기 (타임라인 시작 전)
        if (sirenTailTime > 0f) yield return new WaitForSeconds(sirenTailTime);
        SirenOff();

        if (beforeTimelineDelay > 0f)
            yield return new WaitForSeconds(beforeTimelineDelay);

        // 2) 처형 타임라인
        // 이겼으면 winTimeline, 졌으면 loseTimeline (없으면 endTimeline)
        bool win = pendingWin ?? false;
        PlayableDirector dir = win ? (winTimeline != null ? winTimeline : endTimeline)
                                   : (loseTimeline != null ? loseTimeline : endTimeline);
        playingTimeline = dir;

        if (dir != null)
        {
            Log($"처형 타임라인 시작 ({(win ? "승자" : "패자")} 화면: {dir.name})");

            // 진짜 플레이어 모델 숨기기 (컷신 더미와 겹치지 않게). 다음 라운드/대기에서 ResetStage가 되돌린다
            if (hidePlayersDuringTimeline)
                foreach (var c in FindObjectsByType<GameClient>(FindObjectsSortMode.None))
                    if (c != null && c.IsSpawned) SetModelVisible(c, false);

            dir.time = 0;
            dir.Play();

            // 길이만큼 기다린다. Wrap Mode가 Hold여도 멈추지 않고 넘어가도록 시간으로도 끊는다
            float endAt = Time.time + (float)dir.duration;
            yield return null;
            while (Time.time < endAt && dir.state == PlayState.Playing)
                yield return null;
        }

        // 3) EndGame 문구
        ShowEndGame(pendingWin ?? false);
        gameOverRoutine = null;
    }

    void ShowEndGame(bool win)
    {
        EndGameReady?.Invoke(win);                              // 다른 스크립트(GameResultText)에 알림
        string s = win ? winText : loseText;
        Color c = win ? endWinColor : endLoseColor;            // 문구 색을 따로

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

        // 실제로 틀었던 타임라인을 멈춘다
        if (playingTimeline != null && playingTimeline.state == PlayState.Playing)
            playingTimeline.Stop();
        playingTimeline = null;

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


    // ────────────────────────── 연출 ──────────────────────────


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

        // 우주인 동작을 먼저 시작하고, 손이 닿는 시점(leverDelay)에 레버가 움직인다
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
                if (!leverSoundDone) { leverSoundDone = true; Play(sndLever); }
            }

            // 레버가 다 내려갈 때까지 기다렸다가 숫자 시작
            yield return new WaitForSeconds(rollAfterLever);
        }
        else
        {
            // 시간 방식 (이벤트를 안 쓸 때)
            // Lever Delay가 음수면 = 레버를 먼저 출발시키고, 우주인을 그만큼 늦게 출발시킨다
            float astroWait = Mathf.Max(0f, -leverDelay);
            float leverWait = Mathf.Max(0f, leverDelay);

            if (animateAstronaut)
            {
                TriggerLater(GetAstronautAnimator(0), astronautPullTrigger, astroWait);
                TriggerLater(GetAstronautAnimator(1), astronautPullTrigger, astroWait);
            }
            foreach (var a in leverAnimators) TriggerLater(a, leverTrigger, leverWait);
            PlayLater(sndLever, astroWait + leverSoundDelay);
            yield return new WaitForSeconds(astroWait + rollStartDelay);
        }

        // 자리 보드(Player1/Player2 칸) 슬롯머신 시작 — 홀로그램 스크램블과 동시에 돈다
        if (seatBoard != null) seatBoard.PlayRoll(pendingCard);

        // 숫자 스크램블
        float t = 0f, tick = 0f;
        while (t < 0.8f || (seatBoard != null && seatBoard.IsRolling))   // 보드가 멈출 때까지 같이 돈다
        {
            t += Time.deltaTime;
            tick += Time.deltaTime;
            if (tick >= 0.08f)
            {
                tick = 0f;
                SetMessage($"OPPONENT PRIORITY  {Random.Range(1, 11)}", normalColor);
                Play(sndScrambleTick);
            }
            yield return null;
        }

        SetMessage($"OPPONENT PRIORITY  {pendingCard}", normalColor);
        Play(sndReveal);
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
            Play(sndReveal);
            yield return new WaitForSeconds(1.0f);

            if (mine == Choice.Overload || opp == Choice.Overload)
            {
                // 서버(GameSession.PressButton)와 똑같이 계산: 현재 확률 × 배율 → 반올림 → 0~100
                bool both = (mine == Choice.Overload && opp == Choice.Overload);
                var rule = GameManager.Instance != null ? GameManager.Instance.gameRule : null;
                float mul = rule != null ? (both ? rule.bothOverloadMul : rule.overloadMul) : (both ? 2f : 1.5f);
                int boosted = System.Math.Clamp((int)System.Math.Round(shownPer * mul), 0, 100);

                SetMessage($"SYSTEM OVERLOAD  x{mul:0.#}", warnColor);
                Trigger(alarmAnimator, alarmTrigger);
                Play(sndOverload);
                yield return new WaitForSeconds(0.5f);

                // 확률 숫자가 원래 값 → 올라간 값으로 차르르 올라간다
                int basePer = shownPer;
                // 괄호 안 숫자 색 (TMP 리치 텍스트용 #RRGGBB)
                string hex = ColorUtility.ToHtmlStringRGB(warnColor);
                float t = 0f;
                while (t < overloadCountTime)
                {
                    t += Time.deltaTime;
                    int v = Mathf.RoundToInt(Mathf.Lerp(basePer, boosted, t / overloadCountTime));
                    // "20%(30%)" — 원래 확률은 그대로, 괄호 안(증가된 확률)만 빨갛게 올라간다
                    SetTexts(perTexts, $"{basePer}%<color=#{hex}>({v}%)</color>");
                    SetPerColor(normalColor);
                    yield return null;
                }
                SetTexts(perTexts, $"{basePer}%<color=#{hex}>({boosted}%)</color>");
                SetMessage($"EJECTION RISK  {basePer}% -> {boosted}%", warnColor);
                overloadShown = true;
                yield return new WaitForSeconds(1.0f);
            }
        }

        bool folded = pendingDecision.HasValue && pendingDecision.Value.result == Result.fold;
        if (pendingCards.HasValue && (!folded || revealOnFold))
        {
            var (myCard, oppCard) = pendingCards.Value;

            SetMessage("PRIORITY CHECK", normalColor);
            if (seatBoard != null)
            {
                seatBoard.PlayReveal(myCard, oppCard);
                while (seatBoard.IsRolling) yield return null;      // 보드가 멈출 때까지
            }
            else yield return new WaitForSeconds(0.8f);

            // 숫자는 SeatBoard에서만 공개한다. 홀로그램은 안내 문구만
            Play(sndReveal);
            yield return new WaitForSeconds(1.0f);
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
                SetMessage(myTurn ? youPressText : oppPressText, warnColor);   // 더 알아보기 쉽게
                ShowPresserMarker(presserSeat);                                 // 누를 사람 자리 표시
                StartButtonBlink(presserSeat);                                  // 그 자리 버튼 빨간불 깜빡
            }
            yield return new WaitForSeconds(1.2f);
        }

        pendingReveal = null;
        pendingDecision = null;
        pendingCards = null;

        // OVERLOAD로 올려 보여줬는데 아무도 안 누르는 라운드(FOLD/동점)면 원래 확률로 되돌린다
        if (overloadShown && presserSeat < 0)
            ShowPer(shownPer, normalColor);
        overloadShown = false;
    }

    /// <summary>약 4초: 버튼 눌림 → 서스펜스 → 사출 or 생존 → 확률 변동</summary>
    IEnumerator ResultRoutine()
    {
        StopButtonBlink();                                      // 버튼이 눌렸으니 빨간불 끄기

        // 1) 누른 사람 자리의 버튼이 눌린다
        // 누른 사람 우주인이 버튼을 누르는 동작을 먼저 시작하고,
        //        손가락이 닿는 시점(buttonDelay)에 그 자리 버튼이 눌린다
        if (animateAstronaut && presserSeat >= 0)
            Trigger(GetAstronautAnimator(presserSeat), astronautPressTrigger);

        if (presserSeat >= 0 && presserSeat < pressButtonAnimators.Length)
            TriggerLater(pressButtonAnimators[presserSeat], pressTrigger, buttonDelay);

        SetMessage("EJECTION SYSTEM CHARGING...", warnColor);
        Play(sndCharge);
        yield return new WaitForSeconds(1.5f);

        // 2) 사고 여부 공개
        bool outed = pendingOuted ?? false;
        SetMessage(outed ? "EJECTION SYSTEM ACTIVATED" : "PRESSURE LOCK HELD",
                   outed ? warnColor : normalColor);

        if (outed)
        {
            SirenOn();
            if (sirenLeadTime > 0f)
            {
                SetMessage("WARNING : EJECTION IMMINENT", warnColor);
                yield return new WaitForSeconds(sirenLeadTime);
                SetMessage("EJECTION SYSTEM ACTIVATED", warnColor);
            }

            Play(sndEject);
            if (flyWithChair) StartEjectFlight(presserSeat);    // 의자째 위로 + 고개 쳐짐
            else { OpenHatch(presserSeat); Eject(presserSeat); }   // 체크 끄면 예전 더미 방식 (+ 천장 문)
        }
        else
        {
            Play(sndSafe);
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
        StopEjectFlight();                                  // 날아간 의자/플레이어 원위치
        ResetHatches();                                     // 천장 문 닫기

        foreach (var d in dummies) if (d != null) Destroy(d);
        dummies.Clear();

        foreach (var kv in hiddenModels)
            if (kv.Key != null) SetRenderers(kv.Key, true);
        hiddenModels.Clear();

        presserSeat = -1;
        ShowPresserMarker(-1);                              // 누를 차례 표시 끄기
        StopButtonBlink();                                  // 버튼 빨간불 끄기
        SirenOff();                                         // 사이렌 끄기
    }

    /// <summary>그 자리의 '누를 차례' 표시만 켜고 나머지는 끈다 (-1이면 전부 끔)</summary>
    void ShowPresserMarker(int seat)
    {
        if (presserMarkers == null) return;
        for (int i = 0; i < presserMarkers.Length; i++)
            if (presserMarkers[i] != null) presserMarkers[i].SetActive(i == seat);
    }

    // ────────────────────────── 의자째 사출 ──────────────────────────

    // 흐름: 덜덜 떨림(shakeTime) → 위로 발사(flyTime) + 고개 쳐짐 → 숨김
    // 각자 화면에서 똑같이 계산해서 움직인다 (네트워크로 위치를 주고받지 않음).
    // 떨림만 랜덤이라 양쪽 화면이 미세하게 다르지만, 날아가는 경로는 같다.

    /// <summary>그 자리의 플레이어와 의자를 같이 위로 날린다</summary>
    void StartEjectFlight(int seat)
    {
        Log($"사출 시작 요청: 자리 {seat}");                 // 로그
        if (seat < 0) return;
        StopEjectFlight();                                  // 혹시 이전 것이 남아 있으면 먼저 되돌림

        GameClient target = FindClientBySeat(seat);
        if (target == null) { Log($"의자째 사출 실패: 자리 {seat} 플레이어를 못 찾음"); return; }   // 로그

        flownClient = target;
        flownChair = (chairs != null && seat < chairs.Length) ? chairs[seat] : null;
        if (flownChair != null)
        {
            chairStartPos = flownChair.position;
            chairStartRot = flownChair.rotation;

            // Static으로 체크된 의자는 실행 중에 움직여도 화면에 안 움직인다 (Static Batching)
            if (flownChair.gameObject.isStatic)
                Debug.LogWarning($"[CenterHologram] 의자 '{flownChair.name}'가 Static이라 안 움직여 보일 수 있음 → Inspector 오른쪽 위 Static 체크 해제");
        }
        else Log($"의자 없음: Chairs[{seat}] 칸이 비어 있음 → 플레이어만 날아감");

        flownSeat = seat;                                   // 불꽃 파티클용

        // ★ 중요: HeadLookSync는 매 프레임(LateUpdate) 머리 본 회전을 덮어쓴다.
        //   켜둔 채로 두면 고개 쳐짐 애니메이션의 머리 움직임이 지워진다 → 잠깐 끈다.
        flownLook = target.GetComponentInChildren<HeadLookSync>();
        if (flownLook != null) flownLook.enabled = false;

        flownAnim = target.GetComponentInChildren<Animator>();

        // 아직 소비 안 된 트리거(Press/Pull)가 남아 있으면 날아가는 도중에 그 동작이 다시 나온다 → 지운다
        if (flownAnim != null)
        {
            if (!string.IsNullOrEmpty(astronautPressTrigger)) flownAnim.ResetTrigger(astronautPressTrigger);
            if (!string.IsNullOrEmpty(astronautPullTrigger)) flownAnim.ResetTrigger(astronautPullTrigger);
        }

        OpenHatch(seat);                                    // 천장 문이 열리면서 떨림 시작

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
        float shakeDuration = Mathf.Max(shakeTime, hatchOpenTime);
        float t = 0f;
        while (t < shakeDuration)
        {
            t += Time.deltaTime;
            Vector3 jitter = Random.insideUnitSphere * shakeAmount;
            Place(player, chair, pivot + jitter, Quaternion.identity, pivot, pStart, pRot, cStart, cRot);
            yield return null;
        }

        // 2) 발사 + 고개 쳐짐
        TriggerLater(flownAnim, slumpTrigger, slumpDelay);
        PlayThruster(flownSeat);                            // 의자 밑 불꽃 분사

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
        StopThrusters();                                    // 불꽃 끄기
        flownSeat = -1;

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

    /// <summary>그 자리 위 천장 문을 연다</summary>
    void OpenHatch(int seat)
    {
        if (hatchAnimators == null || seat < 0 || seat >= hatchAnimators.Length) return;
        Trigger(hatchAnimators[seat], hatchTrigger);
        Log($"천장 문 열기: 자리 {seat}");                    // 코드가 연 건지 확인용
    }

    /// <summary>천장 문을 전부 닫힌 처음 상태로 (Animator 기본 State = 닫힘이어야 함)</summary>
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

    void PlayThruster(int seat)
    {
        if (thrusterFx == null || seat < 0 || seat >= thrusterFx.Length)
        {
            Log($"불꽃 없음: Thruster Fx 칸 개수({(thrusterFx == null ? 0 : thrusterFx.Length)})가 모자라거나 자리({seat})가 이상함");
            return;
        }
        var fx = thrusterFx[seat];
        if (fx == null) { Log($"불꽃 없음: Thruster Fx[{seat}] 칸이 비어 있음"); return; }
        if (!fx.gameObject.activeSelf) fx.gameObject.SetActive(true);
        fx.Clear(true);
        fx.Play(true);                                      // true = 자식 파티클(연기, 불씨 등)도 같이
        Log($"불꽃: 자리 {seat}");
    }

    void StopThrusters()
    {
        if (thrusterFx == null) return;
        foreach (var fx in thrusterFx)
            if (fx != null) fx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    static void SetRenderersOf(Transform root, bool on)
    {
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            r.enabled = on;
    }

    /// <summary>내 컴퓨터에 있는 GameClient 중 그 자리 번호를 가진 것 (양쪽 플레이어 모두 존재한다)</summary>
    static GameClient FindClientBySeat(int seat)
    {
        foreach (var c in FindObjectsByType<GameClient>(FindObjectsSortMode.None))
            if (c.IsSpawned && c.myIndex.Value == seat) return c;
        return null;
    }

    /// <summary>
    /// 그 자리 우주인의 Animator.
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

        public void ShowCheatPer(int per)
    {
        ShowPer(per, warnColor);
        SetMessage($"OVERRIDE  EJECTION RISK {per}%", warnColor);

        // Result 연출 도중이면 곧 표시될 확률(서버가 치트 전에 보낸 값)도 바꿔 둔다 (화면이 옛 값으로 돌아가지 않게)
        if (pendingPer.HasValue) pendingPer = (pendingPer.Value.before, per);
    }

    void SetMessage(string s, Color c)
    {
        SetTexts(messageTexts, s);
        foreach (var t in messageTexts)
            if (t != null) t.color = c;
    }

    // 확률 표시 (글자 + 색)
    void ShowPer(int per, Color c)
    {
        shownPer = per;                                     // 지금 화면의 기본 확률 기억 (OVERLOAD 계산용)
        SetTexts(perTexts, $"{per}%");
        SetPerColor(c);
    }

    void SetPerColor(Color c)
    {
        if (perTexts == null) return;
        foreach (var t in perTexts)
            if (t != null) t.color = c;
    }

    void SirenOn()
    {
        if (sirenOn) return;
        sirenOn = true;

        if (sirenObjects != null)
            foreach (var o in sirenObjects) if (o != null) o.SetActive(true);

        SoundManager.PlayLoop(sndSiren);                    // 끌 때까지 반복

        if (sirenSpinSpeed != 0f)
            sirenSpinRoutine = StartCoroutine(SirenSpin());

        Log("사이렌 ON");
    }

    IEnumerator SirenSpin()
    {
        while (true)
        {
            foreach (var o in sirenObjects)
                if (o != null) o.transform.Rotate(0f, sirenSpinSpeed * Time.deltaTime, 0f, Space.Self);
            yield return null;
        }
    }

    void SirenOff()
    {
        if (sirenSpinRoutine != null) { StopCoroutine(sirenSpinRoutine); sirenSpinRoutine = null; }

        if (sirenObjects != null)
            foreach (var o in sirenObjects) if (o != null) o.SetActive(false);

        SoundManager.StopLoop(sndSiren);

        if (sirenOn) Log("사이렌 OFF");
        sirenOn = false;
    }

    void StartButtonBlink(int seat)
    {
        StopButtonBlink();
        if (pressButtonLights == null || seat < 0 || seat >= pressButtonLights.Length) { Log($"빨간불: 자리 {seat} 없음"); return; }
        Light l = pressButtonLights[seat];
        if (l == null) { Log($"빨간불: Press Button Lights[{seat}] 비어 있음"); return; }
        blinkRoutine = StartCoroutine(BlinkRoutine(l));
    }

    IEnumerator BlinkRoutine(Light l)
    {
        blinkingLight = l;
        l.color = blinkColor;
        if (!l.gameObject.activeSelf) l.gameObject.SetActive(true);   // 오브젝트째 꺼 둔 경우도 켜서 깜빡이게
        Log($"버튼 빨간불 깜빡 시작: {l.name}");
        while (true)
        {
            l.enabled = true;
            yield return new WaitForSeconds(blinkOnTime);
            l.enabled = false;
            yield return new WaitForSeconds(blinkOffTime);
        }
    }

    void StopButtonBlink()
    {
        if (blinkRoutine != null) { StopCoroutine(blinkRoutine); blinkRoutine = null; }
        if (blinkingLight != null) blinkingLight.enabled = false;
        blinkingLight = null;

        // 혹시 켜진 채로 남은 불도 다 끈다
        if (pressButtonLights != null)
            foreach (var l in pressButtonLights) if (l != null) l.enabled = false;
    }

    static string ChoiceLabel(Choice c) =>
        c == Choice.Go ? "GO" : c == Choice.Fold ? "FOLD" : "OVERLOAD";

    // Animator가 비어 있거나 꺼져 있으면 조용히 넘어간다
    static void Trigger(Animator a, string trigger)
    {
        if (a == null || !a.isActiveAndEnabled || string.IsNullOrEmpty(trigger)) return;
        a.SetTrigger(trigger);
    }

    // delay초 뒤에 트리거 (0이면 바로). 연출 흐름(대기 시간)은 막지 않는다
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

    // delay초 뒤에 소리 (0이면 바로)
    void PlayLater(string id, float delay)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (delay <= 0f) { Play(id); return; }
        StartCoroutine(PlayAfter(id, delay));
    }

    IEnumerator PlayAfter(string id, float delay)
    {
        yield return new WaitForSeconds(delay);
        Play(id);
    }

    // 모든 소리는 SoundManager로
    void Play(string id)
    {
        if (!string.IsNullOrEmpty(id)) SoundManager.Play(id);
    }

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