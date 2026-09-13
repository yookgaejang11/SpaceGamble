using UnityEngine;

[CreateAssetMenu(fileName = "GamePer", menuName = "Scriptable Objects/GamePer")]
public class GamePer : ScriptableObject
{

    [Header("È®·ü")]
    public float upPer = 49f;
    public float downPer = 49f;
    public float extraPer = 2f;

    [Header("»çÃâ È®·ü")]
    public float basicPer = 10f;
}
