using System;
using UnityEngine;

public class GameClient : MonoBehaviour
{
    public GameManager gameManager;
    public int myIndex;
    public int foldLeft;
    public GamePhase curPhase;

    public event Action<GamePhase> OnChangedPhase;//페이즈 변경시
    public event Action<int> OnRoundChanged;//라운드 변경시
    public event Action<int> OnNumberSelected;
    public event Action<float> OnTimeChanged;
    public event Action<bool, bool> OnFoldSelected;
    public event Action<Result, bool> OnRoundDecided;
    public event Action<bool> OnOuted;//사출됬나
    public event Action<int, int> OnPerChanged;
    public event Action<bool> OnGameWin;
    public event Action<int> OnMyFoldLeftChanged;//폴드 값

    public void ReceiveMyFoldLeft(int foldLeft)
    {
        OnMyFoldLeftChanged?.Invoke(foldLeft);
    }
    public void ReceivePhase(GamePhase phase)
    {
        curPhase = phase;
        OnChangedPhase?.Invoke(phase);
    }

    public void ReceiveRound(int round)
    {
        OnRoundChanged?.Invoke(round);
    }

    public void ReceiveOpponentCard(int card)
    {
        OnNumberSelected?.Invoke(card);
    }

    public void ReceiveTimer(float remainTime)
    {
        OnTimeChanged?.Invoke(remainTime);
    }

    public void ReceiveFoldResult(bool myFold, bool opponentFold)
    {
        OnFoldSelected?.Invoke(myFold, opponentFold);
    }

    public void ReceiveRoundResult(Result result, bool isMyTurn)
    {
        OnRoundDecided?.Invoke(result, isMyTurn);
    }

    public void ReceiveOutResult(bool isOuted)
    {
        OnOuted?.Invoke(isOuted);
    }

    public void ReceivePer(int beforePer, int curPer)
    {
        OnPerChanged?.Invoke(beforePer, curPer);
    }

    public void ReceiveGameOver(bool isWin)
    {
        OnGameWin?.Invoke(isWin);
    }

    public void RequestFold() { gameManager.RequestFold(myIndex); }
    public void RequestPress() { gameManager.RequestPress(myIndex); }

    /// <summary>
    /// 연출끝
    /// </summary>
    public void EndPresentation() { gameManager.NotifyPresentationEnd(myIndex, curPhase); }
}
