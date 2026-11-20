using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

/// <summary>
/// Минимаксный агент с набором взаимодополняющих модификаций.
/// Один класс содержит общее negamax-ядро, а конкретный вариант
/// включается флагами AiProfile: α-β, сортировка ходов, итеративное
/// углубление, таблица транспозиций, quiescence, PVS, Expectimax, PIMC.
/// </summary>
public class MinimaxAgent : IAiAgent, IAiStatsProvider
{
    private const int TtSize = 1 << 18;
    private const ulong HashOffset = 1469598103934665603UL;
    private const ulong HashPrime = 1099511628211UL;

    private struct TTEntry
    {
        public ulong key;
        public float value;
        public int depth;
        public byte flag; // 0 — пусто, 1 — exact, 2 — lower bound, 3 — upper bound
    }

    private class SearchTimeoutException : Exception
    {
    }

    private class RootResult
    {
        public SimAction action;
        public float value;
        public bool completed;
    }

    private AiProfile profile;
    private AiSearchStats stats = new AiSearchStats();
    private Stopwatch timer = new Stopwatch();
    private long deadlineMs;
    private bool timedOut;
    private TTEntry[] tt;

    public AiSearchStats LastStats
    {
        get { return stats; }
    }

    public SimAction ChooseAction(SimGameState state, List<SimAction> actions, AiProfile p)
    {
        // AiType.Expectimax — это тот же минимакс с chance-узлами.
        if (p.aiType == AiType.Expectimax)
            p.useExpectimax = true;

        profile = p;
        stats = new AiSearchStats();
        timer = Stopwatch.StartNew();
        deadlineMs = p.timeLimitMs > 0 ? p.timeLimitMs : 0;
        timedOut = false;

        if (p.useTranspositionTable)
        {
            if (tt == null || tt.Length != TtSize)
                tt = new TTEntry[TtSize];
            else
                Array.Clear(tt, 0, tt.Length);
        }

        List<SimAction> legal = actions;
        if (legal == null || legal.Count == 0)
            legal = SimActionGenerator.GenerateActions(state);

        if (legal == null || legal.Count == 0)
        {
            timer.Stop();
            stats.elapsedMs = timer.ElapsedMilliseconds;
            return null;
        }

        SimAction best;
        if (p.pimcSamples > 0)
            best = ChoosePimc(state, legal, p);
        else if (p.useIterativeDeepening)
            best = ChooseIterative(state, legal, p);
        else
            best = ChooseFixed(state, legal, p);

        timer.Stop();
        stats.elapsedMs = timer.ElapsedMilliseconds;

        if (stats.depthReached <= 0)
            stats.depthReached = Mathf.Max(1, p.minimaxDepth);

        return best ?? legal[legal.Count - 1];
    }

    private SimAction ChooseFixed(SimGameState state, List<SimAction> legal, AiProfile p)
    {
        RootResult result = SearchRoot(state, legal, p.minimaxDepth, p);
        stats.depthReached = Mathf.Max(1, p.minimaxDepth);
        return result.action;
    }

    private SimAction ChooseIterative(SimGameState state, List<SimAction> legal, AiProfile p)
    {
        SimAction best = legal[legal.Count - 1];
        int maxDepth = Mathf.Max(1, p.minimaxDepth);

        for (int depth = 1; depth <= maxDepth; depth++)
        {
            if (timedOut || CheckTimeout())
                break;

            RootResult result = SearchRoot(state, legal, depth, p);

            if (result.completed || depth == 1)
            {
                best = result.action;
                stats.depthReached = depth;
            }

            if (timedOut)
                break;

            if (deadlineMs > 0 && timer.ElapsedMilliseconds >= deadlineMs)
                break;
        }

        if (stats.depthReached <= 0)
            stats.depthReached = 1;

        return best;
    }

