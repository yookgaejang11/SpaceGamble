using System;
using UnityEngine;

/// <summary>
/// 우주인 lever_pull 클립에 찍어 둔 애니메이션 이벤트를 받아서 "지금 레버 당겨!"라고 알리는 스크립트.
///
/// 붙이는 곳
///   플레이어 프리팹 안, 우주인 "Animator가 붙은 바로 그 오브젝트".
///   (애니메이션 이벤트는 Animator와 같은 오브젝트에 있는 스크립트 함수만 부를 수 있다)
///
/// 클립에 찍을 이벤트
///   lever_pull 의 "레버가 움직이기 시작해야 하는 프레임" → Function: OnLeverPull
///
/// 듣는 쪽: CenterHologram (자리 번호와 함께 받아서 그 자리 레버를 움직인다)
/// </summary>
public class AstronautAnimEvents : MonoBehaviour
{
    /// <summary>레버를 당기는 순간 (자리 번호)</summary>
    public static event Action<int> LeverPulled;

    [Tooltip("체크하면 이벤트가 불릴 때마다 콘솔에 찍는다 (제대로 찍혔는지 확인용)")]
    public bool debugLog = false;

    GameClient owner;

    /// <summary>애니메이션 이벤트가 부르는 함수. 이름을 바꾸면 클립 이벤트에서 다시 골라야 한다</summary>
    public void OnLeverPull()
    {
        if (owner == null) owner = GetComponentInParent<GameClient>();
        if (owner == null || !owner.IsSpawned) return;

        int seat = owner.myIndex.Value;
        if (debugLog) Debug.Log($"[AstronautAnimEvents] OnLeverPull 자리={seat}");
        LeverPulled?.Invoke(seat);
    }
}
