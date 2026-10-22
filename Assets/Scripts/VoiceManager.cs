using System;
using System.Threading;
using Unity.Services.Vivox;
using Unity.VisualScripting;
using UnityEngine;

public class VoiceManager : MonoBehaviour
{
    public static VoiceManager Instance { get; private set; }

    public string channelPrefix = "SG_";//채널 이름 앞에 붙을거(다른 게임이랑 안섞이게)

    public bool voiceTestEcho = false;

    string joinedChannel;
    public bool IsMuted { get; private set; }

    void Awake() => Instance = this;


    private void Start()
    {
        if (RelayManager.Instance == null) return;
        RelayManager.Instance.OnRoomConnectSuccessed += HandleJoined;
        RelayManager.Instance.OnRoomQuited += HandleQuit;
    }

    private void OnDestroy()
    {
        if (RelayManager.Instance == null) return;
        RelayManager.Instance.OnRoomConnectSuccessed -= HandleJoined;
        RelayManager.Instance.OnRoomQuited -= HandleQuit;
    }

    void HandleJoined() => _ = ConnectAsync();
    void HandleQuit(string reason) => _ = DisconnectAsync();

    async System.Threading.Tasks.Task ConnectAsync()
    {
        try
        {
            string code = RelayManager.Instance.joinCode;
            if (string.IsNullOrEmpty(code)) return;
            if(!VivoxService.Instance.IsLoggedIn) 
            {
                var options = new LoginOptions();
                options.DisplayName = "CREW";
                await VivoxService.Instance.LoginAsync(options);
            }

            joinedChannel = channelPrefix + code;
            if(voiceTestEcho)
                await VivoxService.Instance.JoinEchoChannelAsync(joinedChannel, ChatCapability.AudioOnly);
            else
                await VivoxService.Instance.JoinGroupChannelAsync(joinedChannel, ChatCapability.AudioOnly);
        }
        catch (Exception e)
        {
            //음성이 안되도 디버그만 남기고 접속
            Debug.LogWarning($"음챗 접속 실패{e.Message}");
            joinedChannel = null;
        }
    }

    async System.Threading.Tasks.Task DisconnectAsync()
    {
        try
        {
            if (joinedChannel != null)
                await VivoxService.Instance.LeaveChannelAsync(joinedChannel);
            joinedChannel = null;

            if (VivoxService.Instance.IsLoggedIn)
                await VivoxService.Instance.LoginAsync();
        }
        catch(Exception e) { Debug.LogWarning($"종료 실패{e.Message}"); }
    }

    public void ToggleMute()
    {
        IsMuted = !IsMuted;
        if(IsMuted) VivoxService.Instance.MuteInputDevice();
        else VivoxService.Instance.UnmuteInputDevice();
    }


  
}
