using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InGameUI : MonoBehaviour
{
    [Header("패널")]
    public GameObject waitingPanel;     // waiting / GameOver
    public GameObject gamePanel;        // 게임 중

    [Header("대기 화면")]
    public TextMeshProUGUI roomCodeText;
    public TextMeshProUGUI readyText;
    public TextMeshProUGUI resultText;      // 승/패 표시 (GameOver)

    [Header("게임 화면")]
    public TextMeshProUGUI roundText;
    public TextMeshProUGUI opponentCardText;
    public TextMeshProUGUI perText;
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI foldLeftText;
    public TextMeshProUGUI messageText;     // 상황 안내 ("상대가 누릅니다" 등)

    [Header("버튼")]
    public Button goButton;
    public Button foldButton;
    public Button overloadButton;
    public Button pressButton;

    [Header("선택 강조")]
    public Image goHighlight;
    public Image foldHighlight;
    public Image overloadHighlight;

    GameClient client;


    void OnEnable()
    {
        GameClient.OnLocalClientReady += Bind;
        if (GameClient.Local != null) Bind(GameClient.Local);

        if (RelayManager.Instance != null)
            RelayManager.Instance.OnRoomCodeSpawned += HandleRoomCode;

        goButton.onClick.AddListener(() => client?.RequestGo());
        foldButton.onClick.AddListener(() => client?.RequestFold());
        overloadButton.onClick.AddListener(() => client?.RequestOverload());
        pressButton.onClick.AddListener(() => client?.RequestPress());
    }

    void OnDisable()
    {
        GameClient.OnLocalClientReady -= Bind;
        Unbind();

        if (RelayManager.Instance != null)
            RelayManager.Instance.OnRoomCodeSpawned -= HandleRoomCode;

        goButton.onClick.RemoveAllListeners();
        foldButton.onClick.RemoveAllListeners();
        overloadButton.onClick.RemoveAllListeners();
        pressButton.onClick.RemoveAllListeners();
    }

    void Bind(GameClient c)
    {
        if (client == c) return;
        Unbind();

        client = c;

        client.OnChangedPhase += HandlePhase;
        client.OnRoundChanged += HandleRound;
        client.OnNumberSelected += HandleOpponentCard;
        client.OnMyFoldLeftChanged += HandleFoldLeft;
        client.OnMyChoiceChanged += HandleMyChoice;
        client.OnTimeChanged += HandleTime;
        client.OnChoiceRevealed += HandleChoiceRevealed;
        client.OnRoundDecided += HandleRoundDecided;
        client.OnOuted += HandleOuted;
        client.OnPerChanged += HandlePer;
        client.OnGameWin += HandleGameWin;
        client.OnReadyPressed += HandleReady;

        HandlePhase(client.curPhase);       // 현재 상태 즉시 반영
    }

    void Unbind()
    {
        if (client == null) return;

        client.OnChangedPhase -= HandlePhase;
        client.OnRoundChanged -= HandleRound;
        client.OnNumberSelected -= HandleOpponentCard;
        client.OnMyFoldLeftChanged -= HandleFoldLeft;
        client.OnMyChoiceChanged -= HandleMyChoice;
        client.OnTimeChanged -= HandleTime;
        client.OnChoiceRevealed -= HandleChoiceRevealed;
        client.OnRoundDecided -= HandleRoundDecided;
        client.OnOuted -= HandleOuted;
        client.OnPerChanged -= HandlePer;
        client.OnGameWin -= HandleGameWin;
        client.OnReadyPressed -= HandleReady;

        client = null;
    }


    void HandlePhase(GamePhase phase)
    {
        bool waiting = (phase == GamePhase.waiting || phase == GamePhase.GameOver);

        waitingPanel.SetActive(waiting);
        gamePanel.SetActive(!waiting);

        bool canChoose = (phase == GamePhase.talkTime);
        goButton.interactable = canChoose;
        overloadButton.interactable = canChoose;
        foldButton.interactable = canChoose && client != null && client.foldLeft > 0;

        pressButton.gameObject.SetActive(false);    // Press 진입 시 OnRoundDecided로 켠다

        if (phase == GamePhase.waiting)
            resultText.text = "";

        // 연출이 필요한 페이즈는 끝났다고 알려야 진행된다
        switch (phase)
        {
            case GamePhase.RoundStart:
                StartPresentation("카드 배정", 2f);
                break;
            case GamePhase.Open:
                StartPresentation("선택 공개", 2f);
                break;
            case GamePhase.Result:
                StartPresentation("결과", 2.5f);
                break;
        }
    }

    Coroutine presentation;

    void StartPresentation(string label, float seconds)
    {
        if (presentation != null) StopCoroutine(presentation);
        presentation = StartCoroutine(PresentationRoutine(seconds));
    }

    System.Collections.IEnumerator PresentationRoutine(float seconds)
    {
        // 여기에 실제 연출을 넣는다 (애니메이션, 사운드, 화면 효과)
        yield return new WaitForSeconds(seconds);

        client?.EndPresentation();
        presentation = null;
    }

    void HandleRoomCode(string code) => roomCodeText.text = $"CODE  {code}";

    void HandleRound(int round) => roundText.text = $"ROUND {round}";

    void HandleOpponentCard(int card) => opponentCardText.text = card.ToString();

    void HandleFoldLeft(int left)
    {
        foldLeftText.text = $"FOLD {left}";
        if (client != null && client.curPhase == GamePhase.talkTime)
            foldButton.interactable = left > 0;
    }

    void HandleTime(float remain) => timerText.text = Mathf.CeilToInt(remain).ToString();

    void HandlePer(int before, int now)
    {
        perText.text = $"{now}%";
        // 증감 연출: now > before 면 상승
    }

    void HandleReady(bool ready)
        => readyText.text = ready ? "READY" : "F4 ? READY";

    void HandleMyChoice(Choice c)
    {
        goHighlight.enabled = (c == Choice.Go);
        foldHighlight.enabled = (c == Choice.Fold);
        overloadHighlight.enabled = (c == Choice.Overload);
    }

    void HandleChoiceRevealed(Choice mine, Choice opponent)
    {
        messageText.text = $"나 {mine}   상대 {opponent}";
        // 동시 공개 연출 자리
    }

    void HandleRoundDecided(Result result, bool isMyTurn)
    {
        switch (result)
        {
            case Result.draw:
                messageText.text = "SURVIVAL PRIORITY EQUAL";
                break;
            case Result.fold:
                messageText.text = "ROUND ABORTED";
                break;
            default:
                messageText.text = isMyTurn ? "당신이 작동합니다" : "상대가 작동합니다";
                pressButton.gameObject.SetActive(isMyTurn);
                pressButton.interactable = isMyTurn;
                break;
        }
    }

    void HandleOuted(bool outed)
    {
        pressButton.gameObject.SetActive(false);
        messageText.text = outed ? "EJECTION SYSTEM ACTIVATED" : "SAFE";
    }

    void HandleGameWin(bool win)
    {
        resultText.text = win ? "SURVIVOR CONFIRMED" : "EJECTED";
    }
}
