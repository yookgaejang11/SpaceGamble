using UnityEngine;

[CreateAssetMenu(fileName = "GamePer", menuName = "Scriptable Objects/GamePer")]
public class GamePer : ScriptableObject
{

    [Header("확률")]
    public int upPer = 49;
    public int downPer = 49;
    public int extraPer = 2;
    [Header("사출 확률 증감값")]
    public int upMaxVal = 8;
    public int upMinVal = 2;
    public int downMaxVal = 6;
    public int downMinVal = 1;
    public int extraVal = 20;

    [Header("시간")]
    public float talkingTime = 20f;
    public float cutSceneTime = 10f;
    [Header("사출 확률")]
    public int basicPer = 10;
}
