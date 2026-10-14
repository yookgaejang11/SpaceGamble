using TMPro;
using UnityEngine;

/// <summary>
/// "누르기(Press)" 입력용 3D 오브젝트.
///
/// 내 자리에서, 내가 누를 차례(Press 페이즈 + 지목됨)일 때만 활성화된다.
///   - 오브젝트에 조준(마우스)을 갖다 대면 hoverText가 나타난다
///   - 그 상태로 좌클릭하면 client.RequestPress()를 보낸다
///
/// 조준 방식
///   커서가 잠겨 있을 때(1인칭)  : 화면 정중앙 = 십자선 기준으로 레이캐스트
///   커서를 풀었을 때(Tab 누름)  : 실제 마우스 위치 기준으로 레이캐스트
///
/// 붙이는 곳
///   클릭될 오브젝트 "옆"의 빈 오브젝트(또는 상위 오브젝트)에 붙인다.
///   이 스크립트가 붙은 오브젝트는 꺼지면 안 된다 (꺼지면 구독이 풀린다).
///   보였다/숨겼다 하고 싶은 모델은 visualRoot에 따로 물린다.
///
/// 필요한 것
///   hitCollider : 클릭을 받을 Collider (Is Trigger여도 됨)
///   씬에 Collider가 있어야 레이캐스트에 잡힌다
/// </summary>
public class PressTarget : ClientUIBase
{
    [Header("이 오브젝트가 누구 자리 것인지 (0 또는 1)")]
    public int seatIndex;

    [Header("클릭 대상")]
    [Tooltip("비워두면 hitMask 레이어에 맞은 것 아무거나 인정. 같은 레이어에 Press 오브젝트가 둘 이상이면 여기에 지정")]
    public Collider hitCollider;

    [Tooltip("누를 차례일 때만 보이게 할 모델. 비워두면 항상 보임")]
    public GameObject visualRoot;

    [Tooltip("조준하면 나타날 텍스트. 비워도 됨")]
    public TextMeshPro hoverText;
    public string hoverMessage = "PRESS";

    [Header("조준")]
    public float maxDistance = 6f;
    public LayerMask hitMask = ~0;

    [Tooltip("비워두면 Camera.main")]
    public Camera cam;

    bool myTurnPending;     // OnRoundDecided에서 "내가 누를 차례"로 정해졌는가
    bool armed;             // 지금 실제로 눌릴 수 있는가
    bool hovering;


    bool IsMine => client != null && client.myIndex.Value == seatIndex;


    // ────────────────────────── 구독 ──────────────────────────

    protected override void Subscribe()
    {
        client.OnChangedPhase += HandlePhase;
        client.OnRoundDecided += HandleRoundDecided;
        client.OnOuted += HandleOuted;
    }

    protected override void Unsubscribe()
    {
        client.OnChangedPhase -= HandlePhase;
        client.OnRoundDecided -= HandleRoundDecided;
        client.OnOuted -= HandleOuted;
    }

    protected override void Refresh()
    {
        myTurnPending = false;
        Evaluate();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        myTurnPending = false;
        SetArmed(false);
    }


    // ────────────────────────── 상태 ──────────────────────────

    void HandlePhase(GamePhase phase)
    {
        // 새 라운드가 시작되거나 게임이 끝나면 지목 상태를 지운다
        if (phase == GamePhase.waiting || phase == GamePhase.GameOver ||
            phase == GamePhase.RoundStart || phase == GamePhase.talkTime)
            myTurnPending = false;

        Evaluate();
    }

    void HandleRoundDecided(Result result, bool isMyTurn)
    {
        // draw / fold는 아무도 안 누른다
        bool pressed = (result != Result.draw && result != Result.fold);
        myTurnPending = pressed && isMyTurn;
        Evaluate();
    }

    void HandleOuted(bool outed)
    {
        myTurnPending = false;
        Evaluate();
    }

    /// <summary>
    /// 이벤트 도착 순서(RoundDecided → Press 페이즈 전환 / 그 반대)에 상관없이
    /// "지목됨 && 현재 Press 페이즈 && 내 자리"일 때만 활성화한다.
    /// </summary>
    void Evaluate()
    {
        bool on = client != null && IsMine && myTurnPending
                  && client.curPhase == GamePhase.Press;
        SetArmed(on);
    }

    void SetArmed(bool on)
    {
        armed = on;

        if (visualRoot != null) visualRoot.SetActive(on);
        if (!on) SetHover(false);
    }


    // ────────────────────────── 조준 / 클릭 ──────────────────────────

    void Update()
    {
        if (!armed || client == null) return;

        // ESC 메뉴가 열려 있으면 뒤에 있는 오브젝트를 누르지 않는다
        if (PauseMenu.IsOpen)
        {
            SetHover(false);
            return;
        }

        bool over = IsAimedAt();
        SetHover(over);

        if (over && Input.GetMouseButtonDown(0))
        {
            myTurnPending = false;      // 한 번만 보낸다
            SetArmed(false);
            client.RequestPress();
        }
    }

    // ===== [추가] 조준 카메라 찾기 (ChoiceTarget과 같은 방식) =====
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
    // ===== [추가] 끝 =====

    bool IsAimedAt()
    {
        // [원래] Camera c = (cam != null) ? cam : Camera.main;
        Camera c = GetAimCamera();                                  // [변경]
        if (c == null) return false;

        Vector3 screenPoint = HeadLookSync.CursorFree
            ? Input.mousePosition
            : new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);

        Ray ray = c.ScreenPointToRay(screenPoint);

        if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance, hitMask,
                             QueryTriggerInteraction.Collide))
            return false;

        // hitCollider를 안 정했으면 hitMask 레이어에 맞은 것만으로 인정
        return hitCollider == null || hit.collider == hitCollider;
    }

    void SetHover(bool on)
    {
        if (hovering == on) return;
        hovering = on;

        if (hoverText == null) return;
        hoverText.text = hoverMessage;
        hoverText.gameObject.SetActive(on);
    }
}