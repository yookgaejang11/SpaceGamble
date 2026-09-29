using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
public enum Result
{
    p1Win,
    p2Win,
    draw,
    fold
}
public enum GamePhase
{
    waiting,     //플레이어 기다리는중
    RoundStart,   // 카드 배정, 상대 숫자 공개
    talkTime,   // 대화 타이머, Fold 선택
    Open,       // Fold 여부 공개, 누를 사람 공개
    Press,        // 버튼 누르기 대기
    Result,       // 사고 여부, 확률 변동 공개
    GameOver,   // 승패 화면
}

[Serializable]
public class GameSession
{

    public int curOutPer;                 //현재 사출확률

    public int winnerId = -1;                    //게임 승자 번호(0,1)

    public int pressedPlayer;                  //눌러야 하는 사람

    public int[] cardNums = new int[2];     //뽑은 카드 번호

    public int[] foldCounts = new int[2];   //포기 횟수

    public int maxFold = 2;                 //최대 fold 횟수

    public bool isGameOver;                 //게임끝났는지

    public int curRound = 0;                    //현재 라운드

    public bool[] isFold = new bool[2];     //포기 여부

    System.Random rand = new System.Random();

    public void StartRound()
    {
        pressedPlayer = -1;
        cardNums[0] = rand.Next(1, 11);
        cardNums[1] = rand.Next(1, 11);

        curRound += 1;

        isFold[0] = false;
        isFold[1] = false;
    }

    public Result DecideResult()
    {
        if (isFold[0] || isFold[1]) { return Result.fold; }
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

    public void PressButton(GamePer gamePer)
    {
        if(pressedPlayer == -1) { return; }

        int outPer = rand.Next(1,101);
        if(outPer <= curOutPer)
        {
            if(pressedPlayer == 0)
            {
                winnerId = 1;
            }
            else
            {
                winnerId = 0;
            }
            isGameOver = true;
        }
        else
        {
            int upDownPer = rand.Next(1, 101);

            if(upDownPer <= gamePer.extraPer)
            {
                curOutPer -= gamePer.extraVal;
            }
            else if(upDownPer <= gamePer.upPer + gamePer.extraPer)
            {
                curOutPer += rand.Next(gamePer.upMinVal, gamePer.upMaxVal+1);
            }
            else
            {
                curOutPer -= rand.Next(gamePer.downMinVal, gamePer.downMaxVal+1);
            }
            curOutPer = Math.Clamp(curOutPer, 0, 100);
        }
    }

}


public class GameManager : NetworkBehaviour
{
    public GamePer SOGameRule;
    public GamePer gameRule;
    public GameClient[] clients = new GameClient[2];
    public bool[] isPresentationEnded = new bool[2];
    public bool[] isReady = new bool[2];
    GameSession session;
    public GamePhase curPhase;
    float talkTimer;
    public static GameManager Instance { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        gameRule = Instantiate(SOGameRule);

    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

        foreach (var id in NetworkManager.Singleton.ConnectedClientsIds)
            OnClientConnected(id);
    }

    void OnClientConnected(ulong clientId)
    {
        var netObj = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject;
        if (netObj == null) return;

        var client = netObj.GetComponent<GameClient>();

        // 이미 등록된 오브젝트면 무시
        if (clients[0] == client || clients[1] == client) return;

        // 자리가 없으면 무시
        if (clients[0] != null && clients[1] != null) return;

        int index = (clients[0] == null) ? 0 : 1;
        clients[index] = client;
        client.myIndex = index;
        client.SetIndexRpc(index, client.ToMe);

    }

    public void StartGame()
    {
        Debug.Log("adsf");
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

        clients[0].ReceiveMyFoldLeftRpc(session.maxFold-session.foldCounts[0], clients[0].ToMe);
        clients[1].ReceiveMyFoldLeftRpc(session.maxFold-session.foldCounts[1], clients[1].ToMe);
        ChangePhase(GamePhase.RoundStart);
    }

