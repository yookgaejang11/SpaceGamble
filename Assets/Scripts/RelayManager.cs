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

    const int MaxConnections = 1;
    const string ConnectionType = "dtls";

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
        await InitAsync();

        var allocation = await RelayService.Instance.CreateAllocationAsync(MaxConnections);

        NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, ConnectionType));

        string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

        NetworkManager.Singleton.StartHost();

        return joinCode;

    }

    public async Task<bool> JoinRoom(string joinCode)
    {

        await InitAsync();

        var allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

        NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, ConnectionType));

        return NetworkManager.Singleton.StartClient();
         
    }
}
