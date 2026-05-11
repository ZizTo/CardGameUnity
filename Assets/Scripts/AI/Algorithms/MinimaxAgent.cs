using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

public class MinimaxAgent : IAiAgent
{
    private const int MaxActionsPerTurn = 5;
    private const int MaxBranching = 6;

    public SimAction ChooseAction(SimGameState state, List<SimAction> actions, AiProfile profile)
    {
        Stopwatch sw = Stopwatch.StartNew();

        List<SimAction> orderedActions = OrderActions(state, actions, true);
        float bestValue = float.NegativeInfinity;
        SimAction bestAction = orderedActions[0];
        List<string> debugLines = new List<string>();

        foreach (var action in orderedActions)
        {
            SimGameState next = state.Clone();
            SimRules.ApplyAction(next, action, false);

            float value = action.actionType == SimActionType.EndTurn
                ? MinValue(next, profile.minimaxDepth - 1, 0, float.NegativeInfinity, float.PositiveInfinity, profile.useAlphaBeta)
                : MaxValue(next, profile.minimaxDepth, 1, float.NegativeInfinity, float.PositiveInfinity, profile.useAlphaBeta);

            debugLines.Add(action + " => " + value.ToString("F2"));

            if (value > bestValue)
            {
                bestValue = value;
                bestAction = action;
            }
        }

        sw.Stop();
        debugLines.Add("BEST = " + bestAction + " | score = " + bestValue.ToString("F2"));
        debugLines.Add("depth = " + profile.minimaxDepth + " | time = " + sw.ElapsedMilliseconds + " ms");
        AiDebug.LogBlock("MINIMAX TURN", debugLines);

        return bestAction;
    }

    private float MaxValue(SimGameState state, int depth, int actionsThisTurn, float alpha, float beta, bool useAlphaBeta)
    {
        if (depth <= 0 || SimRules.IsTerminal(state) || actionsThisTurn >= MaxActionsPerTurn)
            return SimEvaluator.Evaluate(state);

        List<SimAction> actions = SimActionGenerator.GenerateActions(state);
        if (actions == null || actions.Count == 0)
            return SimEvaluator.Evaluate(state);

        actions = OrderActions(state, actions, true);
        float value = float.NegativeInfinity;

        foreach (var action in actions)
        {
            SimGameState next = state.Clone();
            SimRules.ApplyAction(next, action, false);

            float childValue = action.actionType == SimActionType.EndTurn
                ? MinValue(next, depth - 1, 0, alpha, beta, useAlphaBeta)
                : MaxValue(next, depth, actionsThisTurn + 1, alpha, beta, useAlphaBeta);

            value = Mathf.Max(value, childValue);

            if (useAlphaBeta)
            {
                if (value >= beta)
                    return value;
                alpha = Mathf.Max(alpha, value);
            }
        }

        return value;
    }

    private float MinValue(SimGameState state, int depth, int actionsThisTurn, float alpha, float beta, bool useAlphaBeta)
    {
        if (depth <= 0 || SimRules.IsTerminal(state) || actionsThisTurn >= MaxActionsPerTurn)
            return SimEvaluator.Evaluate(state);

        List<SimAction> actions = SimActionGenerator.GenerateActions(state);
        if (actions == null || actions.Count == 0)
            return SimEvaluator.Evaluate(state);

        actions = OrderActions(state, actions, false);
        float value = float.PositiveInfinity;

        foreach (var action in actions)
        {
            SimGameState next = state.Clone();
            SimRules.ApplyAction(next, action, false);

            float childValue = action.actionType == SimActionType.EndTurn
                ? MaxValue(next, depth - 1, 0, alpha, beta, useAlphaBeta)
                : MinValue(next, depth, actionsThisTurn + 1, alpha, beta, useAlphaBeta);

            value = Mathf.Min(value, childValue);

            if (useAlphaBeta)
            {
                if (value <= alpha)
                    return value;
                beta = Mathf.Min(beta, value);
            }
        }

        return value;
    }

    private List<SimAction> OrderActions(SimGameState state, List<SimAction> actions, bool maximizing)
    {
        return actions
            .Select(a => new { Action = a, Score = ScoreAction(state, a) })
            .OrderByDescending(x => maximizing ? x.Score : -x.Score)
            .Take(MaxBranching)
            .Select(x => x.Action)
            .ToList();
    }

    private float ScoreAction(SimGameState state, SimAction action)
    {
        SimGameState next = state.Clone();
        SimRules.ApplyAction(next, action, false);
        float score = SimEvaluator.Evaluate(next);

        if (action.actionType == SimActionType.AttackCard)
        {
            var attacker = state.me.board[action.attackerIndex];
            var target = state.enemy.board[action.targetIndex];
            if (attacker.attack >= target.hp) score += 6f;
            if (target.attack >= attacker.hp) score -= 2f;
            if (target.provoc) score += 4f;
        }

        if (action.actionType == SimActionType.AttackHero)
            score += 1.5f;

        if (action.actionType == SimActionType.PlayCard)
        {
            var card = state.me.hand[action.handIndex];
            score += card.attack + card.hp + (card.provoc ? 3f : 0f);
        }

        if (action.actionType == SimActionType.EndTurn)
            score -= 3f;

        return score;
    }
}