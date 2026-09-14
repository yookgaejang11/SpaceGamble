using UnityEngine;

[CreateAssetMenu(fileName = "GamePer", menuName = "Scriptable Objects/GamePer")]
public class GamePer : ScriptableObject
{

    [Header("È®·ü")]
    public int upPer = 49;
    public int downPer = 49;
    public int extraPer = 2;
    [Header("»çÃâ È®·ü Áõ°¨°ª")]
    public int upMaxVal = 8;
    public int upMinVal = 2;
    public int downMaxVal = 6;
    public int downMinVal = 1;
    public int extraVal = 20;


    [Header("»çÃâ È®·ü")]
    public int basicPer = 10;
}
