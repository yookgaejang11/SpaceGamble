using UnityEngine;

[CreateAssetMenu(fileName = "GamePer", menuName = "Scriptable Objects/GamePer")]
public class GamePer : ScriptableObject
{
    [Header("확률 변동 분포 (합 100)")]
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

    [Header("OVERLOAD")]
    [Tooltip("한 명이 OVERLOAD 했을 때 그 라운드 사고 확률 배율")]
    public float overloadMul = 1.5f;
    [Tooltip("둘 다 OVERLOAD 했을 때 배율")]
    public float bothOverloadMul = 2.0f;

    [Header("시선 동기화")]
    [Tooltip("초당 전송 횟수")]
    public float lookSendRate = 15f;
    [Tooltip("좌우 각도 제한 (도)")]
    public float lookYawLimit = 70f;
    [Tooltip("상하 각도 제한 (도)")]
    public float lookPitchLimit = 45f;
}
