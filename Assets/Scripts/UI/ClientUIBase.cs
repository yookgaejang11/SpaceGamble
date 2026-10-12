using UnityEngine;

/// <summary>
/// GameClient.Local을 구독하는 UI 스크립트들의 공통 베이스.
///
/// GameClient는 접속 후 스폰되므로 인스펙터로 미리 물릴 수 없다.
/// "이미 생겼으면 바로 / 아직이면 알림 받아서" 두 경로를 여기서 한 번만 처리한다.
///
/// 상속받는 쪽은 Subscribe / Unsubscribe만 채우면 된다.
/// </summary>
public abstract class ClientUIBase : MonoBehaviour
{
    protected GameClient client;

    protected virtual void OnEnable()
    {
        GameClient.OnLocalClientReady += Bind;

        if (GameClient.Local != null)
            Bind(GameClient.Local);
    }

    protected virtual void OnDisable()
    {
        GameClient.OnLocalClientReady -= Bind;
        Unbind();
    }

    void Bind(GameClient c)
    {
        if (client == c) return;        // 중복 구독 방지
        Unbind();

        client = c;
        Subscribe();
        Refresh();                      // 현재 상태를 즉시 화면에 반영
    }

    void Unbind()
    {
        if (client == null) return;

        Unsubscribe();
        client = null;
    }

    protected abstract void Subscribe();
    protected abstract void Unsubscribe();

    /// <summary>연결 직후 한 번 — 이미 진행 중인 상태를 그려준다</summary>
    protected virtual void Refresh() { }
}
