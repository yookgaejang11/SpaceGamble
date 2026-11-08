using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using UnityEngine;
using Unity.Services.Relay.Models;
using Unity.Services.Vivox;

public class RelayManager : MonoBehaviour
{
    public static RelayManager Instance { get; private set; }

    const int MaxConnections = 1;
    const string ConnectionType = "dtls";

    public bool isInRoom;
    public string joinCode { get; private set; }

    public event Action<string> OnRoomCodeSpawned;
    public event Action OnRoomConnectSuccessed;
    public event Action<string> OnRoomConnectFailed;
    public event Action<string> OnRoomQuited;

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

        await VivoxService.Instance.InitializeAsync();
    }

    public async Task<string> CreateRoom()
    {
        try
        {
            CleanupNetworkManager();

            if (isInRoom) return null;
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
            Debug.LogError($"[Relay] CreateRoom 실패: {e.Message}");
            ForceResetState();
            OnRoomConnectFailed?.Invoke("방 만들기에 실패했습니다.");
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return null;
        }
    }

    public async Task<bool> JoinRoom(string code)
    {
        try
        {
            // 방 참가 시도 직전 잔여 소켓 정리
            CleanupNetworkManager();

            if (isInRoom) return false;
            isInRoom = true;

            await InitAsync();

            // 호스트가 나가서 삭제된 코드라면 여기서 예외 발생
            var allocation = await RelayService.Instance.JoinAllocationAsync(code);

            NetworkManager.Singleton.GetComponent<UnityTransport>()
                .SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, ConnectionType));

            NetworkManager.Singleton.OnClientDisconnectCallback += OnDisconnected;

            bool started = NetworkManager.Singleton.StartClient();
            if (!started)
                throw new Exception("StartClient failed");

            //타임아웃처리
            float timeoutSeconds = 5f;
            float elapsed = 0f;

            while (!NetworkManager.Singleton.IsConnectedClient && elapsed < timeoutSeconds)
            {
                // 중간에 Client가 튕겼다면 바로 탈출
                if (!NetworkManager.Singleton.IsClient) break;

                await Task.Delay(100);
                elapsed += 0.1f;
            }

            // timeout 되면 연결 강제 취소
            if (!NetworkManager.Singleton.IsConnectedClient)
            {
                throw new Exception("Connection Timeout");
            }

            joinCode = code;
            OnRoomConnectSuccessed?.Invoke();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Relay] JoinRoom 실패: {e.Message}");
            ForceResetState();
            OnRoomConnectFailed?.Invoke("방에 들어가지 못했습니다. 코드를 확인하거나 호스트 상태를 확인해주세요.");
            return false;
        }
    }

    // 소켓 및 콜백 동기 즉시 초기화
    private void CleanupNetworkManager()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnDisconnected;
            if (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsConnectedClient)
            {
                NetworkManager.Singleton.Shutdown();
            }
        }
    }

    // 실패 시 상태 리셋
    private void ForceResetState()
    {
        isInRoom = false;
        joinCode = null;
        CleanupNetworkManager();
    }

    public void LeaveRoom(string reason)
    {
        if (!isInRoom) return;

        isInRoom = false;
        joinCode = null;

        CleanupNetworkManager();

        if (reason != null)
            OnRoomQuited?.Invoke(reason);
    }

    void OnDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer) return;
        LeaveRoom("호스트와의 연결이 끊어졌습니다.");
    }
}