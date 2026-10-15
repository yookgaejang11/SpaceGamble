using UnityEngine;

/// <summary>
/// 로비 카메라의 AudioListener를 "다른 귀가 없을 때만" 켠다.
///
/// 왜 필요한가
///   귀(AudioListener)는 씬에 딱 하나만 켜져 있어야 한다.
///     2개면 → "There are 2 audio listeners" 경고
///     0개면 → "There are no audio listeners" 경고 + 소리가 아예 안 들림
///   방에 들어가면 내 플레이어 카메라의 귀가 켜지므로 로비 귀는 꺼야 하고,
///   어떤 이유로든 플레이어 귀가 없으면 로비 귀가 대신 켜져서 소리가 끊기지 않게 한다.
///
/// [원래] 방 안(GameClient.Local 있음)이면 무조건 껐다
///        → 플레이어 프리팹의 귀가 꺼져 있으면 귀가 0개가 돼서 소리가 안 들렸다
/// [변경] "나 말고 켜진 귀가 있으면 끄고, 없으면 켠다"
///
/// 붙이는 곳
///   씬의 로비 카메라(Main Camera) — AudioListener가 붙은 그 오브젝트.
///   이 오브젝트는 항상 켜져 있어야 한다 (꺼지면 다시 켤 수 없음).
/// </summary>
[RequireComponent(typeof(AudioListener))]
public class LobbyAudioListener : MonoBehaviour
{
    [Tooltip("다른 귀가 있는지 확인하는 간격(초). 매 프레임 찾으면 낭비라 조금씩 쉰다")]
    public float checkInterval = 0.2f;

    AudioListener listener;
    float timer;

    void Awake()
    {
        listener = GetComponent<AudioListener>();
    }

    void Update()
    {
        // [원래]
        // bool inRoom = GameClient.Local != null;
        // if (listener.enabled == inRoom)
        //     listener.enabled = !inRoom;

        // [변경] 나 말고 켜진 귀가 있나?
        timer -= Time.unscaledDeltaTime;
        if (timer > 0f) return;
        timer = checkInterval;

        bool otherEar = false;
        foreach (var l in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
        {
            if (l != listener && l.isActiveAndEnabled) { otherEar = true; break; }
        }

        bool want = !otherEar;              // 다른 귀가 없으면 내가 듣는다
        if (listener.enabled != want)
            listener.enabled = want;
    }
}