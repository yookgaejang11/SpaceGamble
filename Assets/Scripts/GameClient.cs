using System;
using UnityEngine;
using Unity.Netcode;
public class GameClient : NetworkBehaviour
{
    public GameManager gameManager;
    public int myIndex;
    public int foldLeft;
    public GamePhase curPhase;
    public bool mySelection;
    public float curTime;
    public bool isReady;
    public event Action<bool> OnReadyPressed;// 레디 눌렀는지(임시 키 : f4)
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
    public event Action<bool> OnMySelectionChanged;//go,fold 바뀜
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

    private void Update()
    {
        if (!IsOwner) return;
        if(curPhase == GamePhase.talkTime)
        {
            curTime -= Time.deltaTime;
            curTime = Mathf.Max(curTime, 0);
            OnTimeChanged?.Invoke(Mathf.Max(0f, curTime));
        }

        if(curPhase == GamePhase.waiting || curPhase == GamePhase.GameOver)
        {
            if(Input.GetKeyDown(KeyCode.F4))
                RequestReady();
        }
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
    public void ReceiveReadyRpc(bool ready, RpcParams parms)
    {
        isReady = ready;
        OnReadyPressed?.Invoke(isReady);
    }


    [Rpc(SendTo.SpecifiedInParams)]
    public void ReceiveRoundRpc(int round, RpcParams parms)
    {
        mySelection = false;
        OnMySelectionChanged?.Invoke(false);
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
        curTime = remainTime;
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


    public void RequestGo()
    {
        if (!IsOwner) return;
        mySelection = false;
        OnMySelectionChanged?.Invoke(false);
        RequestToGoRpc();
    }

    [Rpc(SendTo.Server)]
    void RequestToGoRpc()
    {
        gameManager.RequestGo(myIndex);
    }

    public void RequestReady()
    {
        if (!IsOwner) return;
        isReady = !isReady;
        OnReadyPressed?.Invoke(isReady);
        RequestReadyRpc();
    }

    [Rpc(SendTo.Server)]
    void RequestReadyRpc()
    {
        gameManager.RequestReady(myIndex);
    }


    public void RequestFold() 
    {
        if (!IsOwner) return;
        if (foldLeft <= 0) return;      // 로컬 차단
        mySelection = true;
        OnMySelectionChanged?.Invoke(true);
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
        if (!IsOwner) return;
        GUILayout.BeginArea(new Rect(myIndex * 220, 0, 210, 300));
        GUILayout.Label($"상대 카드: {opponentCard}");
        GUILayout.Label($"P{myIndex}  phase: {curPhase}");
        GUILayout.Label($"isReady: {isReady}");
        GUILayout.Label($"fold left: {foldLeft}");
        GUILayout.Label($"talkTime:{curTime}");
        if (GUILayout.Button("GO")) RequestGo();
        if (GUILayout.Button("Fold")) RequestFold();
        if (GUILayout.Button("Press")) RequestPress();
        if (GUILayout.Button("연출끝")) EndPresentation();
        GUILayout.EndArea();
    }
}
