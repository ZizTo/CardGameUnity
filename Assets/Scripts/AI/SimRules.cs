using System.Linq;
using UnityEngine;

public static class SimRules
{
    public static void ApplyAction(SimGameState state, SimAction action, bool randomDraw = false)
    {
        switch (action.actionType)
        {
            case SimActionType.PlayCard:
            {
                if (action.handIndex < 0 || action.handIndex >= state.me.hand.Count) return;
                if (state.me.board.Count >= 3) return;

                var card = state.me.hand[action.handIndex];
                if (card.mana > state.me.mana) return;

                state.me.mana -= card.mana;
                card.canAttack = false;
                state.me.board.Add(card);
                state.me.hand.RemoveAt(action.handIndex);
                break;
            }

            case SimActionType.AttackCard:
            {
                if (action.attackerIndex < 0 || action.attackerIndex >= state.me.board.Count) return;
                if (action.targetIndex < 0 || action.targetIndex >= state.enemy.board.Count) return;

                var attacker = state.me.board[action.attackerIndex];
                var target = state.enemy.board[action.targetIndex];

                if (!attacker.canAttack || attacker.hp <= 0 || target.hp <= 0) return;

                bool enemyHasProvoc = state.enemy.board.Any(c => c.provoc && c.hp > 0);
                if (enemyHasProvoc && !target.provoc) return;

                target.hp -= attacker.attack;
                attacker.hp -= target.attack;
                attacker.canAttack = false;

                state.me.board.RemoveAll(c => c.hp <= 0);
                state.enemy.board.RemoveAll(c => c.hp <= 0);
                break;
            }

            case SimActionType.AttackHero:
            {
                if (action.attackerIndex < 0 || action.attackerIndex >= state.me.board.Count) return;

                var attacker = state.me.board[action.attackerIndex];
                if (!attacker.canAttack || attacker.hp <= 0) return;

                bool enemyHasProvoc = state.enemy.board.Any(c => c.provoc && c.hp > 0);
                if (enemyHasProvoc) return;

                state.enemy.heroHp -= attacker.attack;
                attacker.canAttack = false;
                break;
            }

            case SimActionType.EndTurn:
            {
                EndTurn(state, randomDraw);
                break;
            }
        }
    }

    public static void EndTurn(SimGameState state, bool randomDraw)
    {
        var temp = state.me;
        state.me = state.enemy;
        state.enemy = temp;
        state.isMyTurn = !state.isMyTurn;

        state.orderKol++;
        int manaForTurn = state.orderKol > state.maxManaCap ? state.maxManaCap : state.orderKol;

        state.me.maxMana = manaForTurn;
        state.me.mana = manaForTurn;

        foreach (var card in state.me.board)
            card.canAttack = card.attack > 0 && card.hp > 0;

        foreach (var card in state.enemy.board)
            card.canAttack = false;

        if (state.me.deck.Count > 0 && state.me.hand.Count < 7)
        {
            SimCard drawn = randomDraw ? DrawCardRandom(state.me) : DrawCardDeterministic(state.me);
            state.me.hand.Add(drawn);
        }
    }

    private static SimCard DrawCardDeterministic(SimPlayerState player)
    {
        SimCard card = player.deck[0];
        player.deck.RemoveAt(0);
        card.canAttack = false;
        return card;
    }

    private static SimCard DrawCardRandom(SimPlayerState player)
    {
        int index = Random.Range(0, player.deck.Count);
        SimCard card = player.deck[index];
        player.deck.RemoveAt(index);
        card.canAttack = false;
        return card;
    }

    public static bool IsTerminal(SimGameState state)
    {
        return state.me.heroHp <= 0 || state.enemy.heroHp <= 0;
    }
}