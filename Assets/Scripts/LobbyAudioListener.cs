using UnityEngine;

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
        // 나 말고 켜진 귀가 있나?
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
