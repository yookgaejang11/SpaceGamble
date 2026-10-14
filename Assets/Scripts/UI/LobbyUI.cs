using System;
using TMPro;
using UnityEngine;
using UnityEngine.Assertions.Must;
using UnityEngine.UI;
using UnityEngine.Windows;

public class LobbyUI : MonoBehaviour
{
    [SerializeField] GameObject LobbyUi;
    [SerializeField] GameObject InGameUI;
    [SerializeField] Button CreateBtn;
    [SerializeField] Button JoinBtn;
    public TMP_InputField joinCode;
    public async void JoinRoom()
    {
        await RelayManager.Instance.JoinRoom(joinCode.text);
        JoinBtn.interactable = false;
    }

    public async void CreateRoomID()
    {
        await RelayManager.Instance.CreateRoom();
        Debug.Log("\"Room Code:\" " + RelayManager.Instance.joinCode);
    }


    private void Update()
    {
        if(RelayManager.Instance.isInRoom)
        {
            CreateBtn.interactable = false;
            JoinBtn.interactable = false;
            if(LobbyUi != null)
            {
                LobbyUi.SetActive(false);
            }
            if(InGameUI != null)
            {
                InGameUI.SetActive(true);
            }
        }
        else
        {
            CreateBtn.interactable = true;
            JoinBtn.interactable = true;
            if (LobbyUi != null)
            {
                LobbyUi.SetActive(true);
            }
            if (InGameUI != null)
            {
                InGameUI.SetActive(false);
            }
        }
    }


}
