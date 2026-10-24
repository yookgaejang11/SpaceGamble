using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 말하는 중일 때 마이크 아이콘을 띄운다.
///
/// 두 가지 용도로 쓴다
///   1) 내 HUD  — seatIndex를 -1로 두면 "나"를 본다
///   2) 상대 자리 — seatIndex를 0 또는 1로 두면 그 자리 사람을 본다
///
/// 상대 아이콘이 중요한 이유
///   이 게임에서 "상대가 지금 말하고 있는가"는 정보다.
///   목소리가 안 들려도(볼륨 0, 음소거) 말하려 했다는 건 보여야 한다.
///
/// 붙이는 곳
///   항상 켜져 있는 오브젝트. icon은 그 자식으로 둔다
///   (icon을 끄면 스크립트도 같이 꺼지기 때문).
/// </summary>
public class MicIndicator : ClientUIBase
{
    [Header("누구를 볼 것인가")]
    [Tooltip("-1 = 나 / 0 또는 1 = 그 자리 사람")]
    public int seatIndex = -1;

    [Header("아이콘")]
    [Tooltip("말할 때 켜질 오브젝트 (마이크 그림)")]
    public GameObject icon;

    [Tooltip("음소거 상태일 때 켤 오브젝트 (빗금 친 마이크). 없으면 비워두기")]
    public GameObject mutedIcon;

    [Tooltip("눌러서 말하기 모드에서 키를 안 누르고 있을 때 켤 오브젝트. 없으면 비워두기")]
    public GameObject readyIcon;

    [Header("레벨 표시 (없으면 비워두기)")]
    [Tooltip("Image Type을 Filled로 둔 이미지. 목소리 크기만큼 찬다")]
    public Image levelBar;
    [Tooltip("레벨이 따라가는 속도")]
    public float levelSmooth = 12f;

    [Header("말할 때 크기 변화")]
    [Tooltip("1이면 변화 없음")]
    public float speakScale = 1.12f;
    public float scaleSmooth = 14f;

    Vector3 baseScale;
    float shownLevel;
    bool iconOn;


    void Awake()
    {
        if (icon != null) baseScale = icon.transform.localScale;
    }

    // 이벤트를 구독할 필요가 없다. 매 프레임 상태를 읽는다
    protected override void Subscribe() { }
    protected override void Unsubscribe() { }

    protected override void OnDisable()
    {
        base.OnDisable();
        SetAll(false, false, false);
    }

    protected override void OnClientLost() => SetAll(false, false, false);


    /// <summary>이 표시기가 "나"를 보는가</summary>
    bool WatchingSelf
    {
        get
        {
            if (seatIndex < 0) return true;
            return client != null && client.myIndex.Value == seatIndex;
        }
    }


    void Update()
    {
        var vm = VoiceManager.Instance;

        if (vm == null || !vm.IsConnected)
        {
            SetAll(false, false, false);
            shownLevel = 0f;
            ApplyLevel();
            return;
        }

        bool self = WatchingSelf;

        bool speaking = self ? vm.SelfSpeaking : vm.OpponentSpeaking;
        float energy = self ? vm.SelfEnergy : vm.OpponentEnergy;

        // 음소거 / 대기 표시는 내 것에만 의미가 있다
        bool muted = self && vm.SelfMuted;
        bool ready = self && !muted
                     && vm.micMode == VoiceManager.MicMode.PushToTalk
                     && !vm.TalkKeyHeld;

        SetAll(speaking, muted, ready);

        // 레벨
        float k = 1f - Mathf.Exp(-levelSmooth * Time.deltaTime);
        shownLevel = Mathf.Lerp(shownLevel, speaking ? energy : 0f, k);
        ApplyLevel();

        // 말할 때 살짝 커진다
        if (icon != null && speakScale != 1f)
        {
            float t = 1f - Mathf.Exp(-scaleSmooth * Time.deltaTime);
            Vector3 want = speaking ? baseScale * (1f + (speakScale - 1f) * shownLevel) : baseScale;
            icon.transform.localScale = Vector3.Lerp(icon.transform.localScale, want, t);
        }
    }

    void ApplyLevel()
    {
        if (levelBar != null) levelBar.fillAmount = shownLevel;
    }

    void SetAll(bool speaking, bool muted, bool ready)
    {
        Set(icon, speaking);
        Set(mutedIcon, muted);
        Set(readyIcon, ready);
        iconOn = speaking;
    }

    static void Set(GameObject go, bool on)
    {
        if (go != null && go.activeSelf != on) go.SetActive(on);
    }
}
