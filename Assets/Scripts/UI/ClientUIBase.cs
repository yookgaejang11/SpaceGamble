using UnityEngine;

public abstract class ClientUIBase : MonoBehaviour
{
    protected GameClient client;

    protected virtual void OnEnable()
    {
        GameClient.OnLocalClientReady += Bind;
        GameClient.OnLocalClientLost += HandleLost;
        if (GameClient.Local != null) Bind(GameClient.Local);
    }

    protected virtual void OnDisable()
    {
        GameClient.OnLocalClientReady -= Bind;
        GameClient.OnLocalClientLost -= HandleLost;
        Unbind();
    }

    void HandleLost(GameClient c)
    {
        if (client != c) return;
        Unbind();
        OnClientLost();
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

    /// <summary>방을 나가서 클라이언트가 사라졌을 때 — 화면을 초기 상태로 되돌린다</summary>
    protected virtual void OnClientLost() { }
}
