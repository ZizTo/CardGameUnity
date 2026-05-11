using System.Collections.Generic;
using System.Linq;

public static class SimActionGenerator
{
    public static List<SimAction> GenerateActions(SimGameState state)
    {
        var actions = new List<SimAction>();

        for (int i = 0; i < state.me.hand.Count; i++)
        {
            if (state.me.hand[i].mana <= state.me.mana && state.me.board.Count < 3)
            {
                actions.Add(new SimAction
                {
                    actionType = SimActionType.PlayCard,
                    handIndex = i
                });
            }
        }

        bool enemyHasProvoc = state.enemy.board.Any(c => c.provoc && c.hp > 0);

        for (int i = 0; i < state.me.board.Count; i++)
        {
            var attacker = state.me.board[i];
            if (!attacker.canAttack || attacker.hp <= 0) continue;

            for (int j = 0; j < state.enemy.board.Count; j++)
            {
                if (state.enemy.board[j].hp <= 0) continue;
                if (!enemyHasProvoc || state.enemy.board[j].provoc)
                {
                    actions.Add(new SimAction
                    {
                        actionType = SimActionType.AttackCard,
                        attackerIndex = i,
                        targetIndex = j
                    });
                }
            }

            if (!enemyHasProvoc)
            {
                actions.Add(new SimAction
                {
                    actionType = SimActionType.AttackHero,
                    attackerIndex = i
                });
            }
        }

        actions.Add(new SimAction { actionType = SimActionType.EndTurn });
        
        actions = actions
            .OrderBy(a => a.actionType == SimActionType.EndTurn ? 1 : 0)
            .ToList();
        
        return actions;
    }
}