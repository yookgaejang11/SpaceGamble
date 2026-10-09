using System;
using UnityEngine;
using Unity.Netcode;

public enum Result
{
    p1Win,      // 1번 플레이어가 누름
    p2Win,      // 0번 플레이어가 누름
    draw,       // 동점 — 아무도 안 누름
    fold        // 한 명이라도 Fold — 아무도 안 누름
}

public enum GamePhase
{
    waiting,      // 플레이어 대기 / 준비(F4)
    RoundStart,   // 카드 배정, 상대 숫자 공개
    talkTime,     // 대화 타이머, 선택
    Open,         // 선택 공개, 누를 사람 공개
    Press,        // 버튼 누르기 대기
    Result,       // 사고 여부, 확률 변동 공개
    GameOver,     // 승패 화면 / 재시작 준비(F4)
}

/// <summary>
/// 플레이어가 라운드마다 하는 선택.
/// </summary>
public enum Choice
{
    Go,
    Fold,
    Overload
}


[Serializable]
public class GameSession
{
    public int curOutPer;                               // 현재 사출확률 (누적)

    public int winnerId = -1;                           // 게임 승자 번호(0,1)

    public int pressedPlayer;                           // 눌러야 하는 사람

    public int[] cardNums = new int[2];                 // 뽑은 카드 번호

    public int[] foldCounts = new int[2];               // 사용한 Fold 횟수

    public int maxFold = 2;                             // 최대 fold 횟수

    public bool isGameOver;                             // 게임 끝났는지

    public int curRound = 0;                            // 현재 라운드

    public Choice[] choices = new Choice[2];            // 이번 라운드 선택

    public float lastOverloadMul = 1f;                  // 직전 판정에 적용된 배율 (연출/디버그용)

    System.Random rand = new System.Random();

    public void StartRound()
    {
        pressedPlayer = -1;
        cardNums[0] = rand.Next(1, 11);
        cardNums[1] = rand.Next(1, 11);

        curRound += 1;

        choices[0] = Choice.Go;
        choices[1] = Choice.Go;
    }

    public Result DecideResult()
    {
        if (choices[0] == Choice.Fold || choices[1] == Choice.Fold)
        {
            return Result.fold;
        }
        else if (cardNums[0] > cardNums[1])
        {
            pressedPlayer = 1;
            return Result.p1Win;
        }
        else if (cardNums[1] > cardNums[0])
        {
            pressedPlayer = 0;
            return Result.p2Win;
        }
        else
        {
            return Result.draw;
        }
    }

    public bool CanFold(int playerNum) => foldCounts[playerNum] < maxFold;

    /// <summary>
    /// 이번 라운드에 적용될 사고 확률 배율.
    /// OVERLOAD는 누가 걸었든 양쪽 모두에게 적용된다.
    /// </summary>
    public float GetOverloadMul(GamePer gamePer)
    {
        bool a = choices[0] == Choice.Overload;
        bool b = choices[1] == Choice.Overload;

        if (a && b) return gamePer.bothOverloadMul;
        if (a || b) return gamePer.overloadMul;
        return 1f;
    }

    public void PressButton(GamePer gamePer)
    {
        if (pressedPlayer == -1) { return; }

        lastOverloadMul = GetOverloadMul(gamePer);

        // 배율은 이번 판정에만 쓰고 curOutPer 자체는 건드리지 않는다 (누적 아님)
        int effectivePer = (int)Math.Round(curOutPer * lastOverloadMul);
        effectivePer = Math.Clamp(effectivePer, 0, 100);

        int outPer = rand.Next(1, 101);
        if (outPer <= effectivePer)
        {
            winnerId = (pressedPlayer == 0) ? 1 : 0;
            isGameOver = true;
        }
        else
        {
            int upDownPer = rand.Next(1, 101);

            if (upDownPer <= gamePer.extraPer)
            {
                curOutPer -= gamePer.extraVal;
            }
            else if (upDownPer <= gamePer.upPer + gamePer.extraPer)
            {
                curOutPer += rand.Next(gamePer.upMinVal, gamePer.upMaxVal + 1);
            }
            else
            {
                curOutPer -= rand.Next(gamePer.downMinVal, gamePer.downMaxVal + 1);
            }
            curOutPer = Math.Clamp(curOutPer, 0, 100);
        }
    }
}


