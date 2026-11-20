using System.Collections.Generic;

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

    public SimCard Clone()
    {
        return new SimCard
        {
            id = id,
            cardName = cardName,
            mana = mana,
            attack = attack,
            hp = hp,
            canAttack = canAttack,
            provoc = provoc
        };
    }
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

    public SimPlayerState Clone()
    {
        SimPlayerState copy = new SimPlayerState();
        copy.mana = mana;
        copy.maxMana = maxMana;
        copy.heroHp = heroHp;
        copy.hand = CloneCards(hand);
        copy.board = CloneCards(board);
        copy.deck = CloneCards(deck);
        return copy;
    }

    private static List<SimCard> CloneCards(List<SimCard> source)
    {
        List<SimCard> copy = new List<SimCard>(source.Count);
        for (int i = 0; i < source.Count; i++)
            copy.Add(source[i].Clone());
        return copy;
    }
}

[System.Serializable]
public class SimGameState
{
    public SimPlayerState me = new SimPlayerState();
    public SimPlayerState enemy = new SimPlayerState();

    public int orderKol = 0;
    public int maxManaCap = 10;
    public bool isMyTurn = true;

    /// <summary>
    /// 0 — ходит первый игрок раунда, 1 — второй.
    /// Нужен для корректного начисления маны: orderKol увеличивается,
    /// когда ход возвращается к первому игроку раунда.
    /// </summary>
    public int turnInRound = 0;

    /// <summary>
    /// Быстрое глубокое копирование состояния.
    /// Раньше здесь использовался JsonUtility, который на порядки медленнее
    /// и делал поиск на глубине 3-4 практически непригодным для серий матчей.
    /// </summary>
    public SimGameState Clone()
    {
        SimGameState copy = new SimGameState();
        copy.me = me.Clone();
        copy.enemy = enemy.Clone();
        copy.orderKol = orderKol;
        copy.maxManaCap = maxManaCap;
        copy.isMyTurn = isMyTurn;
        copy.turnInRound = turnInRound;
        return copy;
    }
}
