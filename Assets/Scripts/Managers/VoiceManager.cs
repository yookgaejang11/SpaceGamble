using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Vivox;
using UnityEngine;

/// <summary>
/// 음성 채팅 전체 관리자.
///
///   방에 들어가면 채널 접속 / 나가면 끊기
///   오픈 마이크 ↔ 눌러서 말하기 전환
///   마이크 입력 볼륨, 상대 목소리 볼륨, 마이크 장치 선택
///   누가 말하고 있는지 (아이콘 표시용)
///
/// 붙이는 곳
///   씬에 빈 오브젝트 "VoiceManager"를 만들고 붙인다. 항상 켜져 있어야 한다.
///
/// 먼저 할 일
///   RelayManager.InitAsync() 안에 한 줄 추가:
///       await VivoxService.Instance.InitializeAsync();
/// </summary>
public class VoiceManager : MonoBehaviour
{
    public static VoiceManager Instance { get; private set; }

    public enum MicMode { OpenMic, PushToTalk }

    [Header("기본값 (설정 창에서 바꾸면 저장된다)")]
    public MicMode micMode = MicMode.OpenMic;
    public KeyCode pushToTalkKey = KeyCode.V;

    [Tooltip("마이크 입력 볼륨 (-50 ~ 50)")]
    [Range(-50, 50)] public int inputVolume = 0;

    [Tooltip("상대 목소리 볼륨 (-50 ~ 50)")]
    [Range(-50, 50)] public int opponentVolume = 0;

    [Header("채널")]
    [Tooltip("채널 이름 앞에 붙일 글자 (다른 게임과 안 섞이게)")]
    public string channelPrefix = "sg_";

    [Header("디버그")]
    public bool debugLog = false;


    // ────────────────────────── 상태 (UI가 읽는 값) ──────────────────────────

    /// <summary>음성 채팅을 쓰는가 (끄면 채널에 아예 안 들어간다)</summary>
    public bool VoiceEnabled { get; private set; } = true;

    /// <summary>Vivox 초기화가 끝났는가. 장치 목록은 이때부터 읽을 수 있다 (방에 안 들어가도 됨)</summary>
    public bool VivoxReady { get; private set; }

    /// <summary>음성 채널에 들어가 있는가</summary>
    public bool IsConnected { get; private set; }

    /// <summary>내가 지금 말하고 있는가</summary>
    public bool SelfSpeaking { get; private set; }

    /// <summary>상대가 지금 말하고 있는가</summary>
    public bool OpponentSpeaking { get; private set; }

    /// <summary>내 입력 레벨 0~1 (레벨 미터용)</summary>
    public float SelfEnergy { get; private set; }

    /// <summary>상대 목소리 레벨 0~1</summary>
    public float OpponentEnergy { get; private set; }

    /// <summary>내가 음소거했는가 (설정에서 끈 것)</summary>
    public bool SelfMuted { get; private set; }

    /// <summary>눌러서 말하기 모드에서 지금 키를 누르고 있는가</summary>
    public bool TalkKeyHeld { get; private set; }

    public event Action OnVoiceStateChanged;     // UI 갱신용 (연결/음소거/모드 변경)


    const string KeyMode = "voice_mode";
    const string KeyPtt = "voice_ptt_key";
    const string KeyIn = "voice_in_vol";
    const string KeyOpp = "voice_opp_vol";
    const string KeyMute = "voice_self_muted";
    const string KeyInDevice = "voice_in_device";
    const string KeyOutDevice = "voice_out_device";
    const string KeyOutDev = "voice_out_device";
    const string KeyEnabled = "voice_enabled";

    string joinedChannel;
    VivoxParticipant self;
    VivoxParticipant opponent;
    bool initialized;


