using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class CrtPopup : MonoBehaviour
{
    [Header("켜질 때 (초)")]
    [Tooltip("점 → 가로줄로 퍼지는 시간")]
    public float openLineTime = 0.08f;
    [Tooltip("가로줄 → 화면 전체로 세로가 펼쳐지는 시간")]
    public float openExpandTime = 0.14f;

    [Header("꺼질 때 (초)")]
    public float closeLineTime = 0.08f;
    public float closeDotTime = 0.06f;

    [Header("모양")]
    [Tooltip("가로줄 상태일 때 세로 두께 비율 (원래 크기 대비)")]
    public float lineThickness = 0.02f;
    [Tooltip("점 상태일 때 가로 비율")]
    public float dotWidth = 0.02f;

    [Header("번쩍임 / 깜빡임 — 없으면 비워두기")]
    [Tooltip("UI 맨 위에 덮어 둔 흰색 Image. 켜지는 순간 하얗게 번쩍였다가 사라진다 (Raycast Target 끄기)")]
    public Graphic flashImage;
    public float flashTime = 0.25f;
    [Tooltip("UI 루트의 CanvasGroup. 켜진 직후 몇 번 깜빡인다")]
    public CanvasGroup canvasGroup;
    public int flickerCount = 2;

    [Header("사운드 id (SoundManager) — 비우면 소리 없음")]
    // 소리는 SoundManager가 관리. SoundManager 'Sounds' 목록의 id만 적는다
    public string onClip = "tv_on";
    public string offClip = "tv_off";

    Vector3 baseScale;
    bool captured;          // 원래 크기를 기억했는가
    bool shown;             // 지금 켜진(켜지는 중) 상태인가
    Sequence seq;

    /// <summary>
    /// 원래 크기 기억. Awake는 오브젝트가 꺼져 있으면 안 불리기 때문에
    /// Show/Hide가 처음 불릴 때 직접 챙긴다.
    /// </summary>
    void Capture()
    {
        if (captured) return;
        baseScale = transform.localScale;
        captured = true;
    }

    public void Show()
    {
        Capture();
        if (shown) return;          // 이미 켜졌으면 다시 재생하지 않는다 (같은 페이즈가 두 번 와도 안전)
        shown = true;

        seq?.Kill();
        gameObject.SetActive(true);

        // 시작 모양 = 가운데 작은 점
        transform.localScale = new Vector3(baseScale.x * dotWidth, baseScale.y * lineThickness, baseScale.z);
        if (flashImage != null) SetAlpha(flashImage, 1f);
        if (canvasGroup != null) canvasGroup.alpha = 1f;
        Play(onClip);

        seq = DOTween.Sequence().SetLink(gameObject);

        // 1) 점 → 가로줄 (가로만 쫙)
        seq.Append(transform.DOScaleX(baseScale.x, openLineTime).SetEase(Ease.OutQuad));
        // 2) 가로줄 → 전체 (세로가 퐉, 살짝 넘쳤다가 돌아옴)
        seq.Append(transform.DOScaleY(baseScale.y, openExpandTime).SetEase(Ease.OutBack));

        // 3) 흰 번쩍임이 빠지면서 화면이 드러남 (펼쳐지는 것과 동시에 시작)
        if (flashImage != null)
            seq.Insert(openLineTime, flashImage.DOFade(0f, flashTime).SetEase(Ease.OutQuad));

        // 4) 깜빡깜빡 (브라운관이 안정되는 느낌)
        if (canvasGroup != null && flickerCount > 0)
        {
            for (int i = 0; i < flickerCount; i++)
            {
                seq.Append(canvasGroup.DOFade(0.35f, 0.03f));
                seq.Append(canvasGroup.DOFade(1f, 0.04f));
            }
        }
    }

    public void Hide()
    {
        Capture();
        if (!shown && !gameObject.activeSelf) return;   // 이미 꺼져 있음
        shown = false;

        seq?.Kill();
        if (!gameObject.activeSelf) { ResetLook(); return; }

        if (flashImage != null) SetAlpha(flashImage, 0f);
        if (canvasGroup != null) canvasGroup.alpha = 1f;
        Play(offClip);

        seq = DOTween.Sequence().SetLink(gameObject);

        // 1) 세로로 납작 → 가로줄 (번쩍이며)
        seq.Append(transform.DOScaleY(baseScale.y * lineThickness, closeLineTime).SetEase(Ease.InQuad));
        if (flashImage != null)
            seq.Join(flashImage.DOFade(1f, closeLineTime));
        // 2) 가로줄 → 점
        seq.Append(transform.DOScaleX(baseScale.x * dotWidth, closeDotTime).SetEase(Ease.InQuad));
        // 3) 끝나면 끄고 원래 모양으로 되돌려 둔다 (다음에 켤 때를 위해)
        seq.OnComplete(() =>
        {
            gameObject.SetActive(false);
            ResetLook();
        });
    }

    /// <summary>애니메이션 없이 바로 끈다 (방을 나갔을 때 등)</summary>
    public void HideImmediate()
    {
        Capture();
        shown = false;
        seq?.Kill();
        gameObject.SetActive(false);
        ResetLook();
    }


    void ResetLook()
    {
        transform.localScale = baseScale;
        if (flashImage != null) SetAlpha(flashImage, 0f);
        if (canvasGroup != null) canvasGroup.alpha = 1f;
    }

    static void SetAlpha(Graphic g, float a)
    {
        Color c = g.color;
        c.a = a;
        g.color = c;
    }

    void Play(string id)                                    // SoundManager로
    {
        if (!string.IsNullOrEmpty(id)) SoundManager.Play(id);
    }

    void OnDestroy()
    {
        seq?.Kill();
    }
}
