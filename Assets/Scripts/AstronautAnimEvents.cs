using System;
using UnityEngine;

public class AstronautAnimEvents : MonoBehaviour
{
    /// <summary>레버를 당기는 순간</summary>
    public static event Action<int> LeverPulled;

    /// <summary>버튼을 꾹 누르는 순간</summary>
    public static event Action ButtonPressed;

    /// <summary>사출이 발동되는 순간</summary>
    public static event Action EjectTriggered;

    /// <summary>카메라 쉐이크가 발동되는 순간 (지속시간, 세기)</summary>
    public static event Action<float, float> CameraShakeTriggered;

    [Header("기본 쉐이크 설정 (파라미터 미입력 시 적용)")]
    [Tooltip("카메라 쉐이크 기본 지속 시간(초)")]
    public float defaultShakeDuration = 0.35f;

    [Tooltip("카메라 쉐이크 기본 진동 세기(각도 기준)")]
    public float defaultShakeIntensity = 2.5f;

    [Header("디버그")]
    [Tooltip("체크하면 이벤트가 불릴 때마다 콘솔에 찍는다")]
    public bool debugLog = false;

    [Tooltip("플레이어 프리팹(GameClient) 밖에 있는 우주인용 자리 번호 (-1이면 부모에서 자동 산출)")]
    public int seatOverride = -1;

    GameClient owner;

    private int GetSeat()
    {
        if (seatOverride >= 0) return seatOverride;
        if (owner == null) owner = GetComponentInParent<GameClient>();
        return (owner != null && owner.IsSpawned) ? owner.myIndex.Value : -1;
    }

    /// <summary>
    /// lever_pull 클립 애니메이션 이벤트용
    /// </summary>
    public void OnLeverPull()
    {
        int seat = GetSeat();
        if (debugLog) Debug.Log($"[AstronautAnimEvents] OnLeverPull (자리={seat})");
        if (seat >= 0) LeverPulled?.Invoke(seat);
    }

    /// <summary>
    /// button_push 클립 애니메이션 이벤트용
    /// </summary>
    public void OnButtonPush()
    {
        if (debugLog) Debug.Log("[AstronautAnimEvents] OnButtonPush");
        ButtonPressed?.Invoke();
    }

    /// <summary>
    /// 사출 시작 애니메이션 이벤트용
    /// </summary>
    public void OnEject()
    {
        if (debugLog) Debug.Log("[AstronautAnimEvents] OnEject");
        EjectTriggered?.Invoke();
    }

    // ────────────────────────── 카메라 쉐이크 이벤트 ──────────────────────────

    /// <summary>
    /// 1. 파라미터 없이 호출 시: 인스펙터에 설정된 defaultShakeDuration / defaultShakeIntensity 적용
    /// </summary>
    public void OnCameraShake()
    {
        TriggerCameraShake(defaultShakeDuration, defaultShakeIntensity);
    }

    /// <summary>
    /// 2. 애니메이션 이벤트의 Float 값으로 세기(Intensity)만 넘겨줄 때 사용
    /// 예: Float에 3.5 입력 ➔ 0.35초 동안 3.5 세기로 쉐이크
    /// </summary>
    public void OnCameraShakeWithIntensity(float intensity)
    {
        TriggerCameraShake(defaultShakeDuration, intensity);
    }

    public void OnCameraShakeWithDuration(float duration)
    {
        TriggerCameraShake(duration, defaultShakeIntensity);
    }

    /// <summary>
    /// 3. 지속 시간과 세기를 모두 직접 지정해서 호출할 때
    /// </summary>
    public void TriggerCameraShake(float duration, float intensity)
    {
        if (debugLog) Debug.Log($"[AstronautAnimEvents] OnCameraShake (시간={duration}초, 세기={intensity})");

        // 로컬 플레이어(내 화면)의 HeadLookSync 카메라 쉐이크 호출
        if (HeadLookSync.Instance != null)
        {
            HeadLookSync.Instance.ShakeCamera(duration, intensity);
        }

        CameraShakeTriggered?.Invoke(duration, intensity);
    }
}