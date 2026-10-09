using System;
using UnityEngine;
using Unity.Netcode;

public class GameClient : NetworkBehaviour
{
    // ────────────────────────── 상태 (UI가 읽어도 되는 값) ──────────────────────────

    public NetworkVariable<int> myIndex = new NetworkVariable<int>();

    public int foldLeft;            // 내 남은 Fold 횟수
    public GamePhase curPhase;      // 현재 페이즈
    public Choice myChoice;         // 내 현재 선택
    public float curTime;           // 대화 남은 시간 (로컬 계산)
    public bool isReady;            // 내 준비 상태
    public int opponentCard;        // 상대 카드


    // ────────────────────────── UI가 구독할 이벤트 ──────────────────────────

    public event Action<GamePhase> OnChangedPhase;          // 페이즈 변경
    public event Action<int> OnRoundChanged;                // 라운드 변경
    public event Action<int> OnNumberSelected;              // 상대 카드
    public event Action<float> OnTimeChanged;               // 남은 시간
    public event Action<Choice> OnMyChoiceChanged;          // 내 선택 변경 (로컬 즉시)
    public event Action<Choice, Choice> OnChoiceRevealed;   // 선택 공개 (내 것, 상대 것)
    public event Action<Result, bool> OnRoundDecided;       // 라운드 판정 (결과, 내가 누르나)
    public event Action<bool> OnOuted;                      // 사출됐나
    public event Action<int, int> OnPerChanged;             // 확률 (이전, 현재)
    public event Action<bool> OnGameWin;                    // 내가 이겼나
    public event Action<int> OnMyFoldLeftChanged;           // 내 Fold 잔여
    public event Action<bool> OnReadyPressed;               // 내 준비 상태


    GameManager gameManager;

