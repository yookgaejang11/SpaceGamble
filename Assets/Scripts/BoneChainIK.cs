using UnityEngine;

/// <summary>
/// 인스펙터에 Transform을 순서대로(루트 -> 끝) 몇 개든 드래그해서 넣으면
/// CCD(Cyclic Coordinate Descent) 알고리즘으로 IK를 풀어주는 범용 스크립트.
/// 손가락 3마디, 팔 3~4관절, 레버 손잡이 등 체인 길이에 상관없이 그대로 재사용 가능.
///
/// 사용법:
/// 1) 이 스크립트를 아무 오브젝트에나 붙인다 (보통 손/팔의 루트 근처)
/// 2) Bones 배열에 관절들을 루트 -> 끝 순서로 드래그해서 채운다
///    예: [UpperArm, Forearm, Hand]  또는  [Finger_01, Finger_02, Finger_03]
/// 3) Target에 도달하고 싶은 목표 위치(빈 오브젝트)를 지정한다
/// 4) Mode를 IK로 바꾸면 자동으로 Target을 향해 구부러지고,
///    FK로 바꾸면 스크립트가 손을 떼고 애니메이션이 직접 관절을 움직이게 둔다
/// </summary>
public class BoneChainIK : MonoBehaviour
{
    public enum ChainMode { FK, IK }

    [System.Serializable]
    public class JointLimit
    {
        [Tooltip("이 관절의 로컬 회전 제한을 켤지 여부")]
        public bool enabled = false;

        [Tooltip("로컬 오일러 각도 최소값 (X,Y,Z)")]
        public Vector3 minAngles = new Vector3(-45f, -45f, -45f);

        [Tooltip("로컬 오일러 각도 최대값 (X,Y,Z)")]
        public Vector3 maxAngles = new Vector3(45f, 45f, 45f);
    }

    [Header("체인 구성 (루트 -> 끝 순서로 드래그)")]
    public Transform[] bones;

    [Tooltip("bones와 같은 순서/개수로 채워짐. 각 관절의 회전 제한을 개별 설정")]
    public JointLimit[] jointLimits;

    [Header("IK 목표")]
    public Transform target;

    [Tooltip("팔꿈치/무릎처럼 특정 방향으로 굽혀야 자연스러운 관절에 사용. 없으면 비워둬도 됨")]
    public Transform poleHint;

    [Header("모드")]
    public ChainMode mode = ChainMode.FK;

    [Header("IK 풀이 설정")]
    [Range(1, 30)] public int iterations = 10;
    [Range(0.0001f, 0.01f)] public float tolerance = 0.001f;
    [Range(0f, 1f)] public float poleWeight = 0.5f;

    [Header("거리 제한 (Reach Clamp)")]
    [Tooltip("타겟이 체인이 닿을 수 있는 최대 거리보다 멀 때, 그 한계 지점으로 당겨서 계산할지 여부")]
    public bool clampToReach = true;

    void OnValidate()
    {
        // bones 배열 크기가 바뀌면 jointLimits도 같이 맞춰줌 (인스펙터 편의용)
        if (bones == null) return;
        if (jointLimits == null || jointLimits.Length != bones.Length)
        {
            var resized = new JointLimit[bones.Length];
            for (int i = 0; i < bones.Length; i++)
                resized[i] = (jointLimits != null && i < jointLimits.Length) ? jointLimits[i] : new JointLimit();
            jointLimits = resized;
        }
    }

    void LateUpdate()
    {
        if (mode == ChainMode.FK) return;           // FK 모드면 관여하지 않음(애니메이션이 직접 제어)
        if (bones == null || bones.Length < 2 || target == null) return;

        SolveCCD();
    }

    float GetChainLength()
    {
        float total = 0f;
        for (int i = 0; i < bones.Length - 1; i++)
            total += Vector3.Distance(bones[i].position, bones[i + 1].position);
        return total;
    }

