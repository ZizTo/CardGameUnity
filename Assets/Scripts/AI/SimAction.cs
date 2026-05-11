public enum SimActionType
{
    EndTurn,
    PlayCard,
    AttackCard,
    AttackHero
}

[System.Serializable]
public class SimAction
{
    public SimActionType actionType;
    public int handIndex = -1;
    public int attackerIndex = -1;
    public int targetIndex = -1;

    public override string ToString()
    {
        return $"{actionType} h:{handIndex} a:{attackerIndex} t:{targetIndex}";
    }
}