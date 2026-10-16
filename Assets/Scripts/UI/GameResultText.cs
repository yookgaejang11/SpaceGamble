using TMPro;
using UnityEngine;

/// <summary>
/// 게임이 끝났을 때 승패 텍스트 그룹을 보여준다.
///
///   이긴 사람 화면 : winMessage / winColor
///   진 사람 화면   : loseMessage / loseColor
///
/// GameOver가 되자마자 띄우지 않고, CenterHologram의 처형 타임라인이 끝난 뒤
/// (CenterHologram.EndGameReady) 띄운다. 값은 각자 자기 기준(내가 이겼나)으로 온다.
///
/// 붙이는 곳
///   항상 켜져 있는 오브젝트에 붙인다. resultGroup은 그 오브젝트의 "자식"이어야 한다.
///   홀로그램처럼 앞면/뒷면 텍스트가 따로 있으면 resultTexts에 전부 넣는다.
/// </summary>
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


    // GameOver가 아닌 페이즈(재시작, 대기 등)에서는 숨긴다
    void HandlePhase(GamePhase phase)
    {
        if (phase != GamePhase.GameOver) Hide();
    }

    // 처형 타임라인이 끝났다는 신호 (win = 내가 이겼나)
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
