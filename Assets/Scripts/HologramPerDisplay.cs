using UnityEngine;
using TMPro;
using Unity.Netcode;

/// <summary>
/// 홀로그램에 "현재 사출 확률"만 띄운다.  예) 23%
///
/// 붙이는 곳
///   홀로그램 Canvas (또는 그 안의 아무 오브젝트). NetworkObject는 붙이지 않는다.
///
/// 필요한 것
///   없음. 동료의 GameClient / GameManager 원본 그대로 동작한다.
///
/// 처음 확률
///   서버는 게임을 시작할 때 확률을 basicPer(10%)로 되돌리지만 클라이언트에 알려주지 않는다.
///   그래서 "1라운드 시작 = 새 게임 = basicPer"로 직접 맞춘다.
///   GamePer 에셋은 GameManager가 쓰는 것과 같은 원본이라 값이 똑같다.
/// </summary>
public class HologramPerDisplay : MonoBehaviour
{
    [Tooltip("확률을 띄울 글자 (Text - TextMeshPro, UI용)")]
    public TMP_Text perText;

    [Tooltip("Project 창의 GamePer 에셋 (처음 확률 basicPer를 읽음)")]
    public GamePer rule;

    GameClient client;   // 지금 연결된 내 플레이어 (방 밖이면 null)


    // ────────────────────────── 내 플레이어 찾기 ──────────────────────────

    void OnEnable()
    {
        Show(null);
    }

    void OnDisable()
    {
        Unlink();
    }

    /// <summary>
    /// 매 프레임 "내 플레이어가 바뀌었나" 확인한다.
    /// GameClient는 접속해야 생기는 오브젝트라 Inspector로 미리 연결할 수 없기 때문.
    /// </summary>
    void Update()
    {
        GameClient mine = FindMine();

        // ReferenceEquals: 파괴된 Unity 오브젝트는 "== null"이 true라서,
        // 그냥 ==로 비교하면 "사라짐"을 놓친다. 진짜 같은 객체인지만 본다.
        if (ReferenceEquals(mine, client)) return;

        Unlink();                       // 이전 것 정리 (방 나감 / 재입장)
        if (mine != null) Link(mine);   // 새로 연결 (방 입장)
    }

    /// <summary>NGO가 접속 후 채워주는 "내 플레이어 오브젝트"에서 GameClient를 꺼낸다</summary>
    static GameClient FindMine()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening) return null;          // 방 밖

        var obj = nm.LocalClient != null ? nm.LocalClient.PlayerObject : null;
        if (obj == null || !obj.IsSpawned) return null;          // 아직 안 생김

        return obj.GetComponent<GameClient>();
    }

    void Link(GameClient c)
    {
        client = c;
        client.OnRoundChanged += HandleRound;
        client.OnPerChanged += HandlePer;
        client.OnChangedPhase += HandlePhase;
        Show(null);
    }

    void Unlink()
    {
        if (!ReferenceEquals(client, null))
        {
            // 오브젝트가 파괴됐어도 C# 이벤트 해제는 안전하다
            client.OnRoundChanged -= HandleRound;
            client.OnPerChanged -= HandlePer;
            client.OnChangedPhase -= HandlePhase;
        }
        client = null;
        Show(null);
    }


    // ────────────────────────── 이벤트 처리 ──────────────────────────

    // 1라운드 = 새 게임 시작 → 처음 확률
    void HandleRound(int round)
    {
        if (round == 1 && rule != null) Show(rule.basicPer);
    }

    // 버튼을 누른 뒤 서버가 보내주는 새 확률
    void HandlePer(int before, int after) => Show(after);

    // 대기 중엔 확률을 모르므로 지운다
    void HandlePhase(GamePhase phase)
    {
        if (phase == GamePhase.waiting) Show(null);
    }


    // ────────────────────────── 표시 ──────────────────────────

    /// <summary>숫자가 있으면 "23%", 없으면(null) "--%"</summary>
    void Show(int? per)
    {
        if (perText == null) return;
        perText.text = per.HasValue ? $"{per.Value}%" : "--%";
    }
}
