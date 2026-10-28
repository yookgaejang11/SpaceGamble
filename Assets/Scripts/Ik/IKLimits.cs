// Assets/Scripts/IKLimits.cs  ← Editor 폴더 말고 일반 스크립트 폴더에 넣을 것
// Animation Rigging의 Two Bone IK / Chain IK에 없는 기능을 보충:
//   1) 도달 거리 제한  - 타겟이 팔/다리 길이 밖으로 못 나가게
//   2) 굽힘 각도 제한  - 팔꿈치/무릎 굽힘 범위, 어깨/고관절 회전 범위, 손가락 마디 각도
//   3) 따라오기        - 힌트/타겟이 몸(쇄골, 골반, 손 등)을 따라 움직이게
using UnityEngine;

[ExecuteAlways]
[DefaultExecutionOrder(10000)] // 다른 스크립트가 타겟을 옮긴 뒤에 제한 적용
public class IKLimits : MonoBehaviour
{
    public enum Kind { TwoBone, Chain }
    public Kind kind;

    [Header("체인 (자동 세팅됨)")]
    public Transform root;
    public Transform mid;     // Two Bone 전용
    public Transform tip;
    public Transform target;
    public Transform hint;    // Two Bone 전용

    [Header("1. 도달 거리 제한")]
    public bool clampReach = true;
    [Tooltip("팔/다리 전체 길이 대비 최대 거리. 1이면 완전히 쭉 폄")]
    [Range(0.5f, 1f)] public float maxReach = 0.98f;

    [Header("2-a. 팔꿈치/무릎 굽힘 범위 (Two Bone, 0° = 쭉 폄)")]
    public bool limitBend = true;
    [Range(0f, 179f)] public float minBend = 0f;
    [Range(0f, 179f)] public float maxBend = 150f;

    [Header("2-b. 어깨/고관절 회전 범위 (Two Bone, 플레이 중에만)")]
    public bool limitRootSwing = true;
    [Range(0f, 180f)] public float maxRootSwing = 110f;
    [Tooltip("허용 범위(원뿔)의 중심을 기본 포즈에서 얼마나 돌릴지. 부모 본 기준 Euler")]
    public Vector3 swingCenterOffset;

    [Header("2-c. 마디별 최대 각도 (Chain, 플레이 중에만)")]
    public bool limitChainJoints = true;
    [Range(0f, 180f)] public float maxJointAngle = 100f;

    [Header("3. 따라오기")]
    public bool hintFollows = true;
    public Transform hintFollowBone;
    public bool targetFollows = false;
    public Transform targetFollowBone;

    [Header("기본(Rest) 포즈 - 우클릭 메뉴로 다시 저장 가능")]
    public Quaternion rootRestLocal = Quaternion.identity;
    public Quaternion[] chainRestLocal;

    [SerializeField, HideInInspector] Vector3 hintOffset;
    [SerializeField, HideInInspector] Vector3 targetOffsetPos;
    [SerializeField, HideInInspector] Quaternion targetOffsetRot = Quaternion.identity;

    Transform[] chain;
    bool initialized;
    Vector3 lastHintPos, lastTargetPos;
    Quaternion lastTargetRot;

    void OnEnable()
    {
        BuildChain();
        initialized = false;
    }

    void OnValidate()
    {
        if (maxBend < minBend) maxBend = minBend;
    }

    bool Valid => kind == Kind.TwoBone ? (root && mid && tip) : (root && tip);

    void Update()
    {
        if (!Valid) return;

        Follow();
        if (target) ClampTarget();

        if (hint) lastHintPos = hint.position;
        if (target) { lastTargetPos = target.position; lastTargetRot = target.rotation; }
        initialized = true;
    }

    void LateUpdate()
    {
        // 본 회전을 직접 고치는 건 플레이 중에만 (에디트 모드에서 하면 모델 포즈가 영구로 바뀜)
        if (!Application.isPlaying || !Valid) return;

        if (kind == Kind.TwoBone && limitRootSwing)
            LimitSwing(root, rootRestLocal, mid.localPosition, maxRootSwing, Quaternion.Euler(swingCenterOffset));

        if (kind == Kind.Chain && limitChainJoints && chain != null && chainRestLocal != null
            && chainRestLocal.Length == chain.Length)
        {
            for (int i = 0; i < chain.Length; i++)
                LimitAngle(chain[i], chainRestLocal[i], maxJointAngle);
        }
    }

    // ───────────── 3. 따라오기 ─────────────
    // 누가 직접 옮겼으면(씬에서 드래그, 게임 코드) → 그 위치를 새 오프셋으로 저장
    // 안 옮겼으면 → 따라갈 본 기준 오프셋 위치로 이동
    void Follow()
    {
        if (hintFollows && hint && hintFollowBone)
        {
            bool moved = !initialized || (hint.position - lastHintPos).sqrMagnitude > 1e-10f;
            if (moved) hintOffset = hintFollowBone.InverseTransformPoint(hint.position);
            else hint.position = hintFollowBone.TransformPoint(hintOffset);
        }

        if (targetFollows && target && targetFollowBone)
        {
            bool moved = !initialized
                || (target.position - lastTargetPos).sqrMagnitude > 1e-10f
                || Quaternion.Angle(target.rotation, lastTargetRot) > 0.01f;
            if (moved)
            {
                targetOffsetPos = targetFollowBone.InverseTransformPoint(target.position);
                targetOffsetRot = Quaternion.Inverse(targetFollowBone.rotation) * target.rotation;
            }
            else
            {
                target.SetPositionAndRotation(targetFollowBone.TransformPoint(targetOffsetPos),
                                              targetFollowBone.rotation * targetOffsetRot);
            }
        }
    }

