using TMPro;
using UnityEngine;

/// <summary>
/// 방 만들기(Create) / 참가(Join)를 누른 뒤, 내 플레이어가 생길 때까지 덮어 주는 로딩 화면.
///
/// 언제 보이나 (동료 코드는 건드리지 않고 상태만 읽는다)
///   켜짐 : RelayManager.isInRoom == true  &&  GameClient.Local == null
///          (= 방 접속은 시작했는데 아직 내 플레이어가 안 생김 → 로비 카메라 + 조준선만 보이는 그 순간)
///   꺼짐 : 내 플레이어가 생기고(GameClient.Local) 잠깐 더 기다린 뒤 페이드 아웃
///          접속 실패 / 방 나감(isInRoom == false)이면 바로 꺼짐
///
/// 붙이는 곳
///   "항상 켜져 있는" 오브젝트 (예: UIManager)에 붙이고,
///   로딩 화면 패널(전체 화면 Image + 글자)은 그 자식으로 만들어 Group 칸에 넣는다.
///   패널 Canvas는 Sort Order를 제일 높게 (조준선, HUD보다 위에 그려지게).
/// </summary>
public class LoadingScreen : MonoBehaviour
{
    [Header("로딩 화면")]
    [Tooltip("로딩 패널 루트의 CanvasGroup (전체 화면을 덮는 패널). 평소엔 Alpha 0")]
    public CanvasGroup group;

    [Tooltip("상태 글자 (예: CONNECTING...) — 점이 하나씩 늘었다 줄었다")]
    public TMP_Text statusText;
    public string connectingText = "CONNECTING";
    public string readyText = "BOARDING";

    [Tooltip("빙글빙글 도는 아이콘 (없으면 비워두기)")]
    public RectTransform spinner;
    public float spinSpeed = -240f;

    [Tooltip("팁 문구 (없으면 비워두기). 켜질 때마다 랜덤으로 하나")]
    public TMP_Text tipText;
    [TextArea] public string[] tips =
    {
        "숫자가 낮은 쪽이 사출 버튼을 누른다.",
        "OVERLOAD는 이번 라운드 사출 확률을 끌어올린다.",
        "FOLD는 한 게임에 두 번까지만.",
    };

    [Header("시간 (초)")]
    [Tooltip("너무 빨리 꺼지면 깜빡이는 것처럼 보이니 최소 이만큼은 보여준다")]
    public float minShowTime = 1.2f;
    [Tooltip("내 플레이어가 생긴 뒤 조금 더 덮고 있는 시간 (카메라/자리 이동이 끝나도록)")]
    public float holdAfterReady = 0.4f;
    public float fadeInTime = 0.15f;
    public float fadeOutTime = 0.4f;
    [Tooltip("이 시간이 지나도 안 끝나면 그냥 닫는다 (무한 로딩 방지)")]
    public float timeout = 20f;

    [Header("로딩 중 숨길 것 (조준선 등) — 없으면 비워두기")]
    public GameObject[] hideWhileLoading;

    enum State { Hidden, FadingIn, Showing, FadingOut }
    State state = State.Hidden;
    float shownAt;          // 켜진 시각
    float readyAt = -1f;    // 내 플레이어가 생긴 시각
    float dotTimer;
    int dots;


    void Awake()
    {
        SetAlpha(0f);
        SetBlock(false);
    }

    void Update()
    {
        bool inRoom = RelayManager.Instance != null && RelayManager.Instance.isInRoom;
        bool ready = GameClient.Local != null;

        switch (state)
        {
            case State.Hidden:
                if (inRoom && !ready) Show();
                break;

            case State.FadingIn:
            case State.Showing:
                Animate();

                if (!inRoom) { HideNow(); break; }                  // 접속 실패 / 나감 → 바로 닫기

                if (ready && readyAt < 0f)
                {
                    readyAt = Time.unscaledTime;
                    if (statusText != null) statusText.text = readyText;
                }

                float elapsed = Time.unscaledTime - shownAt;
                bool doneWaiting = readyAt >= 0f
                                   && elapsed >= minShowTime
                                   && Time.unscaledTime - readyAt >= holdAfterReady;

                if (doneWaiting || elapsed >= timeout) state = State.FadingOut;

                if (state == State.FadingIn)
                {
                    float a = group != null ? group.alpha : 1f;
                    a = fadeInTime > 0f ? a + Time.unscaledDeltaTime / fadeInTime : 1f;
                    SetAlpha(a);
                    if (a >= 1f) state = State.Showing;
                }
                break;

            case State.FadingOut:
                Animate();
                float alpha = group != null ? group.alpha : 0f;
                alpha = fadeOutTime > 0f ? alpha - Time.unscaledDeltaTime / fadeOutTime : 0f;
                SetAlpha(alpha);
                if (alpha <= 0f) HideNow();
                break;
        }
    }

    void Show()
    {
        state = State.FadingIn;
        shownAt = Time.unscaledTime;
        readyAt = -1f;
        dots = 0; dotTimer = 0f;

        if (statusText != null) statusText.text = connectingText;
        if (tipText != null && tips != null && tips.Length > 0)
            tipText.text = tips[Random.Range(0, tips.Length)];

        SetBlock(true);
        SetHidden(true);
    }

    void HideNow()
    {
        state = State.Hidden;
        SetAlpha(0f);
        SetBlock(false);
        SetHidden(false);
    }

    /// <summary>점 애니메이션 + 스피너 회전</summary>
    void Animate()
    {
        if (spinner != null) spinner.Rotate(0f, 0f, spinSpeed * Time.unscaledDeltaTime);

        if (statusText == null || readyAt >= 0f) return;    // 준비 끝나면 점 애니메이션 멈춤
        dotTimer += Time.unscaledDeltaTime;
        if (dotTimer >= 0.35f)
        {
            dotTimer = 0f;
            dots = (dots + 1) % 4;                          // "" → "." → ".." → "..."
            statusText.text = connectingText + new string('.', dots);
        }
    }

    void SetAlpha(float a)
    {
        if (group != null) group.alpha = Mathf.Clamp01(a);
    }

    /// <summary>로딩 중엔 뒤의 UI(로비 버튼 등)를 못 누르게 막는다</summary>
    void SetBlock(bool on)
    {
        if (group == null) return;
        group.blocksRaycasts = on;
        group.interactable = on;
    }

    bool[] wasActive;   // 숨기기 전에 켜져 있던 것만 다시 켜기 위해 기억

    void SetHidden(bool hide)
    {
        if (hideWhileLoading == null) return;

        if (hide)
        {
            wasActive = new bool[hideWhileLoading.Length];
            for (int i = 0; i < hideWhileLoading.Length; i++)
            {
                var go = hideWhileLoading[i];
                if (go == null) continue;
                wasActive[i] = go.activeSelf;
                go.SetActive(false);
            }
        }
        else if (wasActive != null)
        {
            for (int i = 0; i < hideWhileLoading.Length && i < wasActive.Length; i++)
                if (hideWhileLoading[i] != null && wasActive[i]) hideWhileLoading[i].SetActive(true);
            wasActive = null;
        }
    }
}
