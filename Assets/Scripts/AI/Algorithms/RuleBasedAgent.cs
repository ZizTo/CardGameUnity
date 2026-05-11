using System.Collections.Generic;
using System.Linq;

public class RuleBasedAgent : IAiAgent
{
    public SimAction ChooseAction(SimGameState state, List<SimAction> actions, AiProfile profile)
    {
        var lethalHero = actions
            .Where(a => a.actionType == SimActionType.AttackHero)
            .OrderByDescending(a => state.me.board[a.attackerIndex].attack)
            .FirstOrDefault();

        if (lethalHero != null &&
            state.me.board[lethalHero.attackerIndex].attack >= state.enemy.heroHp)
            return lethalHero;

        var profitableTrade = actions
            .Where(a => a.actionType == SimActionType.AttackCard)
            .OrderByDescending(a =>
            {
                var atk = state.me.board[a.attackerIndex];
                var trg = state.enemy.board[a.targetIndex];
                float value = trg.attack + trg.hp;
                float cost = atk.attack + atk.hp;
                return value - cost * 0.5f;
            })
            .FirstOrDefault();

        if (profitableTrade != null)
            return profitableTrade;

        var bestPlay = actions
            .Where(a => a.actionType == SimActionType.PlayCard)
            .OrderByDescending(a =>
            {
                var c = state.me.hand[a.handIndex];
                return c.attack + c.hp + (c.provoc ? 2 : 0) - c.mana * 0.5f;
            })
            .FirstOrDefault();

        if (bestPlay != null)
            return bestPlay;

        var anyHeroAttack = actions.FirstOrDefault(a => a.actionType == SimActionType.AttackHero);
        if (anyHeroAttack != null)
            return anyHeroAttack;

        return actions.First(a => a.actionType == SimActionType.EndTurn);
    }
}