using System;
using DG.Tweening;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class EmoteSync : NetworkBehaviour
{
    [Serializable]
    public class Emote
    {
        public KeyCode key = KeyCode.Alpha1;
        [Tooltip("말풍선 글자 (TMP 한글 폰트가 없으면 영어로)")]
        public string text = "GG";
        [Tooltip("아이콘. 비워두면 글자만")]
        public Sprite icon;
        public Color color = Color.white;
        [Tooltip("이 감정표현의 음성 id (SoundManager Sounds 목록). 비우면 아래 기본 사운드")]
        public string voiceId = "";
    }

    [Header("감정표현 목록 (키 / 글자 / 아이콘)")]
    public Emote[] emotes =
    {
        new Emote { key = KeyCode.Alpha1, text = "GG",   color = new Color(0.6f, 0.9f, 1f),  voiceId = "voice_gg" },
        new Emote { key = KeyCode.Alpha2, text = "?!",   color = new Color(1f, 0.85f, 0.3f), voiceId = "voice_what" },
        new Emote { key = KeyCode.Alpha3, text = "HAHA", color = new Color(0.5f, 1f, 0.6f),  voiceId = "voice_laugh" },
        new Emote { key = KeyCode.Alpha4, text = "...",  color = new Color(1f, 0.4f, 0.3f),  voiceId = "voice_sigh" },
    };

    [Header("표시 방식")]
    [Tooltip("머리 위 말풍선을 띄운다. 끄면 음성만")]
    public bool showBubble = true;
    [Tooltip("음성을 낸다")]
    public bool playVoice = true;
    [Tooltip("음성을 머리 위치에서 3D로 (멀면 작게). 끄면 2D로 어디서나 같은 크기")]
    public bool voice3D = true;

    [Header("말풍선 UI (프리팹 안 World Space Canvas)")]
    [Tooltip("말풍선 루트. 평소엔 꺼 둔다")]
    public GameObject bubbleRoot;
    public TMP_Text bubbleText;
    [Tooltip("아이콘 Image. 없으면 비워두기")]
    public Image bubbleIcon;
    [Tooltip("말풍선 배경 등 같이 페이드할 CanvasGroup (bubbleRoot에 붙이기)")]
    public CanvasGroup bubbleGroup;

    [Header("위치")]
    [Tooltip("머리 본 (spine.006 등). 말풍선이 이 위에 뜬다")]
    public Transform headAnchor;
    public Vector3 offset = new Vector3(0f, 0.45f, 0f);

    [Header("시간 (초)")]
    public float showTime = 2f;
    public float popTime = 0.2f;
    public float fadeTime = 0.3f;
    [Tooltip("연타 방지")]
    public float cooldown = 1.5f;

    [Header("허용 페이즈 (연출 중엔 막는다)")]
    public bool allowWaiting = true;
    public bool allowTalkTime = true;
    public bool allowPress = true;

    [Header("내 화면")]
    [Tooltip("1인칭이라 보통 끈다. 켜면 내 머리 위 말풍선도 내 화면에 보인다")]
    public bool showOwnBubble = false;
    [Tooltip("내가 보낸 감정표현을 화면 HUD에 잠깐 띄울 글자 (없으면 비워두기)")]
    public TMP_Text ownerFeedbackText;

    [Header("기본 사운드 id (SoundManager) — 감정표현에 voiceId가 없을 때. 비우면 소리 없음")]
    public string sndEmote = "";

    float lastSendTime = -99f;      // 내 쪽 쿨타임
    float lastServerTime = -99f;    // 서버 쪽 쿨타임 (조작된 연타 방지)
    Sequence seq;
    Sequence feedbackSeq;
    Vector3 bubbleBaseScale = Vector3.one;
    Camera viewCam;


    void Awake()
    {
        if (bubbleRoot != null)
        {
            bubbleBaseScale = bubbleRoot.transform.localScale;
            bubbleRoot.SetActive(false);
        }
        if (ownerFeedbackText != null) ownerFeedbackText.gameObject.SetActive(false);
    }

    public override void OnNetworkDespawn()
    {
        HideNow();
    }

    public override void OnDestroy()
    {
        seq?.Kill();
        feedbackSeq?.Kill();
        base.OnDestroy();
    }

    void Update()
    {
        // 연출 페이즈로 넘어가면 떠 있던 말풍선을 바로 치운다 (컷신 위에 둥둥 뜨지 않게)
        if (bubbleRoot != null && bubbleRoot.activeSelf && !PhaseAllowed()) HideNow();

        if (!IsOwner || !IsSpawned) return;
        if (PauseMenu.IsOpen) return;
        if (!PhaseAllowed()) return;
        if (Time.time - lastSendTime < cooldown) return;

        for (int i = 0; i < emotes.Length; i++)
        {
            if (emotes[i] == null || !Input.GetKeyDown(emotes[i].key)) continue;

            lastSendTime = Time.time;
            RequestEmoteRpc(i);
            ShowOwnerFeedback(i);
            break;
        }
    }

    /// <summary>
    /// 지금 감정표현을 써도 되는 페이즈인가.
    /// 상대 우주인 오브젝트의 curPhase는 갱신되지 않으므로 내 클라이언트(GameClient.Local)의 페이즈를 본다.
    /// </summary>
    bool PhaseAllowed()
    {
        var local = GameClient.Local;
        if (local == null) return false;

        switch (local.curPhase)
        {
            case GamePhase.waiting: return allowWaiting;
            case GamePhase.talkTime: return allowTalkTime;
            case GamePhase.Press: return allowPress;
            default: return false;          // RoundStart / Open / Result / GameOver = 연출 중
        }
    }



    [Rpc(SendTo.Server)]
    void RequestEmoteRpc(int id)
    {
        if (id < 0 || id >= emotes.Length) return;
        if (Time.time - lastServerTime < cooldown * 0.8f) return;   // 네트워크 지연 감안해 살짝 여유
        lastServerTime = Time.time;

        ShowEmoteRpc(id);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void ShowEmoteRpc(int id)
    {
        if (id < 0 || id >= emotes.Length || emotes[id] == null) return;
        if (!PhaseAllowed()) return;                // 도착했을 때 이미 연출이 시작됐으면 무시

        // 음성: 내 것도 들린다 (보냈다는 확인 겸)
        if (playVoice) PlayVoice(emotes[id]);

        if (!showBubble) return;
        if (IsOwner && !showOwnBubble) return;      // 내 머리 위는 내 화면에 안 띄운다
        ShowBubble(emotes[id]);
    }


    void PlayVoice(Emote e)
    {
        string id = !string.IsNullOrEmpty(e.voiceId) ? e.voiceId : sndEmote;
        if (string.IsNullOrEmpty(id)) return;

        if (voice3D)
        {
            Vector3 p = headAnchor != null ? headAnchor.position : transform.position;
            SoundManager.PlayAt(id, p);
        }
        else SoundManager.Play(id);
    }


    void ShowBubble(Emote e)
    {
        if (bubbleRoot == null) return;

        if (bubbleText != null)
        {
            bubbleText.text = e.text;
            bubbleText.color = e.color;
            bubbleText.gameObject.SetActive(!string.IsNullOrEmpty(e.text));
        }
        if (bubbleIcon != null)
        {
            bubbleIcon.sprite = e.icon;
            bubbleIcon.gameObject.SetActive(e.icon != null);
        }

        seq?.Kill();
        bubbleRoot.SetActive(true);
        FollowHead();

        Transform t = bubbleRoot.transform;
        t.localScale = bubbleBaseScale * 0.3f;
        if (bubbleGroup != null) bubbleGroup.alpha = 1f;

        // 톡 튀어나옴 → 잠깐 유지 → 위로 살짝 뜨며 사라짐
        seq = DOTween.Sequence().SetLink(bubbleRoot);
        seq.Append(t.DOScale(bubbleBaseScale, popTime).SetEase(Ease.OutBack));
        seq.AppendInterval(showTime);
        if (bubbleGroup != null) seq.Append(bubbleGroup.DOFade(0f, fadeTime));
        else seq.Append(t.DOScale(Vector3.zero, fadeTime).SetEase(Ease.InBack));
        seq.OnComplete(HideNow);
    }

    void HideNow()
    {
        seq?.Kill();
        if (bubbleRoot == null) return;
        bubbleRoot.SetActive(false);
        bubbleRoot.transform.localScale = bubbleBaseScale;
        if (bubbleGroup != null) bubbleGroup.alpha = 1f;
    }

    void ShowOwnerFeedback(int id)
    {
        if (ownerFeedbackText == null) return;

        var e = emotes[id];
        ownerFeedbackText.text = e.text;
        ownerFeedbackText.color = e.color;
        ownerFeedbackText.gameObject.SetActive(true);

        feedbackSeq?.Kill();
        ownerFeedbackText.alpha = 1f;
        feedbackSeq = DOTween.Sequence().SetLink(ownerFeedbackText.gameObject);
        feedbackSeq.AppendInterval(showTime * 0.6f);
        // TMP용 DOFade는 DOTween Pro 전용이라 alpha 값을 직접 트윈한다
        feedbackSeq.Append(DOTween.To(() => ownerFeedbackText.alpha, a => ownerFeedbackText.alpha = a, 0f, fadeTime));
        feedbackSeq.OnComplete(() => ownerFeedbackText.gameObject.SetActive(false));
    }

    void LateUpdate()
    {
        if (bubbleRoot != null && bubbleRoot.activeSelf) FollowHead();
    }

    void FollowHead()
    {
        Transform t = bubbleRoot.transform;
        Vector3 basePos = headAnchor != null ? headAnchor.position : transform.position + Vector3.up * 1.6f;
        t.position = basePos + offset;

        // 보는 사람 카메라를 정면으로 바라보게 (빌보드)
        Camera c = GetViewCamera();
        if (c != null) t.rotation = Quaternion.LookRotation(t.position - c.transform.position, Vector3.up);
    }

    Camera GetViewCamera()
    {
        if (viewCam != null && viewCam.isActiveAndEnabled) return viewCam;

        viewCam = null;
        var local = GameClient.Local;
        if (local != null)
            foreach (var c in local.GetComponentsInChildren<Camera>())
                if (c.isActiveAndEnabled) { viewCam = c; break; }

        if (viewCam == null) viewCam = Camera.main;
        return viewCam;
    }
}
