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
    SelectTime,   // 대화 타이머, Fold 선택
    OpenFold,       // Fold 여부 공개, 누를 사람 공개
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
    GamePhase curPhase;
    float discussionTimer;
}

