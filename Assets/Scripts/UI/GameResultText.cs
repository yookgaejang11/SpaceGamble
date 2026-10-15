using TMPro;
using UnityEngine;

/// <summary>
/// 게임이 끝났을 때(GameOver 페이즈)만 승패 텍스트 그룹을 보여준다.
///
///   이긴 사람 화면 : winMessage / winColor
///   진 사람 화면   : loseMessage / loseColor
///
/// 값은 각자 자기 기준으로 온다 (GameClient.OnGameWin의 bool = "내가 이겼나").
/// 그래서 양쪽 컴퓨터가 서로 다른 문구를 띄우는 게 맞다.
///
/// 붙이는 곳
///   항상 켜져 있는 오브젝트에 붙인다. resultGroup은 그 오브젝트의 "자식"이어야 한다
///   (그룹을 끄면 스크립트도 같이 꺼지기 때문).
///   홀로그램처럼 앞면/뒷면 텍스트가 따로 있으면 resultTexts에 전부 넣는다.
/// </summary>
public class GameResultText : ClientUIBase
{
    [Header("승패 텍스트 그룹 (GameOver에서만 켜짐)")]
    public GameObject resultGroup;

    [Tooltip("문구와 색을 넣을 텍스트들 (앞면 / 뒷면 등). TextMeshPro, TextMeshProUGUI 모두 가능")]
    public TMP_Text[] resultTexts;

    [Header("이겼을 때")]
    public string winMessage = "YOU SURVIVED";
    public Color winColor = new Color(0.6f, 0.9f, 1f);

    [Header("졌을 때")]
    public string loseMessage = "YOU WERE EJECTED";
    public Color loseColor = new Color(1f, 0.4f, 0.3f);

    bool hasResult;     // 이번 게임의 승패를 받았는가
    bool won;


    protected override void OnEnable()
    {
        base.OnEnable();
        if (client == null) Hide();     // 연결 전에는 항상 숨김
    }

    protected override void Subscribe()
    {
        client.OnChangedPhase += HandlePhase;
        client.OnGameWin += HandleGameWin;
    }

    protected override void Unsubscribe()
    {
        client.OnChangedPhase -= HandlePhase;
        client.OnGameWin -= HandleGameWin;
    }

    protected override void Refresh()
    {
        hasResult = false;
        HandlePhase(client.curPhase);
    }

    protected override void OnClientLost()
    {
        hasResult = false;
        Hide();
    }


    // OnGameWin은 GameOver 페이즈보다 먼저 온다. 값만 받아두고 페이즈가 오면 보여준다.
    void HandleGameWin(bool win)
    {
        hasResult = true;
        won = win;

        if (client != null && client.curPhase == GamePhase.GameOver) Show();
    }

    void HandlePhase(GamePhase phase)
    {
        if (phase == GamePhase.GameOver && hasResult)
        {
            Show();
            return;
        }

        // 다른 페이즈(재시작, 대기 등)에서는 숨기고 결과를 지운다
        if (phase != GamePhase.GameOver) hasResult = false;
        Hide();
    }

    void Show()
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
