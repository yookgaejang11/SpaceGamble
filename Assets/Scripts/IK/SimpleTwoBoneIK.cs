using UnityEngine;

/// <summary>
/// "뼈 2개 + 끝" 체인을 목표 지점으로 보내는 IK.
/// 팔, 다리, 집게, 로봇 팔 등 2관절 구조면 무엇이든 쓸 수 있다.
///
/// Animation Rigging 패키지가 필요 없다. Humanoid Avatar도 필요 없다.
/// Blender에서 만든 Generic 리그에서 그대로 동작한다.
///
/// 특징
///   · 에디터에서도 계산된다 (미리보기를 켜면 타겟을 끄는 대로 따라온다)
///   · 인스펙터 버튼으로 뼈를 자동으로 찾는다
///   · LateUpdate에서 푼다 — 애니메이터가 포즈를 다 쓴 뒤 그 위에 덮어쓴다
///
/// 붙이는 곳
///   체인마다 하나씩. 보통 캐릭터 밑에 빈 오브젝트를 만들어 붙인다.
///   (한 오브젝트에 여러 개는 못 붙는다)
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class SimpleTwoBoneIK : MonoBehaviour
{
    public enum ChainPreset
    {
        RightArm,   // 윗팔 → 아래팔 → 손
        LeftArm,
        RightLeg,   // 허벅지 → 정강이 → 발
        LeftLeg,
        Custom      // 직접 지정
    }

    [Header("체인 종류 (자동 찾기 기준)")]
    public ChainPreset preset = ChainPreset.RightArm;

    [Tooltip("뼈를 찾기 시작할 오브젝트. 비워두면 최상위 부모부터 찾는다")]
    public Transform searchRoot;

    [Header("뼈 체인 (위에서 아래로)")]
    [Tooltip("윗팔 / 허벅지")]
    public Transform root;
    [Tooltip("아래팔 / 정강이")]
    public Transform mid;
    [Tooltip("손 / 발")]
    public Transform tip;

    [Header("목표")]
    [Tooltip("끝(손·발)이 가야 할 지점")]
    public Transform target;
    [Tooltip("중간 관절(팔꿈치·무릎)이 향할 방향")]
    public Transform hint;

    [Header("적용량")]
    [Range(0f, 1f)]
    [Tooltip("0 = 애니메이션 그대로 / 1 = 목표에 완전히 붙음")]
    public float weight = 0f;

    [Range(0f, 1f)]
    [Tooltip("끝의 회전도 목표에 맞출지")]
    public float rotationWeight = 1f;

    [Range(0f, 1f)]
    public float hintWeight = 1f;

    [Header("에디터")]
    [Tooltip("켜면 Play를 누르지 않아도 씬에서 바로 계산된다")]
    public bool editorPreview = false;

    // 미리보기를 끌 때 되돌리기 위한 원래 포즈
    [SerializeField, HideInInspector] bool hasSnapshot;
    [SerializeField, HideInInspector] Quaternion savedRoot, savedMid, savedTip;


    public bool IsValid => root != null && mid != null && tip != null && target != null;

    /// <summary>중간 관절을 부르는 말 (UI 표시용)</summary>
    public string JointName => IsLeg ? "무릎" : "팔꿈치";
    public bool IsLeg => preset == ChainPreset.RightLeg || preset == ChainPreset.LeftLeg;
    public bool IsRightSide => preset == ChainPreset.RightArm || preset == ChainPreset.RightLeg;


    void LateUpdate()
    {
        if (!Application.isPlaying && !editorPreview) return;
        if (!IsValid || weight <= 0f) return;

        Solve(root, mid, tip, target, hint,
              weight, weight * rotationWeight, weight * hintWeight);
    }


    // ────────────────────────── 미리보기 ──────────────────────────

    public void EnablePreview()
    {
        if (!IsValid) return;
        TakeSnapshot();
        editorPreview = true;
    }

    public void DisablePreview()
    {
        editorPreview = false;
        RestoreSnapshot();
    }

    public void TakeSnapshot()
    {
        if (!IsValid) return;
        savedRoot = root.localRotation;
        savedMid = mid.localRotation;
        savedTip = tip.localRotation;
        hasSnapshot = true;
    }

    public void RestoreSnapshot()
    {
        if (!hasSnapshot || !IsValid) return;
        root.localRotation = savedRoot;
        mid.localRotation = savedMid;
        tip.localRotation = savedTip;
    }


    // ────────────────────────── 자동 설정 ──────────────────────────

    /// <summary>preset에 맞는 뼈 체인을 이름으로 찾는다. 찾으면 true</summary>
    public bool AutoFindBones()
    {
        if (preset == ChainPreset.Custom) return false;

        Transform from = searchRoot != null ? searchRoot : transform.root;

        Transform end = FindEndBone(from, IsRightSide, IsLeg);
        if (end == null) return false;

        Transform m = ParentSkippingTwist(end);
        Transform r = m != null ? ParentSkippingTwist(m) : null;
        if (m == null || r == null) return false;

        tip = end;
        mid = m;
        root = r;
        return true;
    }

    /// <summary>Target과 Hint를 만들어 적당한 자리에 놓는다</summary>
    public void AutoCreateTargetAndHint()
    {
        if (root == null || mid == null || tip == null) return;

        string prefix = preset == ChainPreset.Custom ? gameObject.name : preset.ToString();

        if (target == null)
        {
            var go = new GameObject(prefix + "_Target");
            go.transform.SetParent(transform, false);
            target = go.transform;
        }
        target.SetPositionAndRotation(tip.position, tip.rotation);

        if (hint == null)
        {
            var go = new GameObject(prefix + "_Hint");
            go.transform.SetParent(transform, false);
            hint = go.transform;
        }
        hint.position = CalcHintPosition();
    }

    public void SnapTargetToTip()
    {
        if (target == null || tip == null) return;
        target.SetPositionAndRotation(tip.position, tip.rotation);
    }

    /// <summary>지금 중간 관절이 꺾여 있는 방향으로 Hint 위치를 계산</summary>
    public Vector3 CalcHintPosition()
    {
        Vector3 chord = (root.position + tip.position) * 0.5f;
        Vector3 dir = mid.position - chord;

        if (dir.sqrMagnitude < 1e-8f) dir = -transform.forward;
        dir.Normalize();

        float limbLength = Vector3.Distance(root.position, mid.position)
                         + Vector3.Distance(mid.position, tip.position);

        return mid.position + dir * limbLength * 0.5f;
    }


    // ────────────────────────── 뼈 찾기 ──────────────────────────

    static readonly string[] HandWords = { "hand", "wrist" };
    static readonly string[] FootWords = { "foot", "ankle" };
    static readonly string[] SkipWords =
    {
        "thumb", "index", "middle", "ring", "pinky", "little", "finger",
        "toe", "ball", "_ik", "attach", "socket", "target", "hint"
    };

    static Transform FindEndBone(Transform from, bool rightSide, bool leg)
    {
        string[] want = leg ? FootWords : HandWords;

        Transform best = null;
        int bestDepth = int.MaxValue;

        foreach (var t in from.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.ToLowerInvariant();

            bool match = false;
            foreach (var w in want) if (n.Contains(w)) { match = true; break; }
            if (!match) continue;

            bool skip = false;
            foreach (var w in SkipWords) if (n.Contains(w)) { skip = true; break; }
            if (skip) continue;

            if (!SideMatches(t.name, rightSide)) continue;

            // 손가락/발가락에 속지 않게 가장 얕은 것을 고른다
            int d = 0;
            for (Transform p = t; p.parent != null; p = p.parent) d++;
            if (d < bestDepth) { bestDepth = d; best = t; }
        }
        return best;
    }

    /// <summary>twist / roll 같은 보조 뼈는 건너뛰고 진짜 부모를 찾는다</summary>
    static Transform ParentSkippingTwist(Transform t)
    {
        Transform p = t.parent;
        while (p != null)
        {
            string n = p.name.ToLowerInvariant();
            if (n.Contains("twist") || n.Contains("roll")) { p = p.parent; continue; }
            return p;
        }
        return null;
    }

    /// <summary>이름의 좌우 표기를 본다. "Right"/"Left" 우선, 없으면 끝에 붙은 r/l</summary>
    static bool SideMatches(string raw, bool rightSide)
    {
        string n = raw.ToLowerInvariant();

        bool hasRight = n.Contains("right");
        bool hasLeft = n.Contains("left");
        if (hasRight || hasLeft) return rightSide ? hasRight : hasLeft;

        char want = rightSide ? 'r' : 'l';
        for (int i = n.Length - 1; i >= 0; i--)
        {
            char c = n[i];
            if (char.IsDigit(c)) continue;                              // .001 같은 꼬리표
            if (c == '.' || c == '_' || c == '-' || c == ' ' || c == ':') continue;
            return c == want;
        }
        return false;
    }


    // ────────────────────────── 풀이 ──────────────────────────
    //
    // 두 뼈의 길이는 고정이고 목표까지의 거리만 바뀐다.
    // 코사인 법칙으로 중간 관절 각도를 구해 굽힌 뒤, 뿌리를 돌려 끝을 목표로 보낸다.
    // 마지막으로 뿌리를 목표 축으로 비틀어 중간 관절을 Hint 쪽으로 돌린다.

    public static void Solve(Transform a, Transform b, Transform c,
                             Transform target, Transform hint,
                             float posWeight, float rotWeight, float hintW)
    {
        Vector3 aPos = a.position;
        Vector3 bPos = b.position;
        Vector3 cPos = c.position;

        Vector3 targetPos = Vector3.Lerp(cPos, target.position, posWeight);
        Quaternion targetRot = Quaternion.Lerp(c.rotation, target.rotation, rotWeight);

        Vector3 ab = bPos - aPos;
        Vector3 bc = cPos - bPos;
        Vector3 ac = cPos - aPos;
        Vector3 at = targetPos - aPos;

        float abLen = ab.magnitude;
        float bcLen = bc.magnitude;

        float oldAngle = TriangleAngle(ac.magnitude, abLen, bcLen);
        float newAngle = TriangleAngle(at.magnitude, abLen, bcLen);

        // 굽히는 평면의 법선. 완전히 펴져 있으면 다른 걸로 대체한다
        Vector3 axis = Vector3.Cross(ab, bc);
        if (axis.sqrMagnitude < 1e-8f)
        {
            if (hint != null) axis = Vector3.Cross(hint.position - aPos, bc);
            if (axis.sqrMagnitude < 1e-8f) axis = Vector3.Cross(at, bc);
            if (axis.sqrMagnitude < 1e-8f) axis = Vector3.up;
        }
        axis.Normalize();

        // 1) 중간 관절을 굽힌다
        b.rotation = Quaternion.AngleAxis((oldAngle - newAngle) * Mathf.Rad2Deg, axis) * b.rotation;

        // 2) 뿌리를 돌려 끝을 목표로 보낸다
        ac = c.position - aPos;
        a.rotation = Quaternion.FromToRotation(ac, at) * a.rotation;

        // 3) 중간 관절을 Hint 쪽으로 비튼다
        if (hint != null && hintW > 0f)
        {
            ac = c.position - aPos;
            float acSqr = ac.sqrMagnitude;
            if (acSqr > 1e-8f)
            {
                Vector3 acDir = ac / Mathf.Sqrt(acSqr);
                Vector3 abNow = b.position - aPos;
                Vector3 ah = hint.position - aPos;

                // 목표 축에 수직인 성분만 비교한다
                Vector3 abProj = abNow - acDir * Vector3.Dot(abNow, acDir);
                Vector3 ahProj = ah - acDir * Vector3.Dot(ah, acDir);

                float reach = abLen + bcLen;
                if (abProj.sqrMagnitude > reach * reach * 1e-3f && ahProj.sqrMagnitude > 1e-8f)
                {
                    Quaternion twist = Quaternion.FromToRotation(abProj, ahProj);
                    twist = Quaternion.Slerp(Quaternion.identity, twist, hintW);
                    a.rotation = twist * a.rotation;
                }
            }
        }

        // 4) 끝의 회전
        if (rotWeight > 0f) c.rotation = targetRot;
    }

    /// <summary>코사인 법칙. 변 aLen을 마주 보는 각(라디안)</summary>
    static float TriangleAngle(float aLen, float len1, float len2)
    {
        float denom = 2f * len1 * len2;
        if (denom < 1e-8f) return 0f;
        float cos = Mathf.Clamp((len1 * len1 + len2 * len2 - aLen * aLen) / denom, -1f, 1f);
        return Mathf.Acos(cos);
    }


    // ────────────────────────── 씬 표시 ──────────────────────────

    void OnDrawGizmosSelected()
    {
        if (root == null || mid == null || tip == null) return;

        Gizmos.color = new Color(0.3f, 0.9f, 1f);
        Gizmos.DrawLine(root.position, mid.position);
        Gizmos.DrawLine(mid.position, tip.position);
        Gizmos.DrawWireSphere(root.position, 0.015f);
        Gizmos.DrawWireSphere(mid.position, 0.015f);
        Gizmos.DrawWireSphere(tip.position, 0.015f);

        if (target != null)
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f);
            Gizmos.DrawWireSphere(target.position, 0.02f);
            Gizmos.DrawLine(tip.position, target.position);
        }

        if (hint != null)
        {
            Gizmos.color = new Color(1f, 0.4f, 0.8f);
            Gizmos.DrawWireSphere(hint.position, 0.02f);
            Gizmos.DrawLine(mid.position, hint.position);
        }
    }
}
