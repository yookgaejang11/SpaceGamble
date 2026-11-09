using System.Collections.Generic;
using UnityEngine;

public class ColorSwitch : ClientUIBase
{
    [Header("기준")]
    [Tooltip("이 확률 이상이면 위험 색")]
    public int threshold = 50;

    [Tooltip("색 전환 속도. 0이면 즉시")]
    public float fadeSpeed = 2f;

    [Tooltip("체크하면 0~100%에 걸쳐 계속 변한다 (threshold 무시)")]
    public bool continuous;

    [Header("테이블 에미션")]
    public Renderer[] emissionRenenders;
    public string emissionProperty = "_EmissionColor";
    [ColorUsage(true, true)] public Color emissionNormal = new Color(0f, 0.78f, 1f, 1f);
    [ColorUsage(true, true)] public Color emissionDanger = new Color(2f, 0.26f, 0.16f, 1f);

    [Header("홀로그램 색")]
    public Renderer[] hologramRenenders;
    public string edgeProperty = "_EdgeColor";
    [ColorUsage(true, true)] public Color edgeNormal = new Color(0f, 0.78f, 1f, 1f);
    [ColorUsage(true, true)] public Color edgeDanger = new Color(1f, 0.15f, 0.1f, 1f);

    Material[] emissionMats;
    Material[] hologramMats;

    int per;            
    int shownPer;       
    float blend;
    float lastApplied = -1f;

    void Awake()
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
        client.OnChangedPhase += OnChangedPhase;
    }

    protected override void Unsubscribe()
    {
        client.OnPerChanged -= OnPerChanged;
        client.OnChangedPhase -= OnChangedPhase;
    }

    void OnPerChanged(int before, int cur) => per = cur;

    // 라운드가 시작될 때만 표시용 값을 당겨온다.
    // 덕분에 Result 연출에서 확률이 변해도 색은 다음 라운드까지 그대로다.
    void OnChangedPhase(GamePhase phase)
    {
        if (phase == GamePhase.RoundStart)
            shownPer = per;
    }

    void Update()
    {
        if (client == null) return;

        float target = continuous
            ? Mathf.Clamp01(shownPer / 100f)
            : (shownPer >= threshold ? 1f : 0f);

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