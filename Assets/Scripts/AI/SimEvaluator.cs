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

        // Небольшая поправка на запас карт и риск усталости.
        score += (state.me.deck.Count - state.enemy.deck.Count) * 0.35f;
        if (state.me.deck.Count == 0) score -= 12f;
        if (state.enemy.deck.Count == 0) score += 12f;
        score -= state.me.fatigue * 3f;
        score += state.enemy.fatigue * 3f;

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

            if (c.divineShield)
                v += 3.5f;

            if (c.lifesteal)
                v += 2.0f;

            if (c.charge)
                v += 1.5f;

            total += v;
        }

        return total;
    }
}