    /// <summary>
    /// Perfect Information Monte Carlo: несколько раз перемешиваем колоды,
    /// для каждой детерминизации считаем обычный α-β и усредняем оценки действий.
    /// </summary>
    private SimAction ChoosePimc(SimGameState state, List<SimAction> legal, AiProfile p)
    {
        int samples = Mathf.Max(1, p.pimcSamples);
        int seed = (p.pimcSeed != 0 ? p.pimcSeed : 12345) + state.orderKol * 7919;
        System.Random rng = new System.Random(seed);

        List<SimGameState> determinizations = new List<SimGameState>(samples);
        for (int i = 0; i < samples; i++)
        {
            SimGameState d = state.Clone();
            Shuffle(d.me.deck, rng);
            Shuffle(d.enemy.deck, rng);
            determinizations.Add(d);
        }

        List<SimAction> ordered = OrderRootActions(state, legal);
        SimAction best = ordered[0];
        float bestAverage = float.NegativeInfinity;

        foreach (SimAction action in ordered)
        {
            float total = 0f;
            int evaluated = 0;

            foreach (SimGameState d in determinizations)
            {
                if (timedOut || CheckTimeout())
                    break;

                try
                {
                    total += EvaluateRootAction(d, action, p.minimaxDepth, float.NegativeInfinity,
                                                float.PositiveInfinity, p);
                    evaluated++;
                }
                catch (SearchTimeoutException)
                {
                    timedOut = true;
                    break;
                }
            }

            if (evaluated == 0)
                continue;

            float average = total / evaluated;
            if (average > bestAverage)
            {
                bestAverage = average;
                best = action;
            }
        }

        stats.depthReached = Mathf.Max(1, p.minimaxDepth);
        return best;
    }

    private RootResult SearchRoot(SimGameState state, List<SimAction> legal, int depth, AiProfile p)
    {
        RootResult result = new RootResult
        {
            action = legal[0],
            value = float.NegativeInfinity,
            completed = false
        };

        List<SimAction> ordered = OrderRootActions(state, legal);
        if (ordered.Count == 0)
            return result;

        result.action = ordered[0];
        float alpha = float.NegativeInfinity;
        float beta = float.PositiveInfinity;

        foreach (SimAction action in ordered)
        {
            if (CheckTimeout())
                return result;

            float value;
            try
            {
                value = EvaluateRootAction(state, action, depth, alpha, beta, p);
            }
            catch (SearchTimeoutException)
            {
                timedOut = true;
                return result;
            }

            if (value > result.value)
            {
                result.value = value;
                result.action = action;
            }

            if (p.useAlphaBeta && result.value > alpha)
                alpha = result.value;
        }

        result.completed = true;
        return result;
    }

    private float EvaluateRootAction(SimGameState state, SimAction action, int depth,
                                     float alpha, float beta, AiProfile p)
    {
        if (action.actionType == SimActionType.EndTurn)
        {
            if (p.useExpectimax)
                return ChanceAfterEndTurn(state, depth, p);

            SimGameState next = state.Clone();
            SimRules.ApplyAction(next, action, false);
            return -Search(next, depth - 1, 0, -beta, -alpha, p);
        }

        SimGameState child = state.Clone();
        SimRules.ApplyAction(child, action, false);

        if (p.useExpectimax)
            return SearchExpectimax(child, depth, 1, p);

        return Search(child, depth, 1, alpha, beta, p);
    }