public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("규칙")]
    public GamePer SOGameRule;
    public GamePer gameRule;

    [Header("좌석 (0번 / 1번)")]
    public Transform[] seats = new Transform[2];

    [Header("런타임 상태")]
    public GameClient[] clients = new GameClient[2];
    public bool[] isPresentationEnded = new bool[2];
    public bool[] isReady = new bool[2];
    public GamePhase curPhase;

    GameSession session;
    float talkTimer;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        gameRule = Instantiate(SOGameRule);
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        foreach (var id in NetworkManager.Singleton.ConnectedClientsIds)
            OnClientConnected(id);
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton == null) return;

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }


    // ────────────────────────── 접속 / 끊김 ──────────────────────────

    void OnClientConnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId)) return;

        var netObj = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject;
        if (netObj == null) return;

        var client = netObj.GetComponent<GameClient>();
        if (client == null) return;

        // 이미 등록된 오브젝트면 무시
        if (clients[0] == client || clients[1] == client) return;

        // 자리가 없으면 무시
        if (clients[0] != null && clients[1] != null) return;

        int index = (clients[0] == null) ? 0 : 1;
        clients[index] = client;
        client.myIndex.Value = index;

        ChangePhase(GamePhase.waiting);
    }

    void OnClientDisconnected(ulong clientId)
    {
        // 이 콜백은 서버에만 등록되어 있다 (클라 쪽 복귀는 RelayManager 담당)
        if (clients[0] != null && clients[0].OwnerClientId == clientId)
            clients[0] = null;

        if (clients[1] != null && clients[1].OwnerClientId == clientId)
            clients[1] = null;

        CancelInvoke(nameof(AfterPresentationEnd));
        session = null;
        ChangePhase(GamePhase.waiting);
    }


    // ────────────────────────── 게임 흐름 ──────────────────────────

    public void StartGame()
    {
        session = new GameSession();
        session.curOutPer = gameRule.basicPer;
        BeginRound();
    }

    public void BeginRound()
    {
        session.StartRound();

        clients[0].ReceiveOpponentCardRpc(session.cardNums[1], clients[0].ToMe);
        clients[1].ReceiveOpponentCardRpc(session.cardNums[0], clients[1].ToMe);

        clients[0].ReceiveRoundRpc(session.curRound, clients[0].ToMe);
        clients[1].ReceiveRoundRpc(session.curRound, clients[1].ToMe);

        clients[0].ReceiveMyFoldLeftRpc(session.maxFold - session.foldCounts[0], clients[0].ToMe);
        clients[1].ReceiveMyFoldLeftRpc(session.maxFold - session.foldCounts[1], clients[1].ToMe);

        ChangePhase(GamePhase.RoundStart);
    }

    public void ChangePhase(GamePhase phase)
    {
        isReady[0] = false;
        isReady[1] = false;

        curPhase = phase;

        bool readyPhase = (phase == GamePhase.waiting || phase == GamePhase.GameOver);

        for (int i = 0; i < 2; i++)
        {
            if (clients[i] == null) continue;

            if (readyPhase)
                clients[i].ReceiveReadyRpc(false, clients[i].ToMe);

            clients[i].ReceivePhaseRpc(phase, clients[i].ToMe);
        }

        isPresentationEnded[0] = false;
        isPresentationEnded[1] = false;

        CancelInvoke(nameof(AfterPresentationEnd));
        if (phase == GamePhase.RoundStart || phase == GamePhase.Open || phase == GamePhase.Result)
            Invoke(nameof(AfterPresentationEnd), gameRule.cutSceneTime);
    }

    void BeginTalk()
    {
        talkTimer = gameRule.talkingTime;

        clients[0].ReceiveTimerRpc(talkTimer, clients[0].ToMe);
        clients[1].ReceiveTimerRpc(talkTimer, clients[1].ToMe);

        ChangePhase(GamePhase.talkTime);
    }

    void EndTalk()
    {
        if (curPhase != GamePhase.talkTime) return;

        // Fold를 실제로 선택한 사람만 횟수 차감
        for (int i = 0; i < 2; i++)
        {
            if (session.choices[i] == Choice.Fold)
            {
                session.foldCounts[i] += 1;
                clients[i].ReceiveMyFoldLeftRpc(session.maxFold - session.foldCounts[i], clients[i].ToMe);
            }
        }

        // 선택 공개 — 시점 변환 (내 것, 상대 것)
        clients[0].ReceiveChoiceRevealedRpc(session.choices[0], session.choices[1], clients[0].ToMe);
        clients[1].ReceiveChoiceRevealedRpc(session.choices[1], session.choices[0], clients[1].ToMe);

        Result r = session.DecideResult();

        clients[0].ReceiveRoundResultRpc(r, session.pressedPlayer == 0, clients[0].ToMe);
        clients[1].ReceiveRoundResultRpc(r, session.pressedPlayer == 1, clients[1].ToMe);

        ChangePhase(GamePhase.Open);
    }

    void AfterOpen()
    {
        if (session.pressedPlayer == -1)
            BeginRound();                       // fold 또는 draw
        else
            ChangePhase(GamePhase.Press);
    }

    void AfterResult()
    {
        if (session.isGameOver)
        {
            clients[0].ReceiveGameOverRpc(session.winnerId == 0, clients[0].ToMe);
            clients[1].ReceiveGameOverRpc(session.winnerId == 1, clients[1].ToMe);
            ChangePhase(GamePhase.GameOver);
        }
        else
        {
            BeginRound();
        }
    }

    void Update()
    {
        if (!IsServer) return;
        if (curPhase != GamePhase.talkTime) return;

        talkTimer -= Time.deltaTime;

        if (talkTimer <= 0f)
            EndTalk();
    }


    // ────────────────────────── 클라이언트 요청 접수 ──────────────────────────

    public void RequestGo(int playerIndex)
    {
        if (curPhase != GamePhase.talkTime) return;
        if (session == null) return;

        session.choices[playerIndex] = Choice.Go;
    }

    public void RequestFold(int playerIndex)
    {
        if (curPhase != GamePhase.talkTime) return;
        if (session == null) return;

        // 횟수가 남아있을 때만 Fold. 없으면 Go로 떨어진다.
        // 실제 차감은 EndTalk에서 (대화 중 언제든 바꿀 수 있으므로)
        session.choices[playerIndex] = session.CanFold(playerIndex) ? Choice.Fold : Choice.Go;
    }

    public void RequestOverload(int playerIndex)
    {
        if (curPhase != GamePhase.talkTime) return;
        if (session == null) return;

        session.choices[playerIndex] = Choice.Overload;
    }

    public void RequestReady(int playerIndex)
    {
        if (curPhase != GamePhase.waiting && curPhase != GamePhase.GameOver) return;

        // 혼자 있을 때는 준비해도 시작되지 않는다
        if (clients[0] == null || clients[1] == null) return;

        isReady[playerIndex] = !isReady[playerIndex];

        clients[0].ReceiveReadyRpc(isReady[0], clients[0].ToMe);
        clients[1].ReceiveReadyRpc(isReady[1], clients[1].ToMe);

        if (isReady[0] && isReady[1])
            StartGame();
    }

    public void RequestPress(int playerIndex)
    {
        if (curPhase != GamePhase.Press) return;
        if (session == null) return;
        if (playerIndex != session.pressedPlayer) return;

        int before = session.curOutPer;
        session.PressButton(gameRule);

        clients[0].ReceiveOutResultRpc(session.isGameOver, clients[0].ToMe);
        clients[1].ReceiveOutResultRpc(session.isGameOver, clients[1].ToMe);

        clients[0].ReceivePerRpc(before, session.curOutPer, clients[0].ToMe);
        clients[1].ReceivePerRpc(before, session.curOutPer, clients[1].ToMe);

        ChangePhase(GamePhase.Result);
    }

    public void NotifyPresentationEnd(int playerIndex, GamePhase phase)
    {
        if (curPhase != phase) return;

        isPresentationEnded[playerIndex] = true;

        if (isPresentationEnded[0] && isPresentationEnded[1])
            AfterPresentationEnd();
    }

    /// <summary>
    /// 연출이 끝났을 때(양쪽 신호) 또는 타임아웃일 때 호출된다.
    /// Invoke로도 불리므로 파라미터가 없어야 한다.
    /// </summary>
    void AfterPresentationEnd()
    {
        CancelInvoke(nameof(AfterPresentationEnd));

        switch (curPhase)
        {
            case GamePhase.RoundStart: BeginTalk(); break;
            case GamePhase.Open: AfterOpen(); break;
            case GamePhase.Result: AfterResult(); break;
        }
    }


    // ────────────────────────── 디버그 ──────────────────────────

    void OnGUI()
    {
        if (!IsServer) return;
        if (session == null) return;

        GUILayout.BeginArea(new Rect(470, 0, 260, 320));
        GUILayout.Label("=== SERVER ===");
        GUILayout.Label($"phase {curPhase}   round {session.curRound}");
        GUILayout.Label($"ready : {isReady[0]} / {isReady[1]}");
        GUILayout.Label($"cards : {session.cardNums[0]} / {session.cardNums[1]}");
        GUILayout.Label($"choice: {session.choices[0]} / {session.choices[1]}");
        GUILayout.Label($"per   : {session.curOutPer}%  (x{session.GetOverloadMul(gameRule):0.0})");
        GUILayout.Label($"presser: {session.pressedPlayer}");
        GUILayout.Label($"timer : {talkTimer:F1}");
        GUILayout.EndArea();
    }
}
