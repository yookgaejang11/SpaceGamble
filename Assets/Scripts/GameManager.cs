using System;
using System.Collections.Generic;
using UnityEngine;



public class GameManager : MonoBehaviour
{

    public GamePer SOGameRule;

    public GamePer gameRule;

    private void Awake()
    {
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

}

[Serializable]
public class GameSession
{
   
    public float curOutPer;                 //현재 사출확률

    public int winnerId;                    //승자 번호(0,1)

    public int[] cardNums = new int[2];     //뽑은 카드 번호

    public int[] foldCounts = new int[2];   //포기 횟수

    public int maxFold = 2;                 //최대 fold 횟수

    public bool isGameOver;                 //게임끝났는지

    public int curRound;                    //현재 라운드

    public bool[] isFold = new bool[2];     //포기 여부

    System.Random rand = new System.Random();

}