    /// <summary>
    /// Negamax-ядро. Состояние всегда оценивается с точки зрения игрока,
    /// который должен ходить в state.me. Поэтому после EndTurn знак меняется.
    /// </summary>
    private float Search(SimGameState state, int depth, int actionsThisTurn,
                         float alpha, float beta, AiProfile p)
    {
        stats.nodes++;
        if (deadlineMs > 0 && (stats.nodes & 1023) == 0 && timer.ElapsedMilliseconds >= deadlineMs)
            throw new SearchTimeoutException();

        if (SimRules.IsTerminal(state))
            return SimEvaluator.Evaluate(state);

        if (depth <= 0 || actionsThisTurn >= p.maxActionsPerTurn)
        {
            if (p.useQuiescence)
                return Quiesce(state, alpha, beta, 0, p);
            return SimEvaluator.Evaluate(state);
        }

        ulong key = 0;
        float alphaOrig = alpha;
        float betaOrig = beta;

        if (p.useTranspositionTable)
        {
            key = ComputeHash(state, actionsThisTurn);
            TTEntry entry = tt[(int)(key & (ulong)(TtSize - 1))];
            if (entry.flag != 0 && entry.key == key && entry.depth >= depth)
            {
                stats.ttHits++;
                if (entry.flag == 1)
                    return entry.value;
                if (entry.flag == 2)
                {
                    if (entry.value > alpha) alpha = entry.value;
                }
                else if (entry.flag == 3)
                {
                    if (entry.value < beta) beta = entry.value;
                }

                if (alpha >= beta)
                    return entry.value;
            }
        }

        List<SimAction> actions = SimActionGenerator.GenerateActions(state);
        if (actions == null || actions.Count == 0)
            return SimEvaluator.Evaluate(state);

        actions = OrderActions(state, actions, p.maxBranching, p);

        float best = float.NegativeInfinity;
        bool first = true;

        foreach (SimAction action in actions)
        {
            float value;

            if (action.actionType == SimActionType.EndTurn)
            {
                SimGameState next = state.Clone();
                SimRules.ApplyAction(next, action, false);
                value = -Search(next, depth - 1, 0, -beta, -alpha, p);
            }
            else
            {
                SimGameState next = state.Clone();
                SimRules.ApplyAction(next, action, false);

                if (p.usePvs && !first)
                {
                    value = Search(next, depth, actionsThisTurn + 1, alpha, alpha + 1f, p);
                    if (value > alpha && value < beta)
                        value = Search(next, depth, actionsThisTurn + 1, alpha, beta, p);
                }
                else
                {
                    value = Search(next, depth, actionsThisTurn + 1, alpha, beta, p);
                }
            }

            if (value > best)
                best = value;
            if (best > alpha)
                alpha = best;

            if (p.useAlphaBeta && alpha >= beta)
            {
                stats.cutoffs++;
                break;
            }

            first = false;
        }

        if (p.useTranspositionTable)
            StoreTt(key, depth, best, alphaOrig, betaOrig);

        return best;
    }

    /// <summary>
    /// Expectimax: в узлах добора карты вычисляется математическое ожидание
    /// по всем ещё не вышедшим типам карт с учётом их количества в колоде.
    /// </summary>
    private float SearchExpectimax(SimGameState state, int depth, int actionsThisTurn, AiProfile p)
    {
        stats.nodes++;
        if (deadlineMs > 0 && (stats.nodes & 1023) == 0 && timer.ElapsedMilliseconds >= deadlineMs)
            throw new SearchTimeoutException();

        if (SimRules.IsTerminal(state))
            return SimEvaluator.Evaluate(state);

        if (depth <= 0 || actionsThisTurn >= p.maxActionsPerTurn)
            return SimEvaluator.Evaluate(state);

        List<SimAction> actions = SimActionGenerator.GenerateActions(state);
        if (actions == null || actions.Count == 0)
            return SimEvaluator.Evaluate(state);

        actions = OrderActions(state, actions, p.maxBranching, p);

        float best = float.NegativeInfinity;
        foreach (SimAction action in actions)
        {
            float value;

            if (action.actionType == SimActionType.EndTurn)
            {
                value = ChanceAfterEndTurn(state, depth, p);
            }
            else
            {
                SimGameState next = state.Clone();
                SimRules.ApplyAction(next, action, false);
                value = SearchExpectimax(next, depth, actionsThisTurn + 1, p);
            }

            if (value > best)
                best = value;
        }

        return best;
    }

    private float ChanceAfterEndTurn(SimGameState state, int depth, AiProfile p)
    {
        bool willDraw = state.enemy.deck.Count > 0 && state.enemy.hand.Count < 7;

        if (!willDraw)
        {
            SimGameState next = state.Clone();
            SimRules.ApplyAction(next, new SimAction { actionType = SimActionType.EndTurn }, false);
            return -SearchExpectimax(next, depth - 1, 0, p);
        }

        int deckCount = state.enemy.deck.Count;
        Dictionary<string, List<int>> groups = new Dictionary<string, List<int>>();

        for (int i = 0; i < deckCount; i++)
        {
            string key = CardKey(state.enemy.deck[i]);
            if (!groups.ContainsKey(key))
                groups[key] = new List<int>();
            groups[key].Add(i);
        }

        float total = 0f;
        foreach (List<int> group in groups.Values)
        {
            SimGameState next = state.Clone();
            SimRules.ApplyAction(next, new SimAction { actionType = SimActionType.EndTurn }, false, group[0]);
            float childValue = -SearchExpectimax(next, depth - 1, 0, p);
            total += childValue * ((float)group.Count / deckCount);
        }

        return total;
    }

