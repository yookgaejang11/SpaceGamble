using UnityEngine;

/// <summary>
/// GO / FOLD / OVERLOAD 버튼을 "조준점 + 좌클릭"으로 누르게 해주는 스크립트.
///
/// 왜 필요한가
///   1인칭이라 커서가 잠겨 있으면 유니티 UI Button은 클릭을 받지 못한다.
///   그래서 PressTarget처럼 화면 정중앙에서 레이를 쏴서 직접 판정한다.
///
/// 붙이는 곳
///   selectUI 안의 각 버튼 오브젝트(GoButton / FoldButton / OverloadButton)에 하나씩.
///   같은 오브젝트에 BoxCollider를 붙인다 (레이가 맞을 판).
///
/// 언제 눌리나
///   내 자리 + talkTime + ESC 메뉴가 닫혀 있을 때만.
///   FOLD는 남은 횟수가 있을 때만.
///   selectUI가 꺼져 있으면 이 스크립트도 같이 꺼지므로 자동으로 안 눌린다.
/// </summary>
public class ChoiceTarget : ClientUIBase
{
    [Header("이 버튼이 누구 자리 것인지 (0 또는 1) / 어떤 선택인지")]
    public int seatIndex;
    public Choice choice;

    [Header("클릭 판정")]
    [Tooltip("비워두면 같은 오브젝트의 Collider를 쓴다")]
    public Collider hitCollider;
    public float maxDistance = 6f;
    [Tooltip("버튼만 있는 레이어로 지정 권장 (내 몸 콜라이더에 막히지 않게)")]
    public LayerMask hitMask = ~0;
    [Tooltip("비워두면 Camera.main")]
    public Camera cam;

    [Header("조준했을 때 표시")]
    [Tooltip("조준 중에만 켤 오브젝트 (테두리 등). 없으면 비워두기")]
    public GameObject hoverMark;
    [Tooltip("조준 중 크기 배율. 1이면 변화 없음")]
    public float hoverScale = 1.08f;

    [Header("디버그")]
    [Tooltip("체크하면 좌클릭할 때마다 '왜 눌렸는지/안 눌렸는지'를 콘솔에 찍는다")]
    public bool debugLog = true;

    Vector3 baseScale;
    bool hovering;


    // ────────────────────────── 준비 ──────────────────────────

    void Awake()
    {
        baseScale = transform.localScale;
        if (hitCollider == null) hitCollider = GetComponent<Collider>();
    }

    // 이 스크립트는 이벤트를 들을 필요가 없다. 매 프레임 상태를 직접 읽는다.
    protected override void Subscribe() { }
    protected override void Unsubscribe() { }

    protected override void OnDisable()
    {
        base.OnDisable();
        SetHover(false);    // 꺼질 때 커진 상태로 남지 않게
    }


    // ────────────────────────── 지금 누를 수 있나 ──────────────────────────

    bool IsMine => client != null && client.myIndex.Value == seatIndex;

    bool CanUse =>
        IsMine
        && client.curPhase == GamePhase.talkTime
        && !PauseMenu.IsOpen
        && (choice != Choice.Fold || client.foldLeft > 0);


    // ────────────────────────── 조준 / 클릭 ──────────────────────────

    void Update()
    {
        if (debugLog && Input.GetMouseButtonDown(0))
            LogDiagnosis();

        if (!CanUse)
        {
            SetHover(false);
            return;
        }

        bool over = IsAimedAt();
        SetHover(over);

        if (over && Input.GetMouseButtonDown(0))
            Send();
    }

    void Send()
    {
        switch (choice)
        {
            case Choice.Go: client.RequestGo(); break;
            case Choice.Fold: client.RequestFold(); break;
            case Choice.Overload: client.RequestOverload(); break;
        }
        // 선택 강조는 SeatPanel이 OnMyChoiceChanged를 받아서 처리한다
    }

    /// <summary>
    /// 조준에 쓸 카메라.
    ///   1) Inspector의 cam  2) 내 플레이어 프리팹 안의 켜진 카메라  3) Camera.main
    /// Camera.main만 쓰면 씬에 MainCamera 태그 카메라(로비 카메라 등)가 남아 있을 때 엉뚱한 걸 집는다.
    /// </summary>
    Camera GetAimCamera()
    {
        if (cam != null) return cam;

        if (client != null)
        {
            foreach (var c in client.GetComponentsInChildren<Camera>())
                if (c.isActiveAndEnabled) return c;
        }
        return Camera.main;
    }

    bool IsAimedAt()
    {
        if (hitCollider == null) return false;

        Camera c = GetAimCamera();
        if (c == null) return false;

        // 커서가 잠겨 있으면 화면 정중앙(조준점), 풀려 있으면 마우스 위치
        Vector3 screenPoint = HeadLookSync.CursorFree
            ? Input.mousePosition
            : new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);

        Ray ray = c.ScreenPointToRay(screenPoint);

        if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance, hitMask,
                             QueryTriggerInteraction.Collide))
            return false;

        // 가장 먼저 맞은 게 이 버튼이어야 한다 (다른 버튼/물체에 가려지면 무시)
        return hit.collider == hitCollider;
    }

    /// <summary>클릭 순간의 상태를 전부 찍는다. 원인을 찾으면 debugLog를 끈다.</summary>
    void LogDiagnosis()
    {
        string who = $"[ChoiceTarget {name} seat{seatIndex} {choice}]";

        if (client == null) { Debug.Log($"{who} client 없음 (내 플레이어 연결 안 됨)"); return; }

        Camera c = GetAimCamera();
        string camName = (c != null) ? c.name : "NULL";

        string hitInfo = "아무것도 안 맞음";
        if (c != null)
        {
            Vector3 sp = HeadLookSync.CursorFree
                ? Input.mousePosition
                : new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
            Ray ray = c.ScreenPointToRay(sp);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, hitMask, QueryTriggerInteraction.Collide))
                hitInfo = $"'{hit.collider.name}' (거리 {hit.distance:F2}, 레이어 {LayerMask.LayerToName(hit.collider.gameObject.layer)})";
        }

        Debug.Log($"{who} myIndex={client.myIndex.Value} IsMine={IsMine} phase={client.curPhase} " +
                  $"pause={PauseMenu.IsOpen} foldLeft={client.foldLeft} | " +
                  $"camera={camName} | 맞은 것={hitInfo} | 내 콜라이더={(hitCollider != null ? hitCollider.name : "NULL")} | maxDistance={maxDistance}");
    }

    void SetHover(bool on)
    {
        if (hovering == on) return;
        hovering = on;

        if (hoverMark != null) hoverMark.SetActive(on);
        transform.localScale = on ? baseScale * hoverScale : baseScale;
    }
}
