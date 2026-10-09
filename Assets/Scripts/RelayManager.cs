using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using UnityEngine;

public class RelayManager : MonoBehaviour
{
    public static RelayManager Instance { get; private set; }

    const int MaxConnections = 1;           // Host 제외 인원
    const string ConnectionType = "dtls";

    public bool isInRoom;
    public string joinCode { get; private set; }

    public event Action<string> OnRoomCodeSpawned;      // 방 코드 생성
    public event Action OnRoomConnectSuccessed;         // 접속 성공
    public event Action<string> OnRoomConnectFailed;    // 접속 실패 (사유)
    public event Action<string> OnRoomQuited;           // 방 나감 (사유)

    void Awake()
    {
        Instance = this;
    }

    async Task InitAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    public async Task<string> CreateRoom()
    {
        try
        {
            isInRoom = true;
            await InitAsync();

            var allocation = await RelayService.Instance.CreateAllocationAsync(MaxConnections);

            NetworkManager.Singleton.GetComponent<UnityTransport>()
                .SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, ConnectionType));

            joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            NetworkManager.Singleton.OnClientDisconnectCallback += OnDisconnected;

            if (!NetworkManager.Singleton.StartHost())
                throw new Exception("StartHost failed");

            OnRoomCodeSpawned?.Invoke(joinCode);
            OnRoomConnectSuccessed?.Invoke();
            return joinCode;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Relay] CreateRoom 실패: {e}");
            LeaveRoom(null);                                    // 조용히 정리만
            OnRoomConnectFailed?.Invoke("방 만들기에 실패했습니다.");
            return null;
        }
    }

    public async Task<bool> JoinRoom(string code)
    {
        try
        {
            isInRoom = true;
            await InitAsync();

            var allocation = await RelayService.Instance.JoinAllocationAsync(code);

            NetworkManager.Singleton.GetComponent<UnityTransport>()
                .SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, ConnectionType));

            NetworkManager.Singleton.OnClientDisconnectCallback += OnDisconnected;

            bool started = NetworkManager.Singleton.StartClient();
            if (!started)
                throw new Exception("StartClient failed");

            joinCode = code;
            OnRoomConnectSuccessed?.Invoke();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Relay] JoinRoom 실패: {e}");
            LeaveRoom(null);
            OnRoomConnectFailed?.Invoke("방에 들어가지 못했습니다. 코드를 확인해주세요.");
            return false;
        }
    }

    /// <summary>
    /// 방을 떠난다. 나가기 버튼 / 호스트 끊김 / 접속 실패가 모두 여기로 모인다.
    /// reason이 null이면 이벤트를 쏘지 않는다 (실패 쪽에서 따로 알리는 경우).
    /// </summary>
    public void LeaveRoom(string reason)
    {
        if (!isInRoom) return;
        isInRoom = false;                       // Shutdown보다 먼저 내려야 재진입이 막힌다

        joinCode = null;

        if (reason != null)
            OnRoomQuited?.Invoke(reason);

        if (NetworkManager.Singleton == null) return;

        NetworkManager.Singleton.OnClientDisconnectCallback -= OnDisconnected;

        // 콜백 처리 중에 Shutdown하면 NGO 내부가 꼬일 수 있어 한 프레임 미룬다
        CancelInvoke(nameof(DoShutdown));
        Invoke(nameof(DoShutdown), 0f);
    }

    void OnDisconnected(ulong clientId)
    {
        // 서버(Host) 쪽 처리는 GameManager가 한다. 여기는 클라이언트 전용.
        if (NetworkManager.Singleton.IsServer) return;

        LeaveRoom("호스트와의 연결이 끊어졌습니다.");
    }

    void DoShutdown()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.Shutdown();
    }
}