    /// <summary>
    /// Quiescence search, адаптированный под структуру хода карточной игры:
    /// после достижения предельной глубины доигрываются только атаки,
    /// чтобы статическая оценка не обрывала тактическую последовательность.
    /// </summary>
    private float Quiesce(SimGameState state, float alpha, float beta, int qDepth, AiProfile p)
    {
        stats.nodes++;
        if (deadlineMs > 0 && (stats.nodes & 1023) == 0 && timer.ElapsedMilliseconds >= deadlineMs)
            throw new SearchTimeoutException();

        if (SimRules.IsTerminal(state))
            return SimEvaluator.Evaluate(state);

        float standPat = SimEvaluator.Evaluate(state);
        if (qDepth >= p.quiescenceDepth)
            return standPat;

        if (standPat >= beta)
            return beta;
        if (standPat > alpha)
            alpha = standPat;

        List<SimAction> actions = SimActionGenerator.GenerateActions(state)
            .Where(a => a.actionType == SimActionType.AttackCard ||
                        (a.actionType == SimActionType.AttackHero && IsLethalHeroAttack(state, a)))
            .ToList();

        if (actions.Count == 0)
            return standPat;

        actions = OrderActions(state, actions, p.maxBranching, p);

        foreach (SimAction action in actions)
        {
            SimGameState next = state.Clone();
            SimRules.ApplyAction(next, action, false);

            // Атака не передаёт ход, поэтому перспектива не меняется.
            float score = Quiesce(next, alpha, beta, qDepth + 1, p);

            if (score >= beta)
            {
                stats.cutoffs++;
                return beta;
            }
            if (score > alpha)
                alpha = score;
        }

        return alpha;
    }

    private static bool IsLethalHeroAttack(SimGameState state, SimAction action)
    {
        return action.attackerIndex >= 0 &&
               action.attackerIndex < state.me.board.Count &&
               state.me.board[action.attackerIndex].attack >= state.enemy.heroHp;
    }

    private List<SimAction> OrderRootActions(SimGameState state, List<SimAction> actions)
    {
        return OrderActions(state, actions, 0, profile);
    }

    private List<SimAction> OrderActions(SimGameState state, List<SimAction> actions, int limit, AiProfile p)
    {
        IEnumerable<SimAction> sequence = actions;

        if (p.useMoveOrdering)
            sequence = actions.OrderByDescending(a => ScoreAction(state, a));

        List<SimAction> result;
        if (limit > 0 && actions.Count > limit)
            result = sequence.Take(limit).ToList();
        else
            result = sequence.ToList();

        // EndTurn не должен теряться при ограничении ширины,
        // иначе поиск может вообще не рассмотреть смену хода.
        if (!result.Any(a => a.actionType == SimActionType.EndTurn))
        {
            SimAction endTurn = actions.FirstOrDefault(a => a.actionType == SimActionType.EndTurn);
            if (endTurn != null)
            {
                if (limit > 0 && result.Count >= limit && result.Count > 0)
                    result[result.Count - 1] = endTurn;
                else
                    result.Add(endTurn);
            }
        }

        return result;
    }

