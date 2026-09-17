using System;
using UnityEngine;
using Unity.Netcode;
public class GameClient : NetworkBehaviour
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
    public int opponentCard; //숫자

    public override void OnNetworkSpawn()
    {
        gameManager = GameManager.Instance;
    }

    public RpcParams ToMe => RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp);

    [Rpc(SendTo.SpecifiedInParams)]
    public void SetIndexRpc(int index, RpcParams parms)
    {
        myIndex = index;
    }

 

    [Rpc(SendTo.SpecifiedInParams)]
    public void ReceiveMyFoldLeftRpc(int foldLeft, RpcParams parms)
    {
        this.foldLeft = foldLeft;
        OnMyFoldLeftChanged?.Invoke(foldLeft);
    }

    [Rpc(SendTo.SpecifiedInParams)]
    public void ReceivePhaseRpc(GamePhase phase, RpcParams parms)
    {
        curPhase = phase;
        OnChangedPhase?.Invoke(phase);
    }

    [Rpc(SendTo.SpecifiedInParams)]
    public void ReceiveRoundRpc(int round, RpcParams parms)
    {
        OnRoundChanged?.Invoke(round);
    }

    [Rpc(SendTo.SpecifiedInParams)]
    public void ReceiveOpponentCardRpc(int card, RpcParams parms)
    {
        OnNumberSelected?.Invoke(card);
        opponentCard = card;
    }

    [Rpc(SendTo.SpecifiedInParams)]
    public void ReceiveTimerRpc(float remainTime, RpcParams parms)
    {
        OnTimeChanged?.Invoke(remainTime);
    }

    [Rpc(SendTo.SpecifiedInParams)]
    public void ReceiveFoldResultRpc(bool myFold, bool opponentFold, RpcParams parms)
    {
        OnFoldSelected?.Invoke(myFold, opponentFold);
    }

    [Rpc(SendTo.SpecifiedInParams)]
    public void ReceiveRoundResultRpc(Result result, bool isMyTurn, RpcParams parms)
    {
        OnRoundDecided?.Invoke(result, isMyTurn);
    }

    [Rpc(SendTo.SpecifiedInParams)]
    public void ReceiveOutResultRpc(bool isOuted, RpcParams parms)
    {
        OnOuted?.Invoke(isOuted);
    }

    [Rpc(SendTo.SpecifiedInParams)]
    public void ReceivePerRpc(int beforePer, int curPer, RpcParams parms)
    {
        OnPerChanged?.Invoke(beforePer, curPer);
    }

    [Rpc(SendTo.SpecifiedInParams)]
    public void ReceiveGameOverRpc(bool isWin, RpcParams parms)
    {
        OnGameWin?.Invoke(isWin);
    }

    
    public void RequestFold() 
    {
        if (!IsOwner) return;
        RequestToFoldRpc();
    }

    [Rpc(SendTo.Server)]
    void RequestToFoldRpc()
    {
        gameManager.RequestFold(myIndex);
    }

    public void RequestPress()
    {
        if (!IsOwner) return;
        RequestToPressRpc();
    }

    [Rpc(SendTo.Server)]
    void RequestToPressRpc() { gameManager.RequestPress(myIndex); }


    public void EndPresentation()
    {
        if (!IsOwner) return;
        EndPresentationRpc(curPhase);
    }

    [Rpc(SendTo.Server)]
    void EndPresentationRpc(GamePhase phase) { gameManager.NotifyPresentationEnd(myIndex, phase); }

    void OnGUI()
    {
        if(!IsOwner) return;
        GUILayout.BeginArea(new Rect(myIndex * 220, 0, 210, 300));
        GUILayout.Label($"상대 카드: {opponentCard}");
        GUILayout.Label($"P{myIndex}  phase: {curPhase}");
        GUILayout.Label($"fold left: {foldLeft}");
        if (GUILayout.Button("Fold")) RequestFold();
        if (GUILayout.Button("Press")) RequestPress();
        if (GUILayout.Button("연출끝")) EndPresentation();
        GUILayout.EndArea();
    }
}
