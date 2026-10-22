using Unity.Netcode;
using UnityEngine;

public class HeadLookSync : NetworkBehaviour
{
    public static bool CursorFree { get; private set; }

    static bool lockEnabled = true;     // lockCursor 설정의 정적 복사본

    /// <summary>
    /// 커서를 풀거나 다시 잠근다. 풀려 있는 동안 시점 회전은 멈춘다.
    /// lockCursor가 꺼져 있으면 커서는 건드리지 않는다.
    /// </summary>
    public static void SetCursorFree(bool free)
    {
        CursorFree = free;

        if (!lockEnabled) return;

        Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = free;
    }

    Animator animator;
    Quaternion headBaseLocal;
    [Header("참조")]
    [Tooltip("모델의 머리 본")]
    public Transform head;

    [Tooltip("1인칭 카메라 (Owner만 활성화됨)")]
    public Transform camRig;

    [Tooltip("Owner가 아닐 때 끌 카메라/오디오리스너")]
    public Camera ownerCamera;
    public AudioListener ownerAudioListener;

    [Header("조작")]
    public float mouseSensitivity = 2.0f;
    [Tooltip("true면 마우스 커서를 잠근다")]
    public bool lockCursor = true;

    [Header("보정")]
    [Tooltip("받은 각도를 따라가는 속도. 클수록 빠르고 딱딱함")]
    public float smooth = 12f;

    // (yaw, pitch) — 캐릭터 기준 상대 각도
    readonly NetworkVariable<Vector2> netLook = new NetworkVariable<Vector2>(
        Vector2.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    Vector2 localLook;      // Owner의 입력값
    Vector2 shownLook;      // 실제로 화면에 적용 중인 값 (보간 결과)
    float sendTimer;

    GamePer rule;

    public override void OnNetworkSpawn()
    {
        rule = GameManager.Instance != null ? GameManager.Instance.gameRule : null;
        animator = GetComponentInChildren<Animator>();
        if (head != null) headBaseLocal = head.localRotation;
        if (!IsOwner)
        {
            if (ownerCamera != null) ownerCamera.enabled = false;
            if (ownerAudioListener != null) ownerAudioListener.enabled = false;
            return;
        }

        lockEnabled = lockCursor;
        CursorFree = false;
        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        if (ownerAudioListener != null)
        {
            foreach (var l in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (l != ownerAudioListener) l.enabled = false;

            ownerAudioListener.enabled = true;
        }

    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;

        // 방을 나가면 커서를 돌려준다 (로비 UI를 눌러야 하므로)
        CursorFree = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        if (!IsOwner) return;

        if (CursorFree)
        {
            SendIfDue();
            return;
        }

        float yawLimit = rule != null ? rule.lookYawLimit : 70f;
        float pitchLimit = rule != null ? rule.lookPitchLimit : 45f;

        localLook.x += Input.GetAxisRaw("Mouse X") * mouseSensitivity;
        localLook.y += Input.GetAxisRaw("Mouse Y") * mouseSensitivity;

        localLook.x = Mathf.Clamp(localLook.x, -yawLimit, yawLimit);
        localLook.y = Mathf.Clamp(localLook.y, -pitchLimit, pitchLimit);

        SendIfDue();
    }

    /// <summary>
    /// 매 프레임 보내면 낭비라 초당 N회로 제한한다.
    /// </summary>
    void SendIfDue()
    {
        float rate = rule != null ? rule.lookSendRate : 15f;
        if (rate <= 0f) return;

        sendTimer -= Time.deltaTime;
        if (sendTimer > 0f) return;

        sendTimer = 1f / rate;

        // 거의 안 움직였으면 보내지 않는다
        if ((netLook.Value - localLook).sqrMagnitude < 0.01f) return;

        netLook.Value = localLook;
    }

    /// <summary>
    /// 애니메이터가 본을 다 쓴 뒤에 덮어써야 하므로 LateUpdate.
    /// </summary>
    void LateUpdate()
    {
        Vector2 target = IsOwner ? localLook : netLook.Value;

        shownLook = Vector2.Lerp(shownLook, target, 1f - Mathf.Exp(-smooth * Time.deltaTime));

        // 카메라는 Owner만
        if (IsOwner && camRig != null)
            camRig.localRotation = Quaternion.Euler(-shownLook.y, shownLook.x, 0f);

        if (head == null) return;
        head.localRotation = headBaseLocal;
        // 애니메이션이 만든 머리 포즈 위에, 캐릭터 기준 축으로 회전을 더한다.
        // (본의 로컬 축 방향은 모델마다 달라서 localRotation 대입은 쓰면 안 된다)
        head.rotation =
            Quaternion.AngleAxis(shownLook.x, transform.up) *
            Quaternion.AngleAxis(-shownLook.y, transform.right) *
            head.rotation;
    }

    /// <summary>
    /// 라운드 리셋 등으로 시선을 정면으로 되돌릴 때 호출.
    /// </summary>
    public void ResetLook()
    {
        localLook = Vector2.zero;
        shownLook = Vector2.zero;
        if (IsOwner) netLook.Value = Vector2.zero;
    }
}