    // ───────────── 1 + 2-a. 거리 제한 ─────────────
    // Two Bone IK에서 팔꿈치 각도는 "어깨~타겟 거리"로만 결정됨
    // → 거리 범위를 막으면 굽힘 각도 범위도 정확히 막힘
    void ClampTarget()
    {
        float full;
        float dMin = 0f;

        if (kind == Kind.TwoBone)
        {
            float a = Vector3.Distance(root.position, mid.position);
            float b = Vector3.Distance(mid.position, tip.position);
            full = a + b;
            if (limitBend)
            {
                full = Mathf.Min(full, BendToDistance(a, b, minBend));
                dMin = BendToDistance(a, b, maxBend);
            }
        }
        else
        {
            full = ChainLength();
        }

        float dMax = clampReach ? full * maxReach : full;
        if (!clampReach && !limitBend) return;

        Vector3 from = root.position;
        Vector3 v = target.position - from;
        float d = v.magnitude;
        if (d < 1e-5f)
        {
            v = tip.position - from;
            d = v.magnitude;
            if (d < 1e-5f) return;
            target.position = from + v / d * Mathf.Max(dMin, 1e-4f);
            return;
        }

        float c = Mathf.Clamp(d, dMin, dMax);
        if (Mathf.Abs(c - d) > 1e-6f)
            target.position = from + v / d * c;
    }

    static float BendToDistance(float a, float b, float bendDeg)
    {
        float interior = (180f - bendDeg) * Mathf.Deg2Rad; // 0° 굽힘 = 내각 180°
        return Mathf.Sqrt(Mathf.Max(0f, a * a + b * b - 2f * a * b * Mathf.Cos(interior)));
    }

    float ChainLength()
    {
        float len = 0f;
        for (var t = tip; t != null && t != root; t = t.parent)
            len += Vector3.Distance(t.position, t.parent.position);
        return len;
    }

    // ───────────── 2-b, 2-c. 회전 제한 ─────────────
    static void LimitSwing(Transform bone, Quaternion rest, Vector3 axisLocal, float max, Quaternion centerOffset)
    {
        if (axisLocal.sqrMagnitude < 1e-8f) return;
        axisLocal.Normalize();
        Vector3 center = centerOffset * (rest * axisLocal);  // 부모 공간 기준 허용 원뿔 중심
        Vector3 cur = bone.localRotation * axisLocal;        // 현재 본 방향
        if (Vector3.Angle(center, cur) <= max) return;

        Vector3 clamped = Vector3.RotateTowards(center, cur, max * Mathf.Deg2Rad, 0f);
        bone.localRotation = Quaternion.FromToRotation(cur, clamped) * bone.localRotation;
    }

    static void LimitAngle(Transform bone, Quaternion rest, float max)
    {
        if (Quaternion.Angle(rest, bone.localRotation) > max)
            bone.localRotation = Quaternion.RotateTowards(rest, bone.localRotation, max);
    }

    // ───────────── 유틸 ─────────────
    void BuildChain()
    {
        chain = null;
        if (!root || !tip) return;
        var list = new System.Collections.Generic.List<Transform>();
        for (var t = tip.parent; t != null; t = t.parent)
        {
            list.Add(t);
            if (t == root) break;
        }
        if (list.Count == 0 || list[list.Count - 1] != root) return;
        list.Reverse();
        chain = list.ToArray(); // root ~ tip 직전까지 (tip 회전은 IK가 타겟 회전으로 맞춤)
    }

    [ContextMenu("현재 포즈를 기본(Rest) 포즈로 저장")]
    public void CaptureRestPose()
    {
        if (root) rootRestLocal = root.localRotation;
        BuildChain();
        if (chain != null)
        {
            chainRestLocal = new Quaternion[chain.Length];
            for (int i = 0; i < chain.Length; i++) chainRestLocal[i] = chain[i].localRotation;
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!Valid) return;
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.6f);
        if (kind == Kind.TwoBone)
        {
            float a = Vector3.Distance(root.position, mid.position);
            float b = Vector3.Distance(mid.position, tip.position);
            float dMax = (a + b) * (clampReach ? maxReach : 1f);
            if (limitBend) dMax = Mathf.Min(dMax, BendToDistance(a, b, minBend));
            Gizmos.DrawWireSphere(root.position, dMax);
            if (limitBend)
            {
                Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.6f);
                Gizmos.DrawWireSphere(root.position, BendToDistance(a, b, maxBend));
            }
        }
        else
        {
            Gizmos.DrawWireSphere(root.position, ChainLength() * (clampReach ? maxReach : 1f));
        }
    }
#endif
}
