using System;
using UnityEngine;

[Serializable]
public class LobbyPlayerData
{
    public ulong clientId;
    public string nickname;
    public string characterId;
    public bool isReady;
    public bool isHost;

    public LobbyPlayerData(ulong CliendId, string Nickname, bool IsHost)
    {
        clientId = CliendId;
        nickname = Nickname;
        isHost = IsHost;
    }
}


public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance { get; private set; }

    [SerializeField]
    LobbyUI lobbyUI;
    [SerializeField]
    



    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
