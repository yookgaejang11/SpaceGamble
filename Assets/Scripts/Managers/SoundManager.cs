using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SoundManager : ClientUIBase
{
    public static SoundManager Instance { get; private set; }

    [Serializable]
    public class Sound
    {
        [Tooltip("코드에서 부를 이름 (예: lever, tick, eject)")]
        public string id;
        [Tooltip("여러 개 넣으면 랜덤으로 하나")]
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("재생할 때마다 음높이를 이 범위에서 살짝 바꾼다 (같은 소리 반복이 덜 지루하게). 1~1이면 고정")]
        public Vector2 pitchRange = new Vector2(1f, 1f);
    }

    [Header("효과음 목록 (id는 다른 스크립트가 부르는 이름 — 클립만 끌어다 넣으면 된다)")]
    public Sound[] sounds =
    {
        // CenterHologram (가운데 홀로그램 연출)
        new Sound { id = "lever" },             // 레버 당김
        new Sound { id = "scramble_tick" },     // 홀로그램 숫자 스크램블 틱
        new Sound { id = "reveal" },            // 선택/카드 공개 "딱"
        new Sound { id = "overload" },          // OVERLOAD 경고
        new Sound { id = "charge" },            // 사출 충전 (버튼 누른 직후)
        new Sound { id = "eject" },             // 사출 발사
        new Sound { id = "safe" },              // 살았다 (사출 안 됨)
        new Sound { id = "siren" },             // 사출 사이렌 (반복 재생)
        // SeatBoard (숫자 보드 슬롯머신)
        new Sound { id = "slot_tick" },         // 숫자 돌아갈 때 틱
        new Sound { id = "slot_lock" },         // 내 칸 ?? 철컥
        new Sound { id = "slot_land" },         // 숫자 확정 딱
        // CrtPopup (선택 UI / 엔드 문구 TV 켜짐)
        new Sound { id = "tv_on" },
        new Sound { id = "tv_off" },
    };

    [Header("BGM (비워두면 그 상황엔 이전 BGM 유지)")]
    [Tooltip("방에 들어가기 전 (로비)")]
    public AudioClip lobbyBgm;
    [Tooltip("방 안 대기 (F4 준비)")]
    public AudioClip waitingBgm;
    [Tooltip("게임 진행 중 (RoundStart ~ Result)")]
    public AudioClip gameBgm;
    [Tooltip("버튼 누르기 대기 (Press) — 긴장감. 비워두면 gameBgm 유지")]
    public AudioClip pressBgm;
    [Tooltip("게임 끝 (GameOver)")]
    public AudioClip gameOverBgm;

    [Range(0f, 1f)] public float bgmBaseVolume = 0.6f;
    [Tooltip("BGM 바뀔 때 크로스페이드 시간(초)")]
    public float crossFadeTime = 1.2f;

    [Header("볼륨 (저장됨)")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float bgmVolume = 1f;
    [Range(0f, 1f)] public float sfxVolume = 1f;



    [Header("디버그")]
    [Tooltip("체크하면 볼륨을 바꿀 때 지금 BGM이 실제로 SoundManager에서 나오는지 콘솔에 찍는다")]
    public bool debugLog = false;

    [Header("3D 효과음")]
    [Tooltip("동시에 날 수 있는 3D 효과음 개수")]
    public int poolSize = 8;
    [Tooltip("3D 소리가 들리는 최대 거리")]
    public float maxDistance = 20f;

    const string KeyMaster = "vol_master", KeyBgm = "vol_bgm", KeySfx = "vol_sfx";

    AudioSource bgmA, bgmB;             // 크로스페이드용 두 개 (번갈아 사용)
    AudioSource activeBgm;
    AudioSource sfx2D;                  // 위치 없는 효과음
    readonly List<AudioSource> pool3D = new List<AudioSource>();
    int poolIndex;
    Coroutine fadeRoutine;
    readonly Dictionary<string, Sound> table = new Dictionary<string, Sound>();


    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        bgmA = MakeSource("BGM_A", loop: true, spatial: false);
        bgmB = MakeSource("BGM_B", loop: true, spatial: false);
        sfx2D = MakeSource("SFX_2D", loop: false, spatial: false);
        for (int i = 0; i < poolSize; i++)
            pool3D.Add(MakeSource($"SFX_3D_{i}", loop: false, spatial: true));

        foreach (var s in sounds)
            if (s != null && !string.IsNullOrEmpty(s.id)) table[s.id] = s;

        masterVolume = PlayerPrefs.GetFloat(KeyMaster, masterVolume);
        bgmVolume = PlayerPrefs.GetFloat(KeyBgm, bgmVolume);
        sfxVolume = PlayerPrefs.GetFloat(KeySfx, sfxVolume);

        AudioListener.volume = masterVolume;
    }

    void Start()
    {
        // 방에 들어가기 전이면 로비 BGM
        if (client == null) PlayBgm(lobbyBgm);
    }

    // 슬라이더 값을 저장된 볼륨으로 맞추고(이벤트 없이), 이후 움직임을 볼륨에 연결
    static void BindSlider(Slider slider, float value, UnityEngine.Events.UnityAction<float> onChanged)
    {
        if (slider == null) return;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.SetValueWithoutNotify(value);
        slider.onValueChanged.AddListener(onChanged);
    }


    AudioSource MakeSource(string n, bool loop, bool spatial)
    {
        var go = new GameObject(n);
        go.transform.SetParent(transform, false);
        var a = go.AddComponent<AudioSource>();
        a.playOnAwake = false;
        a.loop = loop;
        a.spatialBlend = spatial ? 1f : 0f;
        if (spatial)
        {
            a.rolloffMode = AudioRolloffMode.Linear;
            a.maxDistance = maxDistance;
        }
        return a;
    }


    protected override void Subscribe() { client.OnChangedPhase += HandlePhase; }
    protected override void Unsubscribe() { client.OnChangedPhase -= HandlePhase; }
    protected override void Refresh() { HandlePhase(client.curPhase); }
    protected override void OnClientLost() { PlayBgm(lobbyBgm); }   // 방 나가면 로비 BGM

    void HandlePhase(GamePhase phase)
    {
        switch (phase)
        {
            case GamePhase.waiting: PlayBgm(waitingBgm); break;
            case GamePhase.Press: PlayBgm(pressBgm != null ? pressBgm : gameBgm); break;
            case GamePhase.GameOver: PlayBgm(gameOverBgm); break;
            default: PlayBgm(gameBgm); break;   // RoundStart, talkTime, Open, Result
        }
    }

    /// <summary>BGM 바꾸기. 같은 곡이면 아무것도 안 한다 (처음부터 다시 틀지 않음)</summary>
    public void PlayBgm(AudioClip clip)
    {
        if (clip == null) return;
        if (activeBgm != null && activeBgm.clip == clip && activeBgm.isPlaying) return;

        AudioSource from = activeBgm;
        AudioSource to = (activeBgm == bgmA) ? bgmB : bgmA;

        to.clip = clip;
        to.volume = 0f;
        to.Play();
        activeBgm = to;

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(CrossFade(from, to));
    }

    public void StopBgm()
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(CrossFade(activeBgm, null));
        activeBgm = null;
    }

    IEnumerator CrossFade(AudioSource from, AudioSource to)
    {
        float t = 0f;
        float fromStart = from != null ? from.volume : 0f;
        while (t < crossFadeTime)
        {
            t += Time.unscaledDeltaTime;
            float k = crossFadeTime > 0f ? t / crossFadeTime : 1f;
            if (from != null) from.volume = Mathf.Lerp(fromStart, 0f, k);
            if (to != null) to.volume = Mathf.Lerp(0f, BgmTarget, k);
            yield return null;
        }
        if (from != null) { from.volume = 0f; from.Stop(); }
        if (to != null) to.volume = BgmTarget;
        fadeRoutine = null;
    }

    float BgmTarget => bgmBaseVolume * bgmVolume;          // 마스터는 AudioListener가 따로 곱한다

    /// <summary>이름으로 효과음 (2D, 어디서나 같은 크기)</summary>
    public static void Play(string id) => Instance?.PlayInternal(id, null);

    /// <summary>이름으로 효과음 (3D, 그 위치에서 소리)</summary>
    public static void PlayAt(string id, Vector3 position) => Instance?.PlayInternal(id, position);

    /// <summary>클립을 직접 (2D)</summary>
    public static void PlayClip(AudioClip clip, float volume = 1f)
    {
        if (Instance == null || clip == null) return;
        Instance.sfx2D.pitch = 1f;
        Instance.sfx2D.PlayOneShot(clip, volume * Instance.SfxScale);
    }

    void PlayInternal(string id, Vector3? position)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (!table.TryGetValue(id, out var s))
        {
            Debug.LogWarning($"[SoundManager] '{id}' 이름이 Sounds 목록에 없음 (오타 확인)");
            return;
        }
        if (s.clips == null || s.clips.Length == 0) return;    // 아직 클립을 안 넣은 소리는 조용히 넘어감

        AudioClip clip = s.clips[UnityEngine.Random.Range(0, s.clips.Length)];
        if (clip == null) return;

        AudioSource src;
        if (position.HasValue)
        {
            src = pool3D[poolIndex];                    // 돌려가며 사용 (가장 오래된 걸 재활용)
            poolIndex = (poolIndex + 1) % pool3D.Count;
            src.transform.position = position.Value;
        }
        else src = sfx2D;

        src.pitch = UnityEngine.Random.Range(s.pitchRange.x, s.pitchRange.y);
        src.PlayOneShot(clip, s.volume * SfxScale);
    }

    float SfxScale => sfxVolume;                           // 마스터는 AudioListener가 따로 곱한다



    readonly Dictionary<string, AudioSource> loops = new Dictionary<string, AudioSource>();
    readonly Dictionary<string, float> loopBaseVolume = new Dictionary<string, float>();

    /// <summary>끌 때까지 반복 재생. 이미 울리는 중이면 무시</summary>
    public static void PlayLoop(string id) => Instance?.PlayLoopInternal(id);

    /// <summary>반복 재생 끄기</summary>
    public static void StopLoop(string id) => Instance?.StopLoopInternal(id);

    void PlayLoopInternal(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (!table.TryGetValue(id, out var s))
        {
            Debug.LogWarning($"[SoundManager] '{id}' 이름이 Sounds 목록에 없음 (오타 확인)");
            return;
        }
        if (s.clips == null || s.clips.Length == 0 || s.clips[0] == null) return;

        if (!loops.TryGetValue(id, out var src))
        {
            src = MakeSource($"LOOP_{id}", loop: true, spatial: false);
            loops[id] = src;
        }
        if (src.isPlaying) return;

        src.clip = s.clips[UnityEngine.Random.Range(0, s.clips.Length)];
        src.pitch = 1f;
        loopBaseVolume[id] = s.volume;
        src.volume = s.volume * SfxScale;
        src.Play();
    }

    void StopLoopInternal(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (loops.TryGetValue(id, out var src) && src.isPlaying) src.Stop();
    }

    public void SetMasterVolume(float v) { masterVolume = v; Save(KeyMaster, v); AudioListener.volume = v; }
    public void SetBgmVolume(float v) { bgmVolume = v; Save(KeyBgm, v); ApplyBgmVolume(); }
    public void SetSfxVolume(float v) { sfxVolume = v; Save(KeySfx, v); ApplyLoopVolume(); }

    void ApplyLoopVolume()
    {
        foreach (var kv in loops)
            if (kv.Value != null && loopBaseVolume.TryGetValue(kv.Key, out var b))
                kv.Value.volume = b * SfxScale;
    }

    void ApplyBgmVolume()
    {
        // 크로스페이드 중이어도 바로 반영 (페이드는 매 프레임 BgmTarget을 다시 읽으므로 안전)
        if (activeBgm != null) activeBgm.volume = BgmTarget;
        if (debugLog) Debug.Log($"[SoundManager] BGM 볼륨 {BgmTarget:F2} (재생 중: {(activeBgm != null && activeBgm.isPlaying ? activeBgm.clip.name : "없음")})");
    }

    static void Save(string key, float v)
    {
        PlayerPrefs.SetFloat(key, v);
        PlayerPrefs.Save();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
