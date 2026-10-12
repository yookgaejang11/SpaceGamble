using UnityEngine;
using Unity.Netcode;

/// <summary>
/// 홀로그램(World Space Canvas)을 "내 자리" 쪽으로 돌려서, 양쪽 플레이어 모두 정면으로 보이게 한다.
///
/// 원리
///   이 오브젝트는 네트워크로 동기화하지 않는다 (NetworkObject 붙이지 말 것).
///   그래서 P0 컴퓨터에서는 P0 쪽으로, P1 컴퓨터에서는 P1 쪽으로 각자 돌려도 서로 영향이 없다.
///   Canvas는 하나만 있으면 된다.
///
/// 붙이는 곳
///   홀로그램 Canvas의 부모로 쓸 빈 오브젝트 (예: "HoloPivot"). Scale (1,1,1)
///   구조:  HologramProjector
///           └ HoloPivot      ← 이 스크립트
///              └ Canvas      ← World Space
///
/// 필요한 것
///   없음. 동료의 GameClient / GameManager 원본 그대로 동작한다.
///   GameManager.seats 의 파란 화살표(Z축)가 상대 쪽을 보고 있어야 한다.
/// </summary>
public class HologramFacing : MonoBehaviour
{
    [Tooltip("비워두면 GameManager.seats를 쓴다. 따로 지정하고 싶을 때만 넣기")]
    public Transform[] seats;

    [Tooltip("체크하면 좌우로만 돌린다 (기울어지지 않고 똑바로 서 있음)")]
    public bool yawOnly = true;

    [Tooltip("글자가 뒤집혀 보이면 180으로 바꾸기")]
    public float extraYaw = 0f;

    Quaternion defaultRotation;   // 방 밖에 있을 때 돌아갈 원래 방향
    int appliedIndex = -1;        // 마지막으로 맞춘 자리 번호 (-1 = 아직 안 맞춤)


    void Awake()
    {
        defaultRotation = transform.rotation;
    }

    /// <summary>
    /// 매 프레임 "내 자리 번호"를 확인하고, 바뀌었을 때만 돌린다.
    /// (호스트는 스폰 직후 myIndex가 0이었다가 서버가 진짜 번호를 넣으므로, 계속 확인해야 한다)
    /// </summary>
    void Update()
    {
        int index = FindMyIndex();
        if (index == appliedIndex) return;   // 변화 없음

        appliedIndex = index;

        if (index < 0) transform.rotation = defaultRotation;   // 방 밖 → 원래 방향
        else Apply(index);
    }

    /// <summary>내 자리 번호 (0 또는 1). 방 밖이면 -1</summary>
    static int FindMyIndex()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening) return -1;

        var obj = nm.LocalClient != null ? nm.LocalClient.PlayerObject : null;
        if (obj == null || !obj.IsSpawned) return -1;

        var client = obj.GetComponent<GameClient>();
        if (client == null) return -1;

        return Mathf.Clamp(client.myIndex.Value, 0, 1);
    }

    void Apply(int index)
    {
        // 자리 목록 (Inspector에 없으면 GameManager 것)
        Transform[] list = (seats != null && seats.Length >= 2) ? seats
                         : (GameManager.Instance != null ? GameManager.Instance.seats : null);
        if (list == null || list.Length < 2 || list[index] == null)
        {
            appliedIndex = -2;   // 다음 프레임에 다시 시도
            return;
        }

        // 내가 앉아서 바라보는 방향 = 자리의 앞 방향(파란 화살표)
        Vector3 lookDir = list[index].forward;
        if (yawOnly)
        {
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude < 0.0001f) return;
        }

        // UI 글자는 "보는 사람의 시선 방향 = Canvas의 앞(+Z)"일 때 정방향으로 읽힌다
        // (Billboard.cs와 같은 규칙. LookAt(내 위치)를 쓰면 좌우가 뒤집힌다)
        transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up)
                           * Quaternion.Euler(0f, extraYaw, 0f);
    }
}
