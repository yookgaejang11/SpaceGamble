using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class RelayManager : MonoBehaviour
{

    public static RelayManager Instance { get; private set; }
    public bool isInRoom;
    const int MaxConnections = 1;
    const string ConnectionType = "dtls";

    public event Action<string> OnRoomCodeSpawned;
    public event Action OnRoomConnectSuccessed;
    public event Action<string> OnRoomConnectFailed;
    public event Action<string> OnRoomQuited;

    private void Awake()
    {
        Instance = this;
    }

    async Task InitAsync()
    {
        if(UnityServices.State != ServicesInitializationState.Initialized)
        {
            await UnityServices.InitializeAsync();
        }

        if(!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }

    public async Task<string> CreateRoom()
    {
        try
        {
            isInRoom = true;
            await InitAsync();

            var allocation = await RelayService.Instance.CreateAllocationAsync(MaxConnections);

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, ConnectionType));

            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            NetworkManager.Singleton.StartHost();

            NetworkManager.Singleton.OnClientDisconnectCallback += OnDisconnected;

            OnRoomCodeSpawned?.Invoke(joinCode);
            OnRoomConnectSuccessed?.Invoke();
            return joinCode;
        }
        catch(Exception e)
        {
            Debug.Log(e);
            LeaveRoom("방 만들기에 실패했습니다.");
            OnRoomConnectFailed?.Invoke("방만들기에 실패했습니다!");
            return null;
        }

    }

    public async Task<bool> JoinRoom(string joinCode)
    {
        try
        {
            isInRoom = true;

            await InitAsync();

            var allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, ConnectionType));
            NetworkManager.Singleton.OnClientDisconnectCallback += OnDisconnected;
            bool clientStarted = NetworkManager.Singleton.StartClient();
            OnRoomConnectSuccessed?.Invoke();
            return clientStarted;
        }
        catch(Exception e)
        {
            Debug.Log(e);
            LeaveRoom("가입에 실패했습니다");
            OnRoomConnectFailed?.Invoke("가입에 실패했습니다");
            return false;
        }
         
    }

    public void LeaveRoom(string reason)
    {
        if (!isInRoom) return;
        isInRoom = false;
        
        OnRoomQuited?.Invoke(reason);

        if (NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnDisconnected;
        Invoke(nameof(DoShutdown), 0f);

        
    }

    void OnDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton.IsServer) return;

            LeaveRoom("호스트와의 연결이 끊어졌습니다.");
    }

    void DoShutdown()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.Shutdown();
    }
}
