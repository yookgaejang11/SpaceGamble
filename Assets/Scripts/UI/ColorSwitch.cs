using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ColorSwitch : ClientUIBase
{
    [Header("기준")]
    [Tooltip("이 확률 이상이면 위험 색")]
    public int threshold = 50;

    public float fadeSpeed;

    [Tooltip("체크하면 0~100%에 걸쳐 계속 변한다 (threshold 무시)")]
    public bool continuous;

    [Header("테이블 에미션")]
    public Renderer[] emissionRenenders;
    public string emissionProperty = "_EmissionColor";
    [ColorUsage(true, true)] public Color emissionNormal = new Color();
    [ColorUsage(true, true)] public Color emissionDanger = new Color(1f, 0.15f, 0.1f);

    [Header("홀로그램 색")]
    public Renderer[] hologramRenenders;
    public string edgeProperty = "_EdgeColor";
    [ColorUsage(true, true)] public Color edgeNormal = new Color();
    [ColorUsage(true, true)] public Color edgeDanger = new Color();

    Material[] emissionMats;
    Material[] hologramMats;

    int per;
    float blend;
    float lastApplied = -1f;

    private void Awake()
    {
        emissionMats = Collect(emissionRenenders);
        hologramMats = Collect(hologramRenenders);
    }

    static Material[] Collect(Renderer[] rs)
    {
        var list = new List<Material>();
        if (rs != null)
            foreach (var r in rs)
                if (r != null) list.AddRange(r.materials);
        return list.ToArray();
    }

    protected override void Subscribe()
    {
        client.OnPerChanged += OnPerChanged;
        client.OnChangedPhase += OnChangedPhase; // ★ 페이즈 변경 이벤트 추가 구독
    }

    protected override void Unsubscribe()
    {
        client.OnPerChanged -= OnPerChanged;
        client.OnChangedPhase -= OnChangedPhase; // ★ 이벤트 해제
    }

    void OnPerChanged(int before, int cur) => per = cur;

    // ★ 페이즈 변경 시 필요한 처리 (필요시 사용)
    void OnChangedPhase(GamePhase phase)
    {
        // RoundStart 진입 시 즉시 갱신하고 싶다면 추가 로직 배치 가능
    }

    void Update()
    {
        // client가 없거나 연결이 끊긴 경우 예외 방지
        if (client == null) return;

        // ★ GamePhase가 RoundStart일 때만 per 기준 색상(1f 또는 연속 비율)을 타겟으로 잡고,
        // 그 외 페이즈일 때는 기본 색상(0f)을 타겟으로 잡습니다.
        float target = 0f;

        if (client.curPhase == GamePhase.RoundStart)
        {
            target = continuous
                ? Mathf.Clamp01(per / 100f)
                : (per >= threshold ? 1f : 0f);
        }

        blend = fadeSpeed <= 0f
            ? target
            : Mathf.MoveTowards(blend, target, fadeSpeed * Time.deltaTime);

        if (Mathf.Approximately(blend, lastApplied)) return;
        lastApplied = blend;

        Color e = Color.Lerp(emissionNormal, emissionDanger, blend);
        Color g = Color.Lerp(edgeNormal, edgeDanger, blend);

        foreach (var m in emissionMats) if (m != null) m.SetColor(emissionProperty, e);
        foreach (var m in hologramMats) if (m != null) m.SetColor(edgeProperty, g);
    }
}