    void SolveCCD()
    {
        Vector3 effectiveTarget = target.position;

        // 거리 제한: 타겟이 체인 최대 길이보다 멀면 그 한계 지점으로 당김
        if (clampToReach)
        {
            Vector3 root = bones[0].position;
            float maxReach = GetChainLength();
            Vector3 toTarget = effectiveTarget - root;
            if (toTarget.magnitude > maxReach)
                effectiveTarget = root + toTarget.normalized * maxReach;
        }

        for (int iter = 0; iter < iterations; iter++)
        {
            Transform end = bones[bones.Length - 1];

            if (Vector3.Distance(end.position, effectiveTarget) < tolerance)
                break;

            // 끝 관절 바로 앞부터 루트 방향으로 거꾸로 훑으면서 회전 보정
            for (int i = bones.Length - 2; i >= 0; i--)
            {
                Transform joint = bones[i];
                Vector3 toEnd = end.position - joint.position;
                Vector3 toTarget = effectiveTarget - joint.position;

                if (toEnd.sqrMagnitude < 1e-8f || toTarget.sqrMagnitude < 1e-8f)
                    continue;

                Quaternion rotFix = Quaternion.FromToRotation(toEnd, toTarget);
                joint.rotation = rotFix * joint.rotation;

                // 팔꿈치 방향 힌트가 있으면 그쪽으로 살짝 당겨서 부자연스러운 꺾임 방지
                if (poleHint != null && i < bones.Length - 1)
                    ApplyPoleHint(joint, i);

                // 회전 제한 적용
                ApplyJointLimit(i);
            }
        }
    }

    void ApplyJointLimit(int index)
    {
        if (jointLimits == null || index >= jointLimits.Length) return;
        var limit = jointLimits[index];
        if (!limit.enabled) return;

        Transform joint = bones[index];
        Vector3 euler = joint.localEulerAngles;

        euler.x = ClampAngle(euler.x, limit.minAngles.x, limit.maxAngles.x);
        euler.y = ClampAngle(euler.y, limit.minAngles.y, limit.maxAngles.y);
        euler.z = ClampAngle(euler.z, limit.minAngles.z, limit.maxAngles.z);

        joint.localEulerAngles = euler;
    }

    // Unity의 localEulerAngles는 0~360 범위라 -180~180 기준의 min/max와 비교하려면 보정 필요
    float ClampAngle(float angle, float min, float max)
    {
        if (angle > 180f) angle -= 360f;
        angle = Mathf.Clamp(angle, min, max);
        if (angle < 0f) angle += 360f;
        return angle;
    }

    void ApplyPoleHint(Transform joint, int index)
    {
        Transform next = bones[index + 1];
        Vector3 currentBendDir = (next.position - joint.position).normalized;
        Vector3 desiredBendDir = (poleHint.position - joint.position).normalized;

        Quaternion hintFix = Quaternion.FromToRotation(currentBendDir, desiredBendDir);
        Quaternion blended = Quaternion.Slerp(Quaternion.identity, hintFix, poleWeight * 0.3f);
        joint.rotation = blended * joint.rotation;
    }

    // 코드에서 즉시 모드 전환할 때 쓰는 헬퍼 (레버 잡는 순간 IK로, 놓으면 FK로 등)
    public void SetMode(ChainMode newMode)
    {
        mode = newMode;
    }

    void OnDrawGizmosSelected()
    {
        if (bones == null || bones.Length == 0) return;

        // 1) 본 체인 연결선 (초록) + 각 관절 위치(구)
        Gizmos.color = Color.green;
        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i] == null) continue;

            Gizmos.DrawSphere(bones[i].position, 0.015f);

            if (i < bones.Length - 1 && bones[i + 1] != null)
                Gizmos.DrawLine(bones[i].position, bones[i + 1].position);
        }

        // 2) 루트/끝 관절은 색을 다르게 해서 방향 구분
        if (bones[0] != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(bones[0].position, 0.02f);   // 루트
        }
        Transform lastBone = bones[bones.Length - 1];
        if (lastBone != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(lastBone.position, 0.02f);   // 끝(엔드 이펙터)
        }

        // 3) IK 타겟 (빨강)
        if (target != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(target.position, 0.025f);
            if (lastBone != null)
                Gizmos.DrawLine(lastBone.position, target.position);
        }

        // 4) 폴 힌트 (마젠타) - 팔꿈치가 굽는 방향
        if (poleHint != null && bones.Length > 1)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(poleHint.position, 0.02f);
            int midIndex = bones.Length / 2;
            if (bones[midIndex] != null)
                Gizmos.DrawLine(bones[midIndex].position, poleHint.position);
        }

        // 5) 도달 가능 범위 (노랑 와이어 구) - 타겟이 이 밖으로 나가면 clampToReach가 당겨줌
        if (clampToReach && bones[0] != null && bones.Length > 1)
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.5f);
            Gizmos.DrawWireSphere(bones[0].position, GetChainLength());
        }
    }
}
