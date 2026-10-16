using Unity.Netcode;
using UnityEngine;

public class CheatConsole : NetworkBehaviour
{
    [Header("치트")]
    [Tooltip("끄면 단축키가 아무 일도 안 한다 (최종 빌드에서는 끄기)")]
    public bool enableCheats = true;

    [Tooltip("이 키를 누른 채로 (None이면 Trigger Key만)")]
    public KeyCode holdKey = KeyCode.None;
    [Tooltip("이 키를 누르면 발동")]
    public KeyCode triggerKey = KeyCode.F6;

    [Tooltip("올릴 확률 (%p). 100이면 무조건 100%가 된다")]
    public int addPercent = 100;

    [Header("사운드 id (SoundManager) — 비우면 소리 없음")]
    public string sndCheat = "overload";

    [Tooltip("체크하면 콘솔에 결과를 찍는다")]
    public bool debugLog = true;


    // ────────────────────────── 입력 (내 것만) ──────────────────────────

    void Update()
    {
        if (!enableCheats || !IsOwner || !IsSpawned) return;
        if (PauseMenu.IsOpen) return;
        // Hold Key가 None이면 Trigger Key만 눌러도 발동
        bool held = holdKey == KeyCode.None || Input.GetKey(holdKey);
        if (!held || !Input.GetKeyDown(triggerKey)) return;

        var local = GameClient.Local;
        if (local == null || !CanUse(local.curPhase))
        {
            if (debugLog) Debug.Log("[CheatConsole] 지금은 못 씀 (대기 / 게임 끝에서는 안 됨)");
            return;
        }

        RequestCheatRpc(addPercent);
    }

    static bool CanUse(GamePhase p) => p != GamePhase.waiting && p != GamePhase.GameOver;


    // ────────────────────────── 네트워크 ──────────────────────────

    [Rpc(SendTo.Server)]
    void RequestCheatRpc(int amount)
    {
        var gm = GameManager.Instance;
        if (gm == null || !CanUse(gm.curPhase)) return;     // 서버에서 한 번 더 확인

        int per = gm.CheatAddOutPer(amount);
        if (per < 0) return;                                // 게임 중이 아님

        ShowCheatRpc(per);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void ShowCheatRpc(int per)
    {
        var holo = FindFirstObjectByType<CenterHologram>();
        if (holo != null) holo.ShowCheatPer(per);

        if (!string.IsNullOrEmpty(sndCheat)) SoundManager.Play(sndCheat);
        if (debugLog) Debug.Log($"[CheatConsole] 사출 확률 → {per}%");
    }
}