using System;
using UnityEngine;
using UnityEngine.Assertions.Must;
using UnityEngine.UI;
using UnityEngine.Windows;

public class LobbyUI : MonoBehaviour
{
    [SerializeField] Button CreateBtn;
    [SerializeField] Button JoinBtn;
    public Text roomCode;
    public InputField joinCode;
    public async void JoinRoom()
    {
        await RelayManager.Instance.JoinRoom(joinCode.text);
        JoinBtn.interactable = false;
    }

    public async void CreateRoomID()
    {
        roomCode.text = "Room Code:" + await RelayManager.Instance.CreateRoom();
    }


    private void Update()
    {
        if(RelayManager.Instance.isInRoom)
        {
            CreateBtn.interactable = false;
            JoinBtn.interactable = false;
        }
        else
        {
            JoinBtn.interactable = true;
        }
    }


}
