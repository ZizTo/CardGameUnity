using System.Collections.Generic;

public interface IAiAgent
{
    SimAction ChooseAction(SimGameState state, List<SimAction> actions, AiProfile profile);
}