    public void ChangePhase(GamePhase phase)
    {
        isReady[0] = false;
        isReady[1] = false;
        
        curPhase = phase;

        if (clients[0] != null)
        {
            if (phase == GamePhase.waiting || phase == GamePhase.GameOver)
                clients[0].ReceiveReadyRpc(isReady[0], clients[0].ToMe);
            clients[0].ReceivePhaseRpc(phase, clients[0].ToMe);
        }
        if (clients[1] != null)
        {
            if (phase == GamePhase.waiting || phase == GamePhase.GameOver)
                clients[1].ReceiveReadyRpc(isReady[1], clients[1].ToMe);
            clients[1].ReceivePhaseRpc(phase, clients[1].ToMe);
        }
        isPresentationEnded[0] = false;
        isPresentationEnded[1] = false;
        CancelInvoke(nameof(AfterPresentationEnd));
        if(phase == GamePhase.RoundStart || phase == GamePhase.Open || phase == GamePhase.Result)
            Invoke(nameof(AfterPresentationEnd),gameRule.cutSceneTime);
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
        for(int i = 0; i< 2; i++)
        {
            if (session.isFold[i])
            {
                session.foldCounts[i] += 1;
                clients[i].ReceiveMyFoldLeftRpc(session.maxFold - session.foldCounts[i], clients[i].ToMe);
            }
        }

        clients[0].ReceiveFoldResultRpc(session.isFold[0], session.isFold[1], clients[0].ToMe);
        clients[1].ReceiveFoldResultRpc(session.isFold[1], session.isFold[0], clients[1].ToMe);

        Result r = session.DecideResult();

        clients[0].ReceiveRoundResultRpc(r, session.pressedPlayer == 0, clients[0].ToMe);
        clients[1].ReceiveRoundResultRpc(r, session.pressedPlayer == 1, clients[1].ToMe);

        ChangePhase(GamePhase.Open);
    }

    void AfterOpen()
    {
        if (session.pressedPlayer == -1)
            BeginRound();               // fold 또는 draw
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

    public void RequestGo(int playerIndex)
    {
        if (curPhase != GamePhase.talkTime) return;
        session.isFold[playerIndex] = false;
    }

    public void RequestFold(int playerIndex)
    {
        if (curPhase != GamePhase.talkTime) return;

        session.isFold[playerIndex] = session.CanFold(playerIndex);//시간 끝나고 폴드 감소 되게(대화하는동안 언제든지 바꿀 수 있게끔)
    }

    public void RequestReady(int playerIndex)
    {
        if (curPhase != GamePhase.waiting && curPhase != GamePhase.GameOver) return;
        isReady[playerIndex] = !isReady[playerIndex];
        Debug.Log(isReady[0]  + " " + isReady[1]);
        if (clients[0] != null)
            clients[0].ReceiveReadyRpc(isReady[0], clients[0].ToMe);
        if (clients[1] != null)
            clients[1].ReceiveReadyRpc(isReady[1], clients[1].ToMe);
        if (isReady[0] && isReady[1])
        {
            StartGame();
        }
    }
    

    public void RequestPress(int playerIndex)
    {
        Debug.Log($"도착: {playerIndex}, phase={curPhase}, presser={session.pressedPlayer}");
        if (curPhase != GamePhase.Press) return;
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
        {
            AfterPresentationEnd();
        }
    }

    void AfterPresentationEnd()
    {
      
        switch (curPhase)
        {
            case GamePhase.RoundStart: BeginTalk(); break;
            case GamePhase.Open: AfterOpen(); break;
            case GamePhase.Result: AfterResult(); break;
        }
    }

    void OnGUI()
    {
        if (curPhase == GamePhase.waiting) return;
        GUILayout.BeginArea(new Rect(460, 0, 250, 300));
        GUILayout.Label("=== SERVER ===");
        GUILayout.Label($"isReady : {isReady[0]} {isReady[1]}");
        GUILayout.Label($"round {session.curRound}  phase {curPhase}");
        GUILayout.Label($"cards: {session.cardNums[0]} / {session.cardNums[1]}");
        GUILayout.Label($"per: {session.curOutPer}%");
        GUILayout.Label($"presser: {session.pressedPlayer}");
        GUILayout.Label($"timer: {talkTimer:F1}");
        GUILayout.EndArea();
    }
}

