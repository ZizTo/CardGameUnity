/*using System.Linq;

public static class SimEvaluator
{
    public static float Evaluate(SimGameState state)
    {
        float myBoardAttack = state.me.board.Sum(c => c.attack);
        float myBoardHp = state.me.board.Sum(c => c.hp);
        float myHand = state.me.hand.Count;
        float myProvoc = state.me.board.Count(c => c.provoc);

        float enemyBoardAttack = state.enemy.board.Sum(c => c.attack);
        float enemyBoardHp = state.enemy.board.Sum(c => c.hp);
        float enemyHand = state.enemy.hand.Count;
        float enemyProvoc = state.enemy.board.Count(c => c.provoc);

        float score = 0f;
        score += (state.me.heroHp - state.enemy.heroHp) * 5f;
        score += (myBoardAttack - enemyBoardAttack) * 2.5f;
        score += (myBoardHp - enemyBoardHp) * 1.5f;
        score += (myHand - enemyHand) * 1.0f;
        score += (myProvoc - enemyProvoc) * 1.5f;
        score += (state.me.mana - state.enemy.mana) * 0.2f;

        if (state.enemy.heroHp <= 0) score += 10000f;
        if (state.me.heroHp <= 0) score -= 10000f;

        return score;
    }
}*/

/*using System.Linq;

public static class SimEvaluator
{
    public static float Evaluate(SimGameState state)
    {
        if (state.enemy.heroHp <= 0) return 100000f;
        if (state.me.heroHp <= 0) return -100000f;

        float score = 0f;

        score += (state.me.heroHp - state.enemy.heroHp) * 8.0f;
        score += (state.me.hand.Count - state.enemy.hand.Count) * 3.0f;
        score += (state.me.mana - state.enemy.mana) * 0.8f;

        float myBoardScore = 0f;
        float enemyBoardScore = 0f;

        foreach (var c in state.me.board)
            myBoardScore += EvaluateCard(c, true);

        foreach (var c in state.enemy.board)
            enemyBoardScore += EvaluateCard(c, false);

        score += myBoardScore - enemyBoardScore;
        
        score += state.me.board.Count * 2.5f;
        score -= state.enemy.board.Count * 2.8f;

        score -= EvaluateEnemyThreats(state);
        score += EvaluateMyThreats(state) * 0.5f;

        if (state.me.board.Count == 0 && state.me.hand.Count > 0)
            score -= 6f;

        if (state.enemy.board.Count == 0 && state.me.board.Count > 0)
            score += 5f;

        if (CanLethalNextTurn(state.enemy, state.me) && !CanLethalNextTurn(state.me, state.enemy))
            score -= 35f;

        if (CanLethalNextTurn(state.me, state.enemy) && !CanLethalNextTurn(state.enemy, state.me))
            score += 20f;

        return score;
    }

    private static float EvaluateCard(SimCard c, bool mine)
    {
        float v = 0f;
        v += c.attack * 2.4f;
        v += c.hp * 1.9f;
        v += 2.5f;

        if (c.canAttack)
            v += c.attack * (mine ? 2.3f : 2.8f);

        if (c.provoc)
            v += mine ? 4.5f : 5.5f;

        if (c.attack >= 4)
            v += 2.0f;

        if (c.hp <= 1)
            v -= 1.2f;

        return v;
    }

    private static float EvaluateEnemyThreats(SimGameState state)
    {
        float threat = 0f;

        foreach (var c in state.enemy.board)
        {
            float t = 0f;
            t += c.attack * 2.6f;
            t += c.hp * 1.4f;
            if (c.provoc) t += 4.0f;
            if (c.canAttack) t += c.attack * 3.0f;
            if (c.attack >= state.me.heroHp) t += 20f;
            if (c.attack >= 4) t += 3f;
            threat += t;
        }

        return threat;
    }

    private static float EvaluateMyThreats(SimGameState state)
    {
        float pressure = 0f;

        foreach (var c in state.me.board)
        {
            float p = 0f;
            p += c.attack * 2.2f;
            if (c.canAttack) p += c.attack * 1.5f;
            if (c.provoc) p += 2.0f;
            pressure += p;
        }

        return pressure;
    }

    private static bool CanLethalNextTurn(SimPlayerState attacker, SimPlayerState defender)
    {
        bool defenderHasProvoc = defender.board.Any(c => c.provoc && c.hp > 0);
        if (defenderHasProvoc)
            return false;

        int damage = attacker.board.Where(c => c.hp > 0).Sum(c => c.attack);
        return damage >= defender.heroHp;
    }
}*/

using System.Linq;

public static class SimEvaluator
{
    public static float Evaluate(SimGameState state)
    {
        if (state.enemy.heroHp <= 0) return 100000f;
        if (state.me.heroHp <= 0) return -100000f;

        float score = 0f;

        score += (state.me.heroHp - state.enemy.heroHp) * 10f;
        score += (state.me.hand.Count - state.enemy.hand.Count) * 2.5f;
        score += (state.me.board.Count - state.enemy.board.Count) * 4f;

        score += EvaluateBoard(state.me.board, true);
        score -= EvaluateBoard(state.enemy.board, false);

        if (state.me.board.Count == 0 && state.me.hand.Count > 0)
            score -= 5f;

        if (state.enemy.board.Count == 0 && state.me.board.Count > 0)
            score += 4f;

        return score;
    }

    private static float EvaluateBoard(System.Collections.Generic.List<SimCard> board, bool mine)
    {
        float total = 0f;

        foreach (var c in board)
        {
            if (c.hp <= 0) continue;

            float v = 0f;
            v += c.attack * 2.8f;
            v += c.hp * 2.0f;
            v += 3.0f;

            if (c.provoc)
                v += 4.5f;

            if (c.attack >= 4)
                v += 2.0f;

            if (c.hp == 1)
                v -= 1.0f;

            total += v;
        }

        return total;
    }
}