    /// <summary>
    /// Дешёвая эвристика для сортировки ходов. Намеренно не клонирует состояние:
    /// сортировка вызывается в каждом узле и должна стоить минимум.
    /// </summary>
    private float ScoreAction(SimGameState state, SimAction action)
    {
        switch (action.actionType)
        {
            case SimActionType.EndTurn:
                return -1000f;

            case SimActionType.PlayCard:
            {
                if (action.handIndex < 0 || action.handIndex >= state.me.hand.Count)
                    return -1000f;
                SimCard card = state.me.hand[action.handIndex];
                return card.attack * 3f + card.hp * 2.5f + (card.provoc ? 6f : 0f) - card.mana * 0.2f;
            }

            case SimActionType.AttackCard:
            {
                if (action.attackerIndex < 0 || action.attackerIndex >= state.me.board.Count)
                    return -1000f;
                if (action.targetIndex < 0 || action.targetIndex >= state.enemy.board.Count)
                    return -1000f;

                SimCard attacker = state.me.board[action.attackerIndex];
                SimCard target = state.enemy.board[action.targetIndex];

                float score = target.attack * 4f + target.hp * 2.5f + (target.provoc ? 12f : 0f);
                if (attacker.attack >= target.hp) score += 20f;
                if (target.attack >= attacker.hp) score -= 6f;
                return score;
            }

            case SimActionType.AttackHero:
            {
                if (action.attackerIndex < 0 || action.attackerIndex >= state.me.board.Count)
                    return -1000f;
                SimCard attacker = state.me.board[action.attackerIndex];
                float score = attacker.attack * 2f;
                if (attacker.attack >= state.enemy.heroHp)
                    score += 10000f;
                return score;
            }
        }

        return 0f;
    }

    private bool CheckTimeout()
    {
        if (deadlineMs <= 0)
            return false;

        if (timer.ElapsedMilliseconds >= deadlineMs)
        {
            timedOut = true;
            return true;
        }

        return false;
    }

    private void StoreTt(ulong key, int depth, float value, float alphaOrig, float betaOrig)
    {
        if (key == 0)
            return;

        byte flag;
        if (value <= alphaOrig)
            flag = 3;
        else if (value >= betaOrig)
            flag = 2;
        else
            flag = 1;

        tt[(int)(key & (ulong)(TtSize - 1))] = new TTEntry
        {
            key = key,
            value = value,
            depth = depth,
            flag = flag
        };
    }

    private static ulong ComputeHash(SimGameState state, int actionsThisTurn)
    {
        ulong hash = HashOffset;
        Mix(ref hash, state.isMyTurn ? 1 : 0);
        Mix(ref hash, state.orderKol);
        Mix(ref hash, state.maxManaCap);
        Mix(ref hash, state.turnInRound);
        Mix(ref hash, actionsThisTurn);
        MixPlayer(ref hash, state.me);
        MixPlayer(ref hash, state.enemy);
        return hash;
    }

    private static void MixPlayer(ref ulong hash, SimPlayerState player)
    {
        Mix(ref hash, player.heroHp);
        Mix(ref hash, player.mana);
        Mix(ref hash, player.maxMana);
        Mix(ref hash, player.hand.Count);
        for (int i = 0; i < player.hand.Count; i++)
            MixCard(ref hash, player.hand[i]);
        Mix(ref hash, player.board.Count);
        for (int i = 0; i < player.board.Count; i++)
            MixCard(ref hash, player.board[i]);
        Mix(ref hash, player.deck.Count);
        for (int i = 0; i < player.deck.Count; i++)
            MixCard(ref hash, player.deck[i]);
    }

    private static void MixCard(ref ulong hash, SimCard card)
    {
        Mix(ref hash, card.mana);
        Mix(ref hash, card.attack);
        Mix(ref hash, card.hp);
        Mix(ref hash, card.canAttack ? 1 : 0);
        Mix(ref hash, card.provoc ? 1 : 0);
    }

    private static void Mix(ref ulong hash, int value)
    {
        unchecked
        {
            hash ^= (uint)value;
            hash *= HashPrime;
        }
    }

    private static void Shuffle(List<SimCard> cards, System.Random rng)
    {
        for (int i = cards.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            SimCard temp = cards[i];
            cards[i] = cards[j];
            cards[j] = temp;
        }
    }

    private static string CardKey(SimCard card)
    {
        return (card.cardName ?? "") + "|" + card.mana + "|" + card.attack + "|" +
               card.hp + "|" + (card.provoc ? 1 : 0);
    }
}
