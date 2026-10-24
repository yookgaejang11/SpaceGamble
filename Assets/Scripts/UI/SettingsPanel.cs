using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 설정 창. 음성과 게임 사운드를 한 곳에서 다룬다.
///
/// 비워둔 칸은 그냥 무시되므로, 필요한 것만 연결해도 된다.
///
/// 붙이는 곳
///   설정 패널 루트. ESC 메뉴(PauseMenu)에서 이 오브젝트를 켜고 끄면 된다.
///
/// 저장
///   음성 설정은 VoiceManager가, 사운드 볼륨은 SoundManager가 각자 저장한다.
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    [Header("── 마이크 ──")]
    [Tooltip("오픈 마이크 / 눌러서 말하기")]
    public TMP_Dropdown micModeDropdown;

    [Tooltip("눌러서 말하기 키를 보여주고, 누르면 새 키를 받는다")]
    public Button pttKeyButton;
    public TextMeshProUGUI pttKeyLabel;

    [Tooltip("내 마이크 끄기")]
    public Toggle selfMuteToggle;

    [Tooltip("마이크 장치 선택")]
    public TMP_Dropdown deviceDropdown;

    [Header("── 음성 볼륨 ──")]
    [Tooltip("내 마이크 입력 (-50 ~ 50)")]
    public Slider inputVolumeSlider;
    public TextMeshProUGUI inputVolumeLabel;

    [Tooltip("상대 목소리 (-50 ~ 50)")]
    public Slider opponentVolumeSlider;
    public TextMeshProUGUI opponentVolumeLabel;

    [Header("── 입력 레벨 미터 ──")]
    [Tooltip("Image Type을 Filled로. 내 목소리 크기만큼 찬다")]
    public Image levelBar;
    public float levelSmooth = 12f;

    [Header("── 게임 사운드 ──")]
    public Slider masterSlider;
    public Slider bgmSlider;
    public Slider sfxSlider;

    [Header("문구")]
    public string waitingKeyText = "키를 누르세요...";
    public string notConnectedText = "방에 들어가면 사용할 수 있습니다";
    [Tooltip("방 밖일 때 흐리게 할 묶음. 없으면 비워두기")]
    public CanvasGroup voiceGroup;
    public TextMeshProUGUI voiceStatusLabel;

    bool waitingForKey;
    float shownLevel;
    bool built;


    // ────────────────────────── 켜고 끄기 ──────────────────────────

    void OnEnable()
    {
        Build();
        RefreshAll();

        if (VoiceManager.Instance != null)
            VoiceManager.Instance.OnVoiceStateChanged += RefreshAll;
    }

    void OnDisable()
    {
        waitingForKey = false;

        if (VoiceManager.Instance != null)
            VoiceManager.Instance.OnVoiceStateChanged -= RefreshAll;
    }


    /// <summary>리스너 연결. 한 번만 한다</summary>
    void Build()
    {
        if (built) return;
        built = true;

        if (micModeDropdown != null)
        {
            micModeDropdown.ClearOptions();
            micModeDropdown.AddOptions(new List<string> { "오픈 마이크", "눌러서 말하기" });
            micModeDropdown.onValueChanged.AddListener(OnMicModeChanged);
        }

        if (pttKeyButton != null)
            pttKeyButton.onClick.AddListener(StartKeyRebind);

        if (selfMuteToggle != null)
            selfMuteToggle.onValueChanged.AddListener(OnSelfMuteChanged);

        if (deviceDropdown != null)
            deviceDropdown.onValueChanged.AddListener(OnDeviceChanged);

        SetupVolumeSlider(inputVolumeSlider, OnInputVolumeChanged);
        SetupVolumeSlider(opponentVolumeSlider, OnOpponentVolumeChanged);

        var sm = SoundManager.Instance;
        if (sm != null)
        {
            Setup01(masterSlider, sm.masterVolume, sm.SetMasterVolume);
            Setup01(bgmSlider, sm.bgmVolume, sm.SetBgmVolume);
            Setup01(sfxSlider, sm.sfxVolume, sm.SetSfxVolume);
        }
    }

    static void SetupVolumeSlider(Slider s, UnityEngine.Events.UnityAction<float> cb)
    {
        if (s == null) return;
        s.minValue = -50f;
        s.maxValue = 50f;
        s.wholeNumbers = true;
        s.onValueChanged.AddListener(cb);
    }

    static void Setup01(Slider s, float value, UnityEngine.Events.UnityAction<float> cb)
    {
        if (s == null) return;
        s.minValue = 0f;
        s.maxValue = 1f;
        s.wholeNumbers = false;
        s.SetValueWithoutNotify(value);
        s.onValueChanged.AddListener(cb);
    }


    // ────────────────────────── 화면 갱신 ──────────────────────────

    void RefreshAll()
    {
        var vm = VoiceManager.Instance;

        bool usable = vm != null && vm.IsConnected;
        if (voiceGroup != null)
        {
            voiceGroup.alpha = usable ? 1f : 0.45f;
            voiceGroup.interactable = usable;
        }
        if (voiceStatusLabel != null)
            voiceStatusLabel.text = usable ? "" : notConnectedText;

        if (vm == null) return;

        if (micModeDropdown != null)
            micModeDropdown.SetValueWithoutNotify((int)vm.micMode);

        if (pttKeyButton != null)
            pttKeyButton.interactable = (vm.micMode == VoiceManager.MicMode.PushToTalk);

        UpdateKeyLabel();

        if (selfMuteToggle != null)
            selfMuteToggle.SetIsOnWithoutNotify(vm.SelfMuted);

        if (inputVolumeSlider != null)
            inputVolumeSlider.SetValueWithoutNotify(vm.inputVolume);
        if (opponentVolumeSlider != null)
            opponentVolumeSlider.SetValueWithoutNotify(vm.opponentVolume);

        UpdateVolumeLabels();
        RefreshDeviceList();
    }

    void RefreshDeviceList()
    {
        if (deviceDropdown == null) return;

        var vm = VoiceManager.Instance;
        if (vm == null || !vm.IsConnected)
        {
            deviceDropdown.ClearOptions();
            deviceDropdown.interactable = false;
            return;
        }

        var names = vm.GetInputDeviceNames();
        deviceDropdown.ClearOptions();
        deviceDropdown.AddOptions(names);
        deviceDropdown.interactable = names.Count > 0;
    }

    void UpdateKeyLabel()
    {
        if (pttKeyLabel == null) return;

        if (waitingForKey) { pttKeyLabel.text = waitingKeyText; return; }

        var vm = VoiceManager.Instance;
        pttKeyLabel.text = vm != null ? vm.pushToTalkKey.ToString() : "-";
    }

    void UpdateVolumeLabels()
    {
        var vm = VoiceManager.Instance;
        if (vm == null) return;

        if (inputVolumeLabel != null) inputVolumeLabel.text = Pretty(vm.inputVolume);
        if (opponentVolumeLabel != null) opponentVolumeLabel.text = Pretty(vm.opponentVolume);
    }

    /// <summary>-50~50을 0~100처럼 읽기 쉽게 바꾼다</summary>
    static string Pretty(int v) => Mathf.RoundToInt((v + 50) ) + "%";


    // ────────────────────────── 입력 처리 ──────────────────────────

    void Update()
    {
        if (waitingForKey) CaptureKey();
        UpdateLevelBar();
    }

    void UpdateLevelBar()
    {
        if (levelBar == null) return;

        var vm = VoiceManager.Instance;
        float want = (vm != null && vm.IsConnected) ? vm.SelfEnergy : 0f;

        float k = 1f - Mathf.Exp(-levelSmooth * Time.unscaledDeltaTime);
        shownLevel = Mathf.Lerp(shownLevel, want, k);
        levelBar.fillAmount = shownLevel;
    }

    void StartKeyRebind()
    {
        waitingForKey = true;
        UpdateKeyLabel();
    }

    /// <summary>다음에 눌리는 키를 눌러서 말하기 키로 삼는다</summary>
    void CaptureKey()
    {
        if (!Input.anyKeyDown) return;

        // ESC는 취소로 쓴다 (설정 창을 닫는 키라 할당하면 곤란하다)
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            waitingForKey = false;
            UpdateKeyLabel();
            return;
        }

        foreach (KeyCode k in System.Enum.GetValues(typeof(KeyCode)))
        {
            if (!Input.GetKeyDown(k)) continue;

            // 마우스 버튼은 조준 클릭과 겹치므로 제외
            if (k >= KeyCode.Mouse0 && k <= KeyCode.Mouse6) continue;

            VoiceManager.Instance?.SetPushToTalkKey(k);
            waitingForKey = false;
            UpdateKeyLabel();
            return;
        }
    }


    // ────────────────────────── 콜백 ──────────────────────────

    void OnMicModeChanged(int index)
    {
        VoiceManager.Instance?.SetMicMode((VoiceManager.MicMode)index);
        if (pttKeyButton != null)
            pttKeyButton.interactable = (index == (int)VoiceManager.MicMode.PushToTalk);
    }

    void OnSelfMuteChanged(bool on) => VoiceManager.Instance?.SetSelfMuted(on);

    void OnDeviceChanged(int index) => VoiceManager.Instance?.SetInputDevice(index);

    void OnInputVolumeChanged(float v)
    {
        VoiceManager.Instance?.SetInputVolume(Mathf.RoundToInt(v));
        UpdateVolumeLabels();
    }

    void OnOpponentVolumeChanged(float v)
    {
        VoiceManager.Instance?.SetOpponentVolume(Mathf.RoundToInt(v));
        UpdateVolumeLabels();
    }
}
