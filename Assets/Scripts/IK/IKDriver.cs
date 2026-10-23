using System;
using UnityEngine;

/// <summary>
/// 여러 IK 체인을 이름으로 켜고 끄는 스크립트.
///
/// 왜 필요한가
///   Weight를 1로 두면 가만히 있을 때도 손이 목표에 붙어 있다.
///   필요한 구간에만 0 → 1 → 0으로 움직여야 한다.
///
/// 붙이는 곳
///   Animator가 붙은 바로 그 오브젝트.
///   (애니메이션 이벤트는 Animator와 같은 오브젝트의 함수만 부를 수 있다)
///
/// 애니메이션 이벤트에서 쓰는 법
///   Function: IKOn   /  String: rightArm     ← 이벤트 창의 String 칸에 id를 적는다
///   Function: IKOff  /  String: rightArm
///
/// 코드에서 쓰는 법
///   driver.IKOn("rightArm");
///   driver.IKOff("rightArm");
///   driver.AllOff();
/// </summary>
public class IKDriver : MonoBehaviour
{
    [Serializable]
    public class Channel
    {
        [Tooltip("애니메이션 이벤트나 코드에서 부를 이름")]
        public string id = "rightArm";

        public SimpleTwoBoneIK ik;

        [Tooltip("0에서 1까지 올라가는 데 걸리는 시간(초). 짧을수록 딱딱하게 붙는다")]
        public float blendTime = 0.15f;

        [HideInInspector] public float target;
    }

    [Header("체인 목록")]
    public Channel[] channels = new Channel[0];

    [Header("디버그")]
    [Tooltip("체크하면 켜고 끌 때마다 콘솔에 찍는다")]
    public bool debugLog = false;


    void Awake()
    {
        // 에디터 미리보기가 켜진 채로 저장됐어도 여기서 정리된다
        foreach (var c in channels)
        {
            if (c?.ik == null) continue;
            c.ik.editorPreview = false;
            c.ik.weight = 0f;
            c.target = 0f;
        }
    }

    void Update()
    {
        foreach (var c in channels)
        {
            if (c?.ik == null) continue;
            float step = Time.deltaTime / Mathf.Max(c.blendTime, 0.0001f);
            c.ik.weight = Mathf.MoveTowards(c.ik.weight, c.target, step);
        }
    }


    // ────────────────────────── 애니메이션 이벤트 / 코드에서 호출 ──────────────────────────

    /// <summary>해당 체인을 켠다 (부드럽게 1로)</summary>
    public void IKOn(string id) => SetTarget(id, 1f);

    /// <summary>해당 체인을 끈다 (부드럽게 0으로)</summary>
    public void IKOff(string id) => SetTarget(id, 0f);

    /// <summary>전부 끈다. 라운드 리셋이나 재시작에서</summary>
    public void AllOff()
    {
        foreach (var c in channels)
        {
            if (c == null) continue;
            c.target = 0f;
            if (c.ik != null) c.ik.weight = 0f;
        }
    }

    /// <summary>블렌드 없이 즉시 값을 넣는다</summary>
    public void SetWeightImmediate(string id, float w)
    {
        var c = Find(id);
        if (c == null) return;
        c.target = w;
        if (c.ik != null) c.ik.weight = w;
    }

    public float GetWeight(string id)
    {
        var c = Find(id);
        return (c != null && c.ik != null) ? c.ik.weight : 0f;
    }


    void SetTarget(string id, float v)
    {
        var c = Find(id);
        if (c == null)
        {
            Debug.LogWarning($"[IKDriver] '{id}' 라는 체인이 없습니다. Channels 목록의 id를 확인하세요.", this);
            return;
        }
        c.target = v;
        if (debugLog) Debug.Log($"[IKDriver] {id} → {(v > 0f ? "ON" : "OFF")}", this);
    }

    Channel Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (var c in channels)
            if (c != null && c.id == id) return c;
        return null;
    }

    void OnDisable() => AllOff();
}
