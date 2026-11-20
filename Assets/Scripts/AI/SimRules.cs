using System.Linq;
using UnityEngine;

public static class SimRules
{
    /// <summary>
    /// Применяет действие к состоянию.
    /// forcedDrawIndex используется алгоритмом Expectimax: он позволяет
    /// подставить конкретную карту добора вместо случайной/верхней.
    /// </summary>
    public static void ApplyAction(SimGameState state, SimAction action, bool randomDraw = false, int forcedDrawIndex = -1)
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
                EndTurn(state, randomDraw, forcedDrawIndex);
                break;
            }
        }
    }

    public static void EndTurn(SimGameState state, bool randomDraw, int forcedDrawIndex = -1)
    {
        var temp = state.me;
        state.me = state.enemy;
        state.enemy = temp;
        state.isMyTurn = !state.isMyTurn;

        // Номер раунда увеличивается, когда ход возвращается к первому игроку
        // раунда. Так оба игрока в одном раунде получают одинаковое количество
        // маны, как в игровом цикле Unity.
        state.turnInRound = 1 - state.turnInRound;
        if (state.turnInRound == 0)
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
            SimCard drawn;
            if (forcedDrawIndex >= 0 && forcedDrawIndex < state.me.deck.Count)
                drawn = DrawCardAt(state.me, forcedDrawIndex);
            else if (randomDraw)
                drawn = DrawCardRandom(state.me);
            else
                drawn = DrawCardDeterministic(state.me);

            state.me.hand.Add(drawn);
        }
    }

    private static SimCard DrawCardAt(SimPlayerState player, int index)
    {
        SimCard card = player.deck[index];
        player.deck.RemoveAt(index);
        card.canAttack = false;
        return card;
    }

    private static SimCard DrawCardDeterministic(SimPlayerState player)
    {
        return DrawCardAt(player, 0);
    }

    private static SimCard DrawCardRandom(SimPlayerState player)
    {
        int index = Random.Range(0, player.deck.Count);
        return DrawCardAt(player, index);
    }

    public static bool IsTerminal(SimGameState state)
    {
        return state.me.heroHp <= 0 || state.enemy.heroHp <= 0;
    }
}
