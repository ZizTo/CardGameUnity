using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

public class MctsAgent : IAiAgent
{
    public SimAction ChooseAction(SimGameState state, List<SimAction> actions, AiProfile profile)
    {
        Stopwatch sw = Stopwatch.StartNew();

        int candidateCount = Mathf.Clamp(Mathf.Max(2, profile.mctsIterations >= 300 ? 4 : 3), 1, actions.Count);
        List<SimAction> candidateActions = actions
            .OrderByDescending(a => QuickEvaluateAfterAction(state, a))
            .Take(candidateCount)
            .ToList();

        SimAction bestAction = candidateActions[0];
        float bestScore = float.NegativeInfinity;
        List<string> debugLines = new List<string>();

        int iterationsPerAction = Mathf.Max(1, profile.mctsIterations / candidateActions.Count);

        foreach (var action in candidateActions)
        {
            float total = 0f;
            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;

            for (int i = 0; i < iterationsPerAction; i++)
            {
                SimGameState rolloutState = state.Clone();
                SimRules.ApplyAction(rolloutState, action, true);

                float score = Rollout(rolloutState, profile.rolloutDepth);
                total += score;

                if (score < min) min = score;
                if (score > max) max = score;
            }

            float avg = total / iterationsPerAction;
            debugLines.Add(action + " => avg=" + avg.ToString("F2") + " min=" + min.ToString("F2") + " max=" + max.ToString("F2") + " n=" + iterationsPerAction);

            if (avg > bestScore)
            {
                bestScore = avg;
                bestAction = action;
            }
        }

        sw.Stop();
        debugLines.Add("BEST = " + bestAction + " | avg = " + bestScore.ToString("F2"));
        debugLines.Add("iters = " + profile.mctsIterations + " | rolloutDepth = " + profile.rolloutDepth + " | candidates = " + candidateActions.Count + " | time = " + sw.ElapsedMilliseconds + " ms");
        debugLines.Add("all actions count = " + actions.Count);
        foreach (var a in actions)
            debugLines.Add("candidate raw = " + a + " | quick=" + QuickEvaluateAfterAction(state, a).ToString("F2"));
        AiDebug.LogBlock("MCTS TURN", debugLines);
        return bestAction;
    }

    private float QuickEvaluateAfterAction(SimGameState state, SimAction action)
    {
        SimGameState next = state.Clone();
        SimRules.ApplyAction(next, action, false);

        float score = EvaluateFromRootPerspective(next);

        if (action.actionType == SimActionType.EndTurn &&
            state.me.hand.Exists(c => c.mana <= state.me.mana) &&
            state.me.board.Count < 3)
        {
            score -= 6f;
        }
        
        if (action.actionType == SimActionType.EndTurn)
        {
            bool hasAnyAttack = state.me.board.Exists(c => c.canAttack && c.hp > 0);
            bool enemyHasBoard = state.enemy.board.Count > 0;

            if (hasAnyAttack)
                score -= 20f;

            if (hasAnyAttack && enemyHasBoard)
                score -= 15f;
        }

        return score;
    }
    
    private float EvaluateFromRootPerspective(SimGameState state)
    {
        float score = SimEvaluator.Evaluate(state);
        return state.isMyTurn ? score : -score;
    }

    private float Rollout(SimGameState state, int depth)
    {
        for (int step = 0; step < depth; step++)
        {
            if (SimRules.IsTerminal(state))
                break;

            List<SimAction> actions = SimActionGenerator.GenerateActions(state);
            if (actions == null || actions.Count == 0)
                break;

            SimAction action = RolloutPolicy(state, actions);
            SimRules.ApplyAction(state, action, true);
        }

        return EvaluateFromRootPerspective(state);
    }

    private SimAction RolloutPolicy(SimGameState state, List<SimAction> actions)
    {
        var lethal = actions
            .Where(a => a.actionType == SimActionType.AttackHero)
            .FirstOrDefault(a => state.me.board[a.attackerIndex].attack >= state.enemy.heroHp);
        if (lethal != null) return lethal;

        var killThreat = actions
            .Where(a => a.actionType == SimActionType.AttackCard)
            .OrderByDescending(a => ThreatTradeScore(state, a))
            .FirstOrDefault();

        if (killThreat != null && ThreatTradeScore(state, killThreat) >= 8f)
            return killThreat;

        var bestPlay = actions
            .Where(a => a.actionType == SimActionType.PlayCard)
            .OrderByDescending(a => PlayScore(state, a))
            .FirstOrDefault();

        if (bestPlay != null && Random.value < 0.55f)
            return bestPlay;

        var hero = actions
            .Where(a => a.actionType == SimActionType.AttackHero)
            .OrderByDescending(a => state.me.board[a.attackerIndex].attack)
            .FirstOrDefault();

        if (hero != null && !state.enemy.board.Any())
            return hero;

        return actions.FirstOrDefault(a => a.actionType == SimActionType.EndTurn) ?? actions[0];
    }
    
    private float ThreatTradeScore(SimGameState state, SimAction action)
    {
        var attacker = state.me.board[action.attackerIndex];
        var target = state.enemy.board[action.targetIndex];

        float score = 0f;
        score += target.attack * 4.0f;
        score += target.hp * 1.5f;
        if (target.provoc) score += 8f;
        if (target.canAttack) score += 8f;
        if (attacker.attack >= target.hp) score += 12f;
        if (target.attack >= attacker.hp) score -= 3f;

        return score;
    }

    private float TradeScore(SimGameState state, SimAction action)
    {
        var atk = state.me.board[action.attackerIndex];
        var trg = state.enemy.board[action.targetIndex];

        float value = trg.attack * 2.0f + trg.hp * 1.5f + (trg.provoc ? 2.5f : 0f);
        float cost = atk.attack * 1.2f + atk.hp * 1.6f;
        if (atk.attack >= trg.hp) value += 2.5f;
        if (trg.attack >= atk.hp) cost += 1.5f;
        return value - cost;
    }

    private float PlayScore(SimGameState state, SimAction action)
    {
        var c = state.me.hand[action.handIndex];
        return c.attack * 2.2f + c.hp * 1.8f + (c.provoc ? 3f : 0f) - c.mana * 0.3f;
    }

    private bool ShouldEndTurn(SimGameState state)
    {
        bool hasReadyAttackers = state.me.board.Any(c => c.canAttack && c.attack > 0);
        bool hasPlayableCard = state.me.hand.Any(c => c.mana <= state.me.mana) && state.me.board.Count < 3;
        return !hasReadyAttackers && !hasPlayableCard;
    }
}