    // ────────────────────────── 생명주기 ──────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        LoadPrefs();
    }

    void Start()
    {
        if (RelayManager.Instance != null)
        {
            RelayManager.Instance.OnRoomConnectSuccessed += HandleJoined;
            RelayManager.Instance.OnRoomQuited += HandleQuit;
        }
        else Debug.LogError("[Voice] RelayManager가 씬에 없습니다.", this);

        // 방에 들어가기 전에도 장치 목록을 읽을 수 있어야 하므로 여기서 초기화한다
        _ = InitVivoxAsync();
    }


    /// <summary>
    /// Vivox 초기화. 여러 번 불러도 한 번만 한다.
    /// RelayManager.InitAsync에 VivoxService.InitializeAsync를 안 넣었어도 여기서 처리된다.
    /// </summary>
    public async Task InitVivoxAsync()
    {
        if (VivoxReady) return;

        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            await VivoxService.Instance.InitializeAsync();

            VivoxReady = true;
            Log("Vivox 초기화 완료 ? 장치 목록 사용 가능");

            ApplyInputVolume();
            await ApplySavedDevicesAsync();

            OnVoiceStateChanged?.Invoke();
        }
        catch (Exception e)
        {
            // 음성이 안 되는 건 게임을 막을 이유가 아니다
            Debug.LogWarning($"[Voice] Vivox 초기화 실패: {e.Message}", this);
        }
    }

    void OnDestroy()
    {
        if (RelayManager.Instance != null)
        {
            RelayManager.Instance.OnRoomConnectSuccessed -= HandleJoined;
            RelayManager.Instance.OnRoomQuited -= HandleQuit;
        }
        if (Instance == this) Instance = null;
    }


    void Update()
    {
        if (!IsConnected) return;

        UpdatePushToTalk();
        UpdateSpeakingState();
    }


    // ────────────────────────── 접속 / 퇴장 ──────────────────────────

    void HandleJoined() => _ = ConnectAsync();
    void HandleQuit(string reason) => _ = DisconnectAsync();

    async Task ConnectAsync()
    {
        try
        {
            if (!VoiceEnabled) { Log("음성 채팅이 꺼져 있어 접속하지 않는다"); return; }

            string code = RelayManager.Instance.joinCode;
            if (string.IsNullOrEmpty(code)) return;

            await InitVivoxAsync();
            if (!VivoxReady) return;

            if (!VivoxService.Instance.IsLoggedIn)
            {
                var options = new LoginOptions { DisplayName = "CREW" };
                await VivoxService.Instance.LoginAsync(options);
            }

            if (!initialized)
            {
                VivoxService.Instance.ParticipantAddedToChannel += OnParticipantAdded;
                VivoxService.Instance.ParticipantRemovedFromChannel += OnParticipantRemoved;
                initialized = true;
            }

            joinedChannel = channelPrefix + code;
            await VivoxService.Instance.JoinGroupChannelAsync(joinedChannel, ChatCapability.AudioOnly);

            IsConnected = true;
            ApplyInputVolume();
            ApplyMicState();
            await ApplySavedDevicesAsync();

            Log($"채널 접속: {joinedChannel}");
            OnVoiceStateChanged?.Invoke();
        }
        catch (Exception e)
        {
            // 음성이 안 되는 건 게임을 막을 이유가 아니다
            Debug.LogWarning($"[Voice] 접속 실패: {e.Message}", this);
            joinedChannel = null;
            IsConnected = false;
            OnVoiceStateChanged?.Invoke();
        }
    }

    async Task DisconnectAsync()
    {
        try
        {
            if (joinedChannel != null)
                await VivoxService.Instance.LeaveChannelAsync(joinedChannel);

            if (VivoxService.Instance.IsLoggedIn)
                await VivoxService.Instance.LogoutAsync();
        }
        catch (Exception e) { Debug.LogWarning($"[Voice] 종료 실패: {e.Message}", this); }

        joinedChannel = null;
        self = opponent = null;
        IsConnected = false;
        SelfSpeaking = OpponentSpeaking = false;
        SelfEnergy = OpponentEnergy = 0f;
        OnVoiceStateChanged?.Invoke();
    }


    // ────────────────────────── 참가자 ──────────────────────────

    void OnParticipantAdded(VivoxParticipant p)
    {
        if (p.IsSelf) self = p;
        else
        {
            opponent = p;
            ApplyOpponentVolume();
        }
        Log($"참가: {p.DisplayName} (self={p.IsSelf})");
        OnVoiceStateChanged?.Invoke();
    }

    void OnParticipantRemoved(VivoxParticipant p)
    {
        if (p.IsSelf) self = null;
        else { opponent = null; OpponentSpeaking = false; OpponentEnergy = 0f; }
        OnVoiceStateChanged?.Invoke();
    }

    /// <summary>
    /// 말하는 중인지 매 프레임 읽는다.
    /// 이벤트로 받을 수도 있지만, 폴링이 상태 꼬임이 없고 값 비교 몇 번이라 비용도 거의 없다.
    /// </summary>
    void UpdateSpeakingState()
    {
        if (self != null)
        {
            SelfSpeaking = self.SpeechDetected && !SelfMuted && CanTalkNow();
            SelfEnergy = Mathf.Clamp01((float)self.AudioEnergy);
        }
        else { SelfSpeaking = false; SelfEnergy = 0f; }

        if (opponent != null)
        {
            OpponentSpeaking = opponent.SpeechDetected;
            OpponentEnergy = Mathf.Clamp01((float)opponent.AudioEnergy);
        }
        else { OpponentSpeaking = false; OpponentEnergy = 0f; }
    }


    // ────────────────────────── 마이크 켜고 끄기 ──────────────────────────

    void UpdatePushToTalk()
    {
        if (micMode != MicMode.PushToTalk) return;

        bool held = Input.GetKey(pushToTalkKey);
        if (held == TalkKeyHeld) return;

        TalkKeyHeld = held;
        ApplyMicState();
    }

    /// <summary>지금 말할 수 있는 상태인가</summary>
    public bool CanTalkNow()
    {
        if (SelfMuted) return false;
        if (micMode == MicMode.PushToTalk) return TalkKeyHeld;
        return true;
    }

    /// <summary>현재 설정대로 마이크를 켜거나 끈다</summary>
    void ApplyMicState()
    {
        if (!IsConnected) return;

        try
        {
            if (CanTalkNow()) VivoxService.Instance.UnmuteInputDevice();
            else VivoxService.Instance.MuteInputDevice();
        }
        catch (Exception e) { Debug.LogWarning($"[Voice] 마이크 전환 실패: {e.Message}", this); }
    }


    // ────────────────────────── 설정 (설정 창에서 호출) ──────────────────────────

    public void SetMicMode(MicMode mode)
    {
        micMode = mode;
        TalkKeyHeld = false;
        PlayerPrefs.SetInt(KeyMode, (int)mode);
        PlayerPrefs.Save();
        ApplyMicState();
        OnVoiceStateChanged?.Invoke();
    }

    public void SetPushToTalkKey(KeyCode key)
    {
        pushToTalkKey = key;
        PlayerPrefs.SetInt(KeyPtt, (int)key);
        PlayerPrefs.Save();
        OnVoiceStateChanged?.Invoke();
    }

    /// <summary>
    /// 음성 채팅 자체를 켜고 끈다.
    /// 끄면 채널에서 나가고, 방에 들어가도 접속하지 않는다.
    /// </summary>
    public void SetVoiceEnabled(bool on)
    {
        if (VoiceEnabled == on) return;

        VoiceEnabled = on;
        PlayerPrefs.SetInt(KeyEnabled, on ? 1 : 0);
        PlayerPrefs.Save();

        bool inRoom = RelayManager.Instance != null && RelayManager.Instance.isInRoom;

        if (!on && IsConnected) _ = DisconnectAsync();
        else if (on && inRoom && !IsConnected) _ = ConnectAsync();

        Log($"음성 채팅 {(on ? "켜짐" : "꺼짐")}");
        OnVoiceStateChanged?.Invoke();
    }

    public void SetSelfMuted(bool muted)
    {
        SelfMuted = muted;
        PlayerPrefs.SetInt(KeyMute, muted ? 1 : 0);
        PlayerPrefs.Save();
        ApplyMicState();
        OnVoiceStateChanged?.Invoke();
    }

    public void SetInputVolume(int v)
    {
        inputVolume = Mathf.Clamp(v, -50, 50);
        PlayerPrefs.SetInt(KeyIn, inputVolume);
        PlayerPrefs.Save();
        ApplyInputVolume();
    }

    public void SetOpponentVolume(int v)
    {
        opponentVolume = Mathf.Clamp(v, -50, 50);
        PlayerPrefs.SetInt(KeyOpp, opponentVolume);
        PlayerPrefs.Save();
        ApplyOpponentVolume();
    }

    void ApplyInputVolume()
    {
        if (!VivoxReady) return;
        try { VivoxService.Instance.SetInputDeviceVolume(inputVolume); }
        catch (Exception e) { Debug.LogWarning($"[Voice] 입력 볼륨 실패: {e.Message}", this); }
    }

    void ApplyOpponentVolume()
    {
        if (opponent == null) return;
        try { opponent.SetLocalVolume(opponentVolume); }
        catch (Exception e) { Debug.LogWarning($"[Voice] 상대 볼륨 실패: {e.Message}", this); }
    }


    // ────────────────────────── 장치 ──────────────────────────
    //
    // 장치 이름을 읽는 곳은 DeviceLabel 하나뿐이다.
    // SDK 버전 때문에 이름이 다르면 여기 한 줄만 고치면 된다.

    static string DeviceLabel(VivoxInputDevice d) => d.DeviceName;
    static string DeviceLabel(VivoxOutputDevice d) => d.DeviceName;


    /// <summary>마이크 목록. 설정 창 드롭다운에 넣는다</summary>
    public List<string> GetInputDeviceNames()
    {
        var names = new List<string>();
        if (!VivoxReady) return names;
        try
        {
            foreach (var d in VivoxService.Instance.AvailableInputDevices)
                names.Add(DeviceLabel(d));
        }
        catch (Exception e) { Debug.LogWarning($"[Voice] 마이크 목록 실패: {e.Message}", this); }
        return names;
    }

    /// <summary>스피커 목록</summary>
    public List<string> GetOutputDeviceNames()
    {
        var names = new List<string>();
        if (!VivoxReady) return names;
        try
        {
            foreach (var d in VivoxService.Instance.AvailableOutputDevices)
                names.Add(DeviceLabel(d));
        }
        catch (Exception e) { Debug.LogWarning($"[Voice] 스피커 목록 실패: {e.Message}", this); }
        return names;
    }

    /// <summary>지금 고른 마이크가 목록에서 몇 번째인가 (드롭다운 초기값용)</summary>
    public int GetInputDeviceIndex() => IndexOfSaved(GetInputDeviceNames(), KeyInDevice);

    /// <summary>지금 고른 스피커가 목록에서 몇 번째인가</summary>
    public int GetOutputDeviceIndex() => IndexOfSaved(GetOutputDeviceNames(), KeyOutDevice);

    static int IndexOfSaved(List<string> names, string prefKey)
    {
        string want = PlayerPrefs.GetString(prefKey, "");
        if (string.IsNullOrEmpty(want)) return 0;

        int i = names.IndexOf(want);
        return i >= 0 ? i : 0;
    }


    public async void SetInputDevice(int index)
    {
        try
        {
            var devices = VivoxService.Instance.AvailableInputDevices;
            if (index < 0 || index >= devices.Count) return;

            await VivoxService.Instance.SetActiveInputDeviceAsync(devices[index]);

            PlayerPrefs.SetString(KeyInDevice, DeviceLabel(devices[index]));
            PlayerPrefs.Save();
            Log($"마이크: {DeviceLabel(devices[index])}");
        }
        catch (Exception e) { Debug.LogWarning($"[Voice] 마이크 변경 실패: {e.Message}", this); }
    }

    public async void SetOutputDevice(int index)
    {
        try
        {
            var devices = VivoxService.Instance.AvailableOutputDevices;
            if (index < 0 || index >= devices.Count) return;

            await VivoxService.Instance.SetActiveOutputDeviceAsync(devices[index]);

            PlayerPrefs.SetString(KeyOutDevice, DeviceLabel(devices[index]));
            PlayerPrefs.Save();
            Log($"스피커: {DeviceLabel(devices[index])}");
        }
        catch (Exception e) { Debug.LogWarning($"[Voice] 스피커 변경 실패: {e.Message}", this); }
    }


    /// <summary>저장해 둔 장치를 다시 고른다. 채널 접속 직후 호출</summary>
    async Task ApplySavedDevicesAsync()
    {
        try
        {
            string wantIn = PlayerPrefs.GetString(KeyInDevice, "");
            if (!string.IsNullOrEmpty(wantIn))
                foreach (var d in VivoxService.Instance.AvailableInputDevices)
                    if (DeviceLabel(d) == wantIn)
                    {
                        await VivoxService.Instance.SetActiveInputDeviceAsync(d);
                        break;
                    }

            string wantOut = PlayerPrefs.GetString(KeyOutDevice, "");
            if (!string.IsNullOrEmpty(wantOut))
                foreach (var d in VivoxService.Instance.AvailableOutputDevices)
                    if (DeviceLabel(d) == wantOut)
                    {
                        await VivoxService.Instance.SetActiveOutputDeviceAsync(d);
                        break;
                    }
        }
        catch (Exception e) { Debug.LogWarning($"[Voice] 저장된 장치 적용 실패: {e.Message}", this); }
    }


    // ────────────────────────── 저장값 ──────────────────────────

    void LoadPrefs()
    {
        micMode = (MicMode)PlayerPrefs.GetInt(KeyMode, (int)micMode);
        pushToTalkKey = (KeyCode)PlayerPrefs.GetInt(KeyPtt, (int)pushToTalkKey);
        inputVolume = PlayerPrefs.GetInt(KeyIn, inputVolume);
        opponentVolume = PlayerPrefs.GetInt(KeyOpp, opponentVolume);
        SelfMuted = PlayerPrefs.GetInt(KeyMute, 0) == 1;
        VoiceEnabled = PlayerPrefs.GetInt(KeyEnabled, 1) == 1;
    }

    void Log(string msg)
    {
        if (debugLog) Debug.Log($"[Voice] {msg}", this);
    }
}