    public RpcParams ToMe => RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp);


    // ────────────────────────── 생명주기 ──────────────────────────

    public override void OnNetworkSpawn()
    {
        gameManager = GameManager.Instance;

        myIndex.OnValueChanged += HandleIndexChanged;

        // 스폰 시점에 이미 값이 와 있을 수 있으므로 한 번 적용
        MoveSeat();
    }

    public override void OnNetworkDespawn()
    {
        myIndex.OnValueChanged -= HandleIndexChanged;
    }

    void HandleIndexChanged(int before, int after)
    {
        MoveSeat();
    }

    /// <summary>
    /// 내 번호에 맞는 의자로 이동. 여러 번 불려도 안전해야 한다.
    /// </summary>
    public void MoveSeat()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        if (gm.seats == null || gm.seats.Length < 2) return;

        var seat = gm.seats[Mathf.Clamp(myIndex.Value, 0, 1)];
        if (seat == null) return;

        transform.SetPositionAndRotation(seat.position, seat.rotation);
    }

    void Update()
    {
        if (!IsOwner) return;

        if (curPhase == GamePhase.talkTime)
        {
            curTime = Mathf.Max(curTime - Time.deltaTime, 0f);
            OnTimeChanged?.Invoke(curTime);
        }

        if (curPhase == GamePhase.waiting || curPhase == GamePhase.GameOver)
        {
            if (Input.GetKeyDown(KeyCode.F4))
                RequestReady();
        }
    }


    // ────────────────────────── 서버 → 나 (타겟 RPC) ──────────────────────────

    [Rpc(SendTo.SpecifiedInParams)]
    public void ReceivePhaseRpc(GamePhase phase, RpcParams parms)
    {
        curPhase = phase;

        // 새 페이즈로 넘어가면 지난 라운드 선택 표시를 지운다
        if (phase == GamePhase.RoundStart || phase == GamePhase.waiting)
        {
            myChoice = Choice.Go;
            OnMyChoiceChanged?.Invoke(myChoice);
        }

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
        myChoice = Choice.Go;
        OnMyChoiceChanged?.Invoke(myChoice);
        OnRoundChanged?.Invoke(round);
    }

    [Rpc(SendTo.SpecifiedInParams)]
    public void ReceiveOpponentCardRpc(int card, RpcParams parms)
    {
        opponentCard = card;
        OnNumberSelected?.Invoke(card);
    }

    [Rpc(SendTo.SpecifiedInParams)]
    public void ReceiveMyFoldLeftRpc(int left, RpcParams parms)
    {
        foldLeft = left;
        OnMyFoldLeftChanged?.Invoke(left);
    }

    [Rpc(SendTo.SpecifiedInParams)]
    public void ReceiveTimerRpc(float startTime, RpcParams parms)
    {
        curTime = startTime;
        OnTimeChanged?.Invoke(curTime);
    }

    [Rpc(SendTo.SpecifiedInParams)]
    public void ReceiveChoiceRevealedRpc(Choice mine, Choice opponent, RpcParams parms)
    {
        myChoice = mine;
        OnChoiceRevealed?.Invoke(mine, opponent);
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


    // ────────────────────────── 나 → 서버 (UI가 호출) ──────────────────────────

    public void RequestGo()
    {
        if (!IsOwner) return;
        if (curPhase != GamePhase.talkTime) return;

        SetChoiceLocal(Choice.Go);
        RequestGoRpc();
    }

    public void RequestFold()
    {
        if (!IsOwner) return;
        if (curPhase != GamePhase.talkTime) return;
        if (foldLeft <= 0) return;              // 로컬 차단

        SetChoiceLocal(Choice.Fold);
        RequestFoldRpc();
    }

    public void RequestOverload()
    {
        if (!IsOwner) return;
        if (curPhase != GamePhase.talkTime) return;

        SetChoiceLocal(Choice.Overload);
        RequestOverloadRpc();
    }

    public void RequestReady()
    {
        if (!IsOwner) return;
        RequestReadyRpc();
    }

    public void RequestPress()
    {
        if (!IsOwner) return;
        RequestPressRpc();
    }

    /// <summary>연출이 끝났을 때 UI가 호출</summary>
    public void EndPresentation()
    {
        if (!IsOwner) return;
        EndPresentationRpc(curPhase);
    }

    /// <summary>서버 응답을 기다리지 않고 화면에 먼저 반영한다</summary>
    void SetChoiceLocal(Choice c)
    {
        myChoice = c;
        OnMyChoiceChanged?.Invoke(c);
    }


    [Rpc(SendTo.Server)]
    void RequestGoRpc() { gameManager.RequestGo(myIndex.Value); }

    [Rpc(SendTo.Server)]
    void RequestFoldRpc() { gameManager.RequestFold(myIndex.Value); }

    [Rpc(SendTo.Server)]
    void RequestOverloadRpc() { gameManager.RequestOverload(myIndex.Value); }

    [Rpc(SendTo.Server)]
    void RequestReadyRpc() { gameManager.RequestReady(myIndex.Value); }

    [Rpc(SendTo.Server)]
    void RequestPressRpc() { gameManager.RequestPress(myIndex.Value); }

    [Rpc(SendTo.Server)]
    void EndPresentationRpc(GamePhase phase) { gameManager.NotifyPresentationEnd(myIndex.Value, phase); }


    // ────────────────────────── 임시 디버그 UI ──────────────────────────

    void OnGUI()
    {
        if (!IsOwner) return;

        GUILayout.BeginArea(new Rect(10, 10, 220, 340));
        GUILayout.Label($"P{myIndex.Value}   {curPhase}");
        GUILayout.Label($"상대 카드 : {opponentCard}");
        GUILayout.Label($"내 선택   : {myChoice}");
        GUILayout.Label($"fold left : {foldLeft}");
        GUILayout.Label($"ready     : {isReady}");
        GUILayout.Label($"time      : {curTime:F1}");

        if (GUILayout.Button("GO")) RequestGo();
        if (GUILayout.Button("FOLD")) RequestFold();
        if (GUILayout.Button("OVERLOAD")) RequestOverload();
        if (GUILayout.Button("PRESS")) RequestPress();
        if (GUILayout.Button("연출끝")) EndPresentation();
        if (GUILayout.Button("READY (F4)")) RequestReady();
        GUILayout.EndArea();
    }
}
