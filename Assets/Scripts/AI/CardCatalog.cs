using System.Collections.Generic;

/// <summary>
/// Описание карты. Один и тот же каталог используется бенчмарком
/// и Editor-генератором prefab'ов, чтобы правила в игре и в AI-симуляции
/// не расходились.
/// </summary>
[System.Serializable]
public class CardDefinition
{
    public string name;
    public int mana;
    public int attack;
    public int hp;
    public int copies;
    public bool taunt;
    public bool charge;
    public bool divineShield;
    public bool lifesteal;
    public string description;

    public SimCard ToSimCard()
    {
        return new SimCard
        {
            id = name,
            cardName = name,
            mana = mana,
            attack = attack,
            hp = hp,
            canAttack = false,
            provoc = taunt,
            charge = charge,
            divineShield = divineShield,
            lifesteal = lifesteal
        };
    }
}

/// <summary>
/// Расширенный пул карт. Колода собирается из copies каждой карты,
/// поэтому оба игрока получают одинаковый набор и одинаковую кривую маны.
/// </summary>
public static class CardCatalog
{
    public static readonly List<CardDefinition> Cards = new List<CardDefinition>
    {
        new CardDefinition
        {
            name = "Wisp", mana = 0, attack = 1, hp = 1, copies = 1,
            description = "A tiny spark of initiative."
        },
        new CardDefinition
        {
            name = "Goblin", mana = 1, attack = 1, hp = 3, copies = 4,
            description = "Little strong bro."
        },
        new CardDefinition
        {
            name = "Squire", mana = 1, attack = 2, hp = 1, copies = 2,
            charge = true,
            description = "A fast attacker."
        },
        new CardDefinition
        {
            name = "Thief", mana = 2, attack = 3, hp = 1, copies = 4,
            description = "Fast and fragile."
        },
        new CardDefinition
        {
            name = "Shieldbearer", mana = 2, attack = 1, hp = 4, copies = 3,
            taunt = true,
            description = "A sturdy defender."
        },
        new CardDefinition
        {
            name = "Cleric", mana = 2, attack = 2, hp = 2, copies = 2,
            lifesteal = true,
            description = "Drains life from enemies."
        },
        new CardDefinition
        {
            name = "Knight", mana = 3, attack = 3, hp = 4, copies = 3,
            taunt = true,
            description = "A sturdy defender."
        },
        new CardDefinition
        {
            name = "Assassin", mana = 3, attack = 4, hp = 2, copies = 2,
            charge = true,
            description = "A fast attacker."
        },
        new CardDefinition
        {
            name = "Mage", mana = 4, attack = 5, hp = 4, copies = 2,
            description = "Heavy hitter."
        },
        new CardDefinition
        {
            name = "Paladin", mana = 4, attack = 3, hp = 4, copies = 2,
            divineShield = true,
            description = "Protected by a holy shield."
        },
        new CardDefinition
        {
            name = "Golem", mana = 5, attack = 3, hp = 7, copies = 2,
            taunt = true,
            description = "A sturdy defender."
        },
        new CardDefinition
        {
            name = "Vampire", mana = 5, attack = 5, hp = 4, copies = 1,
            lifesteal = true,
            description = "Drains life from enemies."
        },
        new CardDefinition
        {
            name = "Dragon", mana = 6, attack = 7, hp = 6, copies = 1,
            description = "Late-game threat."
        },
        new CardDefinition
        {
            name = "Titan", mana = 8, attack = 9, hp = 9, copies = 1,
            description = "Massive late-game threat."
        }
    };

    public static int TotalCards
    {
        get
        {
            int total = 0;
            for (int i = 0; i < Cards.Count; i++)
                total += Cards[i].copies;
            return total;
        }
    }

    public static List<SimCard> CreateDeck(int seed)
    {
        List<SimCard> deck = new List<SimCard>();

        for (int i = 0; i < Cards.Count; i++)
        {
            CardDefinition definition = Cards[i];
            for (int copy = 0; copy < definition.copies; copy++)
                deck.Add(definition.ToSimCard());
        }

        System.Random rng = new System.Random(seed);
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            SimCard temp = deck[i];
            deck[i] = deck[j];
            deck[j] = temp;
        }

        return deck;
    }

    public static string DescribeKeywords(CardDefinition card)
    {
        List<string> keywords = new List<string>();
        if (card.taunt) keywords.Add("Taunt");
        if (card.charge) keywords.Add("Charge");
        if (card.divineShield) keywords.Add("Divine Shield");
        if (card.lifesteal) keywords.Add("Lifesteal");

        if (keywords.Count == 0)
            return card.description;

        return string.Join(". ", keywords.ToArray()) + ". " + card.description;
    }
}

