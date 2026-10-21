using TMPro;
using UnityEngine;

public class GameResultText : ClientUIBase
{
    [Header("승패 텍스트 그룹 (처형 타임라인이 끝난 뒤 켜짐)")]
    public GameObject resultGroup;

    [Tooltip("문구와 색을 넣을 텍스트들 (앞면 / 뒷면 등). TextMeshPro, TextMeshProUGUI 모두 가능")]
    public TMP_Text[] resultTexts;

    [Header("이겼을 때")]
    public string winMessage = "YOU SURVIVED";
    public Color winColor = new Color(0.6f, 0.9f, 1f);

    [Header("졌을 때")]
    public string loseMessage = "YOU WERE EJECTED";
    public Color loseColor = new Color(1f, 0.4f, 0.3f);


    protected override void OnEnable()
    {
        base.OnEnable();
        if (client == null) Hide();     // 연결 전에는 항상 숨김
    }

    protected override void Subscribe()
    {
        client.OnChangedPhase += HandlePhase;
        CenterHologram.EndGameReady += HandleEndGameReady;
    }

    protected override void Unsubscribe()
    {
        client.OnChangedPhase -= HandlePhase;
        CenterHologram.EndGameReady -= HandleEndGameReady;
    }

    protected override void Refresh()
    {
        HandlePhase(client.curPhase);
    }

    protected override void OnClientLost()
    {
        Hide();
    }


    void HandlePhase(GamePhase phase)
    {
        if (phase != GamePhase.GameOver) Hide();
    }
    void HandleEndGameReady(bool win)
    {
        if (client == null || client.curPhase != GamePhase.GameOver) return;
        Show(win);
    }

    void Show(bool won)
    {
        string msg = won ? winMessage : loseMessage;
        Color col = won ? winColor : loseColor;

        if (resultTexts != null)
        {
            foreach (var t in resultTexts)
            {
                if (t == null) continue;
                t.text = msg;
                t.color = col;
            }
        }

        if (resultGroup != null) resultGroup.SetActive(true);
    }

    void Hide()
    {
        if (resultGroup != null) resultGroup.SetActive(false);
    }
}
