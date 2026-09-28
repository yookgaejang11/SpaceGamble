using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Windows;

public class LobbyUI : MonoBehaviour
{

    public Text roomCode;
    public InputField JoinCode;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public async void CreateRoomID()
    {
        roomCode.text = await RelayManager.Instance.CreateRoom();
    }


}
