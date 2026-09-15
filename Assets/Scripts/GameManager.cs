using System;
using System.Collections.Generic;
using UnityEngine;

public enum Result
{
    p1Win,
    p2Win,
    draw,
    fold
}
public enum GamePhase
{
    RoundStart,   // 카드 배정, 상대 숫자 공개
    talkTime,   // 대화 타이머, Fold 선택
    Open,       // Fold 여부 공개, 누를 사람 공개
    Press,        // 버튼 누르기 대기
    Result,       // 사고 여부, 확률 변동 공개
    GameOver      // 승패 화면
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

    public bool TryFold(int playerNum)
    {
        if (foldCounts[playerNum] <maxFold && !isFold[playerNum])
        {
            foldCounts[playerNum] += 1;
            isFold[playerNum] = true;
            return true;
        }
        return false;
    }

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


public class GameManager : MonoBehaviour
{
    public GamePer SOGameRule;
    public GamePer gameRule;
    public GameClient[] clients = new GameClient[2];

    GameSession session;
    public GamePhase curPhase;
    float talkTimer;

    private void Start()
    {
        gameRule = Instantiate(SOGameRule);
    }

    public void StartGame()
    {
        session = new GameSession();
        session.curOutPer = gameRule.basicPer;
        BeginRound();
    }

    public void BeginRound()
    {
        session.StartRound();
       
        clients[0].ReceiveOpponentCard(session.cardNums[1]);
        clients[1].ReceiveOpponentCard(session.cardNums[0]);

        clients[0].ReceiveRound(session.curRound);
        clients[1].ReceiveRound(session.curRound);

        clients[0].ReceiveMyFoldLeft(session.maxFold-session.foldCounts[0]);
        clients[1].ReceiveMyFoldLeft(session.maxFold-session.foldCounts[1]);
        ChangePhase(GamePhase.RoundStart);
    }

    public void ChangePhase(GamePhase phase)
    {
        curPhase = phase;
        clients[0].ReceivePhase(phase);
        clients[1].ReceivePhase(phase);
    }

    void BeginTalk()
    {
        talkTimer = gameRule.talkingTime;
        ChangePhase(GamePhase.talkTime);
    }

    void EndTalk()
    {
        clients[0].ReceiveFoldResult(session.isFold[0], session.isFold[1]);
        clients[1].ReceiveFoldResult(session.isFold[1], session.isFold[0]);

        Result r = session.DecideResult();

        clients[0].ReceiveRoundResult(r, session.pressedPlayer == 0);
        clients[1].ReceiveRoundResult(r, session.pressedPlayer == 1);

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
            clients[0].ReceiveGameOver(session.winnerId == 0);
            clients[1].ReceiveGameOver(session.winnerId == 1);
            ChangePhase(GamePhase.GameOver);
        }
        else
        {
            BeginRound();
        }
    }

    void Update()
    {
        if (curPhase != GamePhase.talkTime) return;

        talkTimer -= Time.deltaTime;

        clients[0].ReceiveTimer(talkTimer);
        clients[1].ReceiveTimer(talkTimer);

        if (talkTimer <= 0f)
            EndTalk();
    }


    public void RequestFold(int playerIndex)
    {
        if (curPhase != GamePhase.talkTime) return;

        if (session.TryFold(playerIndex))
            clients[playerIndex].ReceiveMyFoldLeft(session.maxFold - session.foldCounts[playerIndex]);
    }

    public void RequestPress(int playerIndex)
    {
        if (curPhase != GamePhase.Press) return;
        if (playerIndex != session.pressedPlayer) return;

        int before = session.curOutPer;
        session.PressButton(gameRule);

        clients[0].ReceiveOutResult(session.isGameOver);
        clients[1].ReceiveOutResult(session.isGameOver);

        clients[0].ReceivePer(before, session.curOutPer);
        clients[1].ReceivePer(before, session.curOutPer);

        ChangePhase(GamePhase.Result);
    }

    public void NotifyPresentationEnd(int playerIndex, GamePhase phase)
    {
        if (curPhase != phase) return;

        switch (phase)
        {
            case GamePhase.RoundStart: BeginTalk(); break;
            case GamePhase.Open: AfterOpen(); break;
            case GamePhase.Result: AfterResult(); break;
        }
    }
}

