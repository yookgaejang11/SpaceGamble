using UnityEngine;

/// <summary>
/// 로비 카메라의 AudioListener를 "방 밖에서만" 켠다.
///
/// 왜 필요한가
///   방에 들어가면 내 플레이어 카메라에도 AudioListener가 있어서,
///   로비 카메라 것까지 켜져 있으면 "There are 2 audio listeners" 경고가 뜬다.
///
/// 붙이는 곳
///   씬의 로비 카메라(Main Camera) — AudioListener가 붙은 그 오브젝트.
///   이 오브젝트는 항상 켜져 있어야 한다 (꺼지면 다시 켤 수 없음).
/// </summary>
[RequireComponent(typeof(AudioListener))]
public class LobbyAudioListener : MonoBehaviour
{
    AudioListener listener;

    void Awake()
    {
        listener = GetComponent<AudioListener>();
    }

    void Update()
    {
        // 내 플레이어가 있으면(방 안) 끄고, 없으면(로비) 켠다
        bool inRoom = GameClient.Local != null;
        if (listener.enabled == inRoom)
            listener.enabled = !inRoom;
    }
}
