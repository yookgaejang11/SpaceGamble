using System;
using UnityEngine;

public class GameClient : MonoBehaviour
{
    public GameManager gameManager;
    public int myIndex;
    public int foldLeft;

    public event Action<GamePhase> OnChangedPhase;//페이즈 변경시
    public event Action<int> OnRoundChanged;//라운드 변경시
    public event Action<int> OnNumberSelected;
    public event Action<float> OnTimeChanged;
    public event Action<bool, bool> OnFoldSelected;
    public event Action<Result, bool> OnButtonPulled;//졌나
    public event Action<bool> OnOuted;//사출됬나
    public event Action<int, int> OnPerChanged;
    public event Action<bool> OnGameWin;

    public void RequestFold()
    {

    }

    public void RequestPress()
    {

    }
    public void TurnEnd()//notify 뭔솔인지 못알아쳐먹어서 일단 내ㅈ대로
    {

    }
}
