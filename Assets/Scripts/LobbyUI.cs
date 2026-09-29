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
        try
        {
            await RelayManager.Instance.JoinRoom(joinCode.text);
            JoinBtn.interactable = false;
        }
        catch
        {
            //참가 실패 메시지 출력(방 코드 틀리면 틀렸다고 UI 출력)
            Debug.Log("참가에 실패했습니다.");
            JoinBtn.interactable = true;
        }
    }

    public async void CreateRoomID()
    {
        try
        {
            roomCode.text = "Room Code:" + await RelayManager.Instance.CreateRoom();
            CreateBtn.interactable = false;
        }
        catch
        {
            //만들기 실패 메시지 출력
            Debug.Log("방만들기에 실패했습니다!");
            CreateBtn.interactable = true;
        }
    }


}
