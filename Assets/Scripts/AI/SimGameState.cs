using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SimCard
{
    public string id;
    public string cardName;
    public int mana;
    public int attack;
    public int hp;
    public bool canAttack;
    public bool provoc;
}

[System.Serializable]
public class SimPlayerState
{
    public int mana;
    public int maxMana;
    public int heroHp = 30;

    public List<SimCard> hand = new List<SimCard>();
    public List<SimCard> board = new List<SimCard>();
    public List<SimCard> deck = new List<SimCard>();
}

[System.Serializable]
public class SimGameState
{
    public SimPlayerState me = new SimPlayerState();
    public SimPlayerState enemy = new SimPlayerState();

    public int orderKol = 0;
    public int maxManaCap = 10;
    public bool isMyTurn = true;

    public SimGameState Clone()
    {
        return JsonUtility.FromJson<SimGameState>(JsonUtility.ToJson(this));
    }
}