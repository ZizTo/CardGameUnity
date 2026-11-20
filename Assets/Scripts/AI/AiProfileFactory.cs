/// <summary>
/// Фабрика готовых конфигураций агентов.
/// Позволяет держать описания вариантов в одном месте и не повторять
/// десятки параметров в бенчмарке и игровом коде.
/// </summary>
public static class AiProfileFactory
{
    public static AiProfile RuleBased()
    {
        AiProfile p = new AiProfile(AiType.RuleBased, 0, 0, 0, false);
        p.useMoveOrdering = false;
        return p;
    }

    /// <summary>
    /// Классический минимакс без альфа-бета отсечения.
    /// Ширина и число действий ограничены, иначе полный перебор
    /// даже на глубине 2 становится слишком дорогим.
    /// </summary>
    public static AiProfile MinimaxClassic(int depth)
    {
        AiProfile p = new AiProfile(AiType.Minimax, depth, 0, 0, false);
        p.useAlphaBeta = false;
        p.useMoveOrdering = true;
        p.maxBranching = 6;
        p.maxActionsPerTurn = 3;
        return p;
    }

    public static AiProfile AlphaBeta(int depth)
    {
        AiProfile p = new AiProfile(AiType.Minimax, depth, 0, 0, true);
        p.useAlphaBeta = true;
        p.useMoveOrdering = true;
        p.maxBranching = 6;
        p.maxActionsPerTurn = 3;
        return p;
    }

    /// <summary>
    /// Контрольный вариант: то же самое, но без сортировки ходов.
    /// Нужен, чтобы измерить вклад эвристики упорядочивания.
    /// </summary>
    public static AiProfile AlphaBetaNoOrdering(int depth)
    {
        AiProfile p = AlphaBeta(depth);
        p.useMoveOrdering = false;
        return p;
    }

    public static AiProfile IterativeDeepening(int maxDepth, int timeLimitMs)
    {
        AiProfile p = AlphaBeta(maxDepth);
        p.useIterativeDeepening = true;
        p.timeLimitMs = timeLimitMs;
        p.useTranspositionTable = true;
        return p;
    }

    public static AiProfile Transposition(int depth)
    {
        AiProfile p = AlphaBeta(depth);
        p.useTranspositionTable = true;
        return p;
    }

    public static AiProfile Quiescence(int depth)
    {
        AiProfile p = AlphaBeta(depth);
        p.useQuiescence = true;
        p.quiescenceDepth = 3;
        return p;
    }

    public static AiProfile Pvs(int depth)
    {
        AiProfile p = AlphaBeta(depth);
        p.usePvs = true;
        p.useTranspositionTable = true;
        return p;
    }

    public static AiProfile Expectimax(int depth)
    {
        AiProfile p = new AiProfile(AiType.Expectimax, depth, 0, 0, false);
        p.useExpectimax = true;
        p.useAlphaBeta = false;
        p.useMoveOrdering = true;
        p.maxBranching = 4;
        p.maxActionsPerTurn = 2;
        return p;
    }

    public static AiProfile Pimc(int depth, int samples)
    {
        AiProfile p = AlphaBeta(depth);
        p.pimcSamples = samples;
        p.pimcSeed = 12345;
        return p;
    }

    public static AiProfile Mcts(int iterations, int rolloutDepth)
    {
        AiProfile p = new AiProfile(AiType.Mcts, 0, iterations, rolloutDepth, false);
        p.useMoveOrdering = false;
        return p;
    }
}
