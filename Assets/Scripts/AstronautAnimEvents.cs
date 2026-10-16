using System;
using UnityEngine;

public class AstronautAnimEvents : MonoBehaviour
{
    /// <summary>레버를 당기는 순간 (자리 번호)</summary>
    public static event Action<int> LeverPulled;

    [Tooltip("체크하면 이벤트가 불릴 때마다 콘솔에 찍는다 (제대로 찍혔는지 확인용)")]
    public bool debugLog = false;

    [Tooltip("플레이어 프리팹(GameClient) 밖에 있는 우주인이면 자리 번호를 직접 적는다 (0 / 1). " +
             "-1이면 부모 GameClient에서 자동으로 읽는다")]
    public int seatOverride = -1;

    GameClient owner;

    /// <summary>애니메이션 이벤트가 부르는 함수. 이름을 바꾸면 클립 이벤트에서 다시 골라야 한다</summary>
    public void OnLeverPull()
    {
        int seat = seatOverride;
        if (seat < 0)
        {
            if (owner == null) owner = GetComponentInParent<GameClient>();
            if (owner == null || !owner.IsSpawned)
            {
                if (debugLog) Debug.Log($"[AstronautAnimEvents] {name}: 부모에 GameClient가 없음 → Seat Override에 자리 번호를 적어주세요");
                return;
            }
            seat = owner.myIndex.Value;
        }

        if (debugLog) Debug.Log($"[AstronautAnimEvents] OnLeverPull 자리={seat}");
        LeverPulled?.Invoke(seat);
    }
}
