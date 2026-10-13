using TMPro;
using UnityEngine;

/// <summary>
/// Player1 / Player2 보드.
///
/// 양쪽 보드가 같은 내용을 보여주므로 스크립트 하나로 둘 다 처리한다.
/// 배열에 보드 두 개의 텍스트를 각각 넣으면 된다.
///   p1Texts = [위쪽 보드의 Player1 칸, 아래쪽 보드의 Player1 칸]
///   p2Texts = [위쪽 보드의 Player2 칸, 아래쪽 보드의 Player2 칸]
///
/// 내 칸은 서버가 값을 보낸 적이 없다. "??"는 하드코딩이다.
/// (숨겨야 하는 정보는 애초에 클라이언트로 오지 않는다)
/// </summary>
public class SeatBoard : ClientUIBase
{
    [Header("Player1 칸 (보드마다 하나씩)")]
    public TextMeshPro[] p1Texts;

    [Header("Player2 칸 (보드마다 하나씩)")]
    public TextMeshPro[] p2Texts;

    [Header("표시 문자열")]
    public string hiddenMark = "??";
    public string emptyMark = "---";
    public string readyMark = "READY";
    public string notReadyMark = "...";

    [Header("색")]
    public Color normalColor = Color.white;
    public Color readyColor = new Color(0.5f, 1f, 0.6f);
    public Color dimColor = new Color(0.5f, 0.5f, 0.5f);

    int shownCard = 0;      // 마지막으로 받은 상대 카드


    protected override void Subscribe()
    {
        client.OnChangedPhase += HandlePhase;
        client.OnNumberSelected += HandleOpponentCard;
        client.OnReadyChanged += HandleReady;
        client.OnOpponentPresentChanged += HandleOpponentPresent;
    }

    protected override void Unsubscribe()
    {
        client.OnChangedPhase -= HandlePhase;
        client.OnNumberSelected -= HandleOpponentCard;
        client.OnReadyChanged -= HandleReady;
        client.OnOpponentPresentChanged -= HandleOpponentPresent;
    }

    protected override void Refresh()
    {
        HandlePhase(client.curPhase);
    }


    /// <summary>내가 Player1 칸인가</summary>
    bool IAmP1 => client != null && client.myIndex.Value == 0;


    void HandlePhase(GamePhase phase)
    {
        if (phase == GamePhase.waiting || phase == GamePhase.GameOver)
            DrawReady(client.isReady, client.opponentReady, client.opponentPresent);
        else
            DrawCards(shownCard);
    }

    void HandleOpponentCard(int card)
    {
        shownCard = card;
        DrawCards(card);
    }

    void HandleReady(bool mine, bool opponent)
    {
        if (client.curPhase == GamePhase.waiting || client.curPhase == GamePhase.GameOver)
            DrawReady(mine, opponent, client.opponentPresent);
    }

    void HandleOpponentPresent(bool present)
    {
        if (client.curPhase == GamePhase.waiting || client.curPhase == GamePhase.GameOver)
            DrawReady(client.isReady, client.opponentReady, present);
    }


    // ────────────────────────── 그리기 ──────────────────────────

    void DrawCards(int opponentCard)
    {
        string mine = hiddenMark;
        string theirs = opponentCard.ToString();

        Set(IAmP1 ? mine : theirs, IAmP1 ? theirs : mine,
            normalColor, normalColor);
    }

    void DrawReady(bool myReady, bool oppReady, bool oppPresent)
    {
        string mine = myReady ? readyMark : notReadyMark;
        Color mineColor = myReady ? readyColor : dimColor;

        string theirs = !oppPresent ? emptyMark : (oppReady ? readyMark : notReadyMark);
        Color theirsColor = !oppPresent ? dimColor : (oppReady ? readyColor : dimColor);

        Set(IAmP1 ? mine : theirs,
            IAmP1 ? theirs : mine,
            IAmP1 ? mineColor : theirsColor,
            IAmP1 ? theirsColor : mineColor);
    }

    void Set(string p1, string p2, Color c1, Color c2)
    {
        Apply(p1Texts, p1, c1);
        Apply(p2Texts, p2, c2);
    }

    static void Apply(TextMeshPro[] arr, string s, Color c)
    {
        if (arr == null) return;
        foreach (var t in arr)
        {
            if (t == null) continue;
            t.text = s;
            t.color = c;
        }
    }
}
