using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

public class AIBenchmark : MonoBehaviour
{
    [Header("Benchmark Settings")]
    public int matchesPerPair = 1;
    public int maxTurnsPerMatch = 1;
    public bool saveDetailedLogs = true;

    private List<BenchmarkResult> results = new List<BenchmarkResult>();
    private StringBuilder detailedLog = new StringBuilder();

    private class BenchmarkResult
    {
        public string agent1Name;
        public string agent2Name;
        public int agent1Wins;
        public int agent2Wins;
        public int draws;
        public List<float> agent1TurnTimes = new List<float>();
        public List<float> agent2TurnTimes = new List<float>();
        public int agent1TotalTurns;
        public int agent2TotalTurns;
        public int agent1Errors;
        public int agent2Errors;
    }

    public void Start()
    {
        //UnityEngine.Debug.Log("=== AI BENCHMARK STARTED ===");
        //RunBenchmark();
    }
    
    [ContextMenu("Run Benchamrk")]
    private void RunBenchmark()
    {
        UnityEngine.Debug.Log("=== AI BENCHMARK STARTED ===");
        
        // Определяем конфигурации для тестирования
        List<AiProfile> profiles = new List<AiProfile>
        {
            new AiProfile(AiType.RuleBased, 0, 0, 0, false),
            //new AiProfile(AiType.Minimax, 2, 0, 0, false),
            new AiProfile(AiType.Minimax, 2, 0, 0, true),
            //new AiProfile(AiType.Minimax, 3, 0, 0, true),
            //new AiProfile(AiType.Mcts, 0, 100, 4, false),
            //new AiProfile(AiType.Mcts, 0, 300, 6, false),
            new AiProfile(AiType.Mcts, 0, 1200, 12, false)
        };

        List<string> profileNames = new List<string>
        {
            "Rule-Based",
            //"Minimax D=2",
            "Minimax α-β D=2",
            //"Minimax α-β D=3",
            //"MCTS 100 iter",
            //"MCTS 300 iter",
            "MCTS 1000 iter"
        };

        // Прогоняем все пары
        for (int i = 0; i < profiles.Count; i++)
        {
            for (int j = i + 1; j < profiles.Count; j++)
            {
                UnityEngine.Debug.Log($"Testing {profileNames[i]} vs {profileNames[j]}");
                detailedLog.AppendLine($"Testing {profileNames[i]} vs {profileNames[j]}");
                BenchmarkResult result = RunMatchSeries(
                    profiles[i], profileNames[i],
                    profiles[j], profileNames[j]
                );
                results.Add(result);
            }
        }

        // Выводим результаты
        PrintResults();
        SaveResultsToCSV();

        if (saveDetailedLogs)
            SaveDetailedLog();
    }

    private BenchmarkResult RunMatchSeries(AiProfile profile1, string name1, 
                                           AiProfile profile2, string name2)
    {
        BenchmarkResult result = new BenchmarkResult
        {
            agent1Name = name1,
            agent2Name = name2
        };

        for (int matchIdx = 0; matchIdx < matchesPerPair; matchIdx++)
        {
            // Чередуем, кто начинает
            bool agent1First = matchIdx % 2 == 0;

            MatchResult match = SimulateMatch(profile1, profile2, agent1First);

            if (match.winner == 1)
                result.agent1Wins++;
            else if (match.winner == 2)
                result.agent2Wins++;
            else
                result.draws++;

            result.agent1TurnTimes.AddRange(match.agent1TurnTimes);
            result.agent2TurnTimes.AddRange(match.agent2TurnTimes);
            result.agent1TotalTurns += match.agent1Turns;
            result.agent2TotalTurns += match.agent2Turns;
            result.agent1Errors += match.agent1Errors;
            result.agent2Errors += match.agent2Errors;

            if (saveDetailedLogs)
            {
                detailedLog.AppendLine($"Match {matchIdx + 1}: {name1} vs {name2}");
                detailedLog.AppendLine($"  Winner: {(match.winner == 1 ? name1 : match.winner == 2 ? name2 : "Draw")}");
                detailedLog.AppendLine($"  Turns: {match.totalTurns}");
                detailedLog.AppendLine();
            }
        }

        return result;
    }

    private class MatchResult
    {
        public int winner; // 1, 2, or 0 for draw
        public int totalTurns;
        public int agent1Turns;
        public int agent2Turns;
        public List<float> agent1TurnTimes = new List<float>();
        public List<float> agent2TurnTimes = new List<float>();
        public int agent1Errors;
        public int agent2Errors;
    }

    private MatchResult SimulateMatch(AiProfile profile1, AiProfile profile2, bool agent1First)
    {
        MatchResult result = new MatchResult();

        // Инициализация стартового состояния
        SimGameState state = CreateInitialState();

        IAiAgent agent1 = CreateAgent(profile1);
        IAiAgent agent2 = CreateAgent(profile2);

        bool isAgent1Turn = agent1First;
        int turnCount = 0;

        while (turnCount < maxTurnsPerMatch && !SimRules.IsTerminal(state))
        {
            IAiAgent currentAgent = isAgent1Turn ? agent1 : agent2;
            AiProfile currentProfile = isAgent1Turn ? profile1 : profile2;

            List<SimAction> actions = SimActionGenerator.GenerateActions(state);

            if (actions == null || actions.Count == 0)
            {
                SimRules.EndTurn(state, true);
                isAgent1Turn = !isAgent1Turn;
                turnCount++;
                continue;
            }

            Stopwatch sw = Stopwatch.StartNew();
            SimAction chosenAction = null;

            try
            {
                chosenAction = currentAgent.ChooseAction(state, actions, currentProfile);
            }
            catch (Exception e)
            {
                if (isAgent1Turn)
                    result.agent1Errors++;
                else
                    result.agent2Errors++;

                chosenAction = actions.LastOrDefault();
            }

            sw.Stop();
            float turnTime = sw.ElapsedMilliseconds;

            if (isAgent1Turn)
            {
                result.agent1TurnTimes.Add(turnTime);
                result.agent1Turns++;
            }
            else
            {
                result.agent2TurnTimes.Add(turnTime);
                result.agent2Turns++;
            }

            if (chosenAction == null)
                chosenAction = actions.LastOrDefault();

            SimRules.ApplyAction(state, chosenAction, true);

            if (chosenAction.actionType == SimActionType.EndTurn)
                isAgent1Turn = !isAgent1Turn;

            turnCount++;
        }

        result.totalTurns = turnCount;

        // Определяем победителя
        if (state.me.heroHp <= 0 && state.enemy.heroHp <= 0)
            result.winner = 0;
        else if (state.me.heroHp <= 0)
            result.winner = state.isMyTurn ? 2 : 1;
        else if (state.enemy.heroHp <= 0)
            result.winner = state.isMyTurn ? 1 : 2;
        else
            result.winner = 0;

        // Корректировка если agent1 не начинал первым
        if (!agent1First && result.winner != 0)
            result.winner = result.winner == 1 ? 2 : 1;

        return result;
    }

    private SimGameState CreateInitialState()
    {
        SimGameState state = new SimGameState();

        // Параметры по умолчанию
        state.orderKol = 0;
        state.maxManaCap = 5;
        state.isMyTurn = true;

        // Игрок 1
        state.me.heroHp = 20;
        state.me.mana = 0;
        state.me.maxMana = 0;
        state.me.deck = CreateDeck();

        // Игрок 2
        state.enemy.heroHp = 20;
        state.enemy.mana = 0;
        state.enemy.maxMana = 0;
        state.enemy.deck = CreateDeck();

        // Стартовая рука - 3 карты
        for (int i = 0; i < 3 && state.me.deck.Count > 0; i++)
        {
            SimCard card = state.me.deck[0];
            state.me.deck.RemoveAt(0);
            state.me.hand.Add(card);
        }

        for (int i = 0; i < 3 && state.enemy.deck.Count > 0; i++)
        {
            SimCard card = state.enemy.deck[0];
            state.enemy.deck.RemoveAt(0);
            state.enemy.hand.Add(card);
        }

        return state;
    }

    private List<SimCard> CreateDeck()
    {
        List<SimCard> deck = new List<SimCard>();

        // Колода согласно скриншотам:
        // 4x Goblin (1/3, cost 1)
        for (int i = 0; i < 4; i++)
            deck.Add(new SimCard { cardName = "Goblin", mana = 1, attack = 1, hp = 3, provoc = false });

        // 3x Thief (3/1, cost 2)
        for (int i = 0; i < 3; i++)
            deck.Add(new SimCard { cardName = "Thief", mana = 2, attack = 3, hp = 1, provoc = false });

        // 3x Knight (3/4, cost 3, Provoc)
        for (int i = 0; i < 3; i++)
            deck.Add(new SimCard { cardName = "Knight", mana = 3, attack = 3, hp = 4, provoc = true });

        // 2x Mage (5/4, cost 4)
        for (int i = 0; i < 2; i++)
            deck.Add(new SimCard { cardName = "Mage", mana = 4, attack = 5, hp = 4, provoc = false });

        // Перемешиваем колоду
        System.Random rng = new System.Random();
        deck = deck.OrderBy(x => rng.Next()).ToList();

        return deck;
    }

    private IAiAgent CreateAgent(AiProfile profile)
    {
        switch (profile.aiType)
        {
            case AiType.Minimax:
                return new MinimaxAgent();
            case AiType.Mcts:
                return new MctsAgent();
            default:
                return new RuleBasedAgent();
        }
    }

    private void PrintResults()
    {
        UnityEngine.Debug.Log("\n=== BENCHMARK RESULTS ===\n");
        detailedLog.AppendLine("\n=== BENCHMARK RESULTS ===\n");
        
        foreach (var result in results)
        {
            float agent1AvgTime = result.agent1TurnTimes.Count > 0 
                ? result.agent1TurnTimes.Average() : 0;
            float agent2AvgTime = result.agent2TurnTimes.Count > 0 
                ? result.agent2TurnTimes.Average() : 0;

            float agent1WinRate = (float)result.agent1Wins / matchesPerPair * 100f;
            float agent2WinRate = (float)result.agent2Wins / matchesPerPair * 100f;

            float agent1ErrorRate = result.agent1TotalTurns > 0 
                ? (float)result.agent1Errors / result.agent1TotalTurns : 0;
            float agent2ErrorRate = result.agent2TotalTurns > 0 
                ? (float)result.agent2Errors / result.agent2TotalTurns : 0;

            UnityEngine.Debug.Log($"{result.agent1Name} vs {result.agent2Name}:");
            detailedLog.AppendLine($"{result.agent1Name} vs {result.agent2Name}:");
            UnityEngine.Debug.Log($"  Wins: {result.agent1Wins}-{result.agent2Wins} (Draws: {result.draws})");
            detailedLog.AppendLine($"  Wins: {result.agent1Wins}-{result.agent2Wins} (Draws: {result.draws})");
            UnityEngine.Debug.Log($"  Win Rate: {agent1WinRate:F1}% vs {agent2WinRate:F1}%");
            detailedLog.AppendLine($"  Win Rate: {agent1WinRate:F1}% vs {agent2WinRate:F1}%");
            UnityEngine.Debug.Log($"  Avg Turn Time: {agent1AvgTime:F1}ms vs {agent2AvgTime:F1}ms");
            detailedLog.AppendLine($"  Avg Turn Time: {agent1AvgTime:F1}ms vs {agent2AvgTime:F1}ms");
            UnityEngine.Debug.Log($"  Errors/Turn: {agent1ErrorRate:F3} vs {agent2ErrorRate:F3}");
            detailedLog.AppendLine($"  Errors/Turn: {agent1ErrorRate:F3} vs {agent2ErrorRate:F3}");
            UnityEngine.Debug.Log("");
            detailedLog.AppendLine("");
        }
    }

    private void SaveResultsToCSV()
    {
        StringBuilder csv = new StringBuilder();
        csv.AppendLine("Agent1,Agent2,Agent1_Wins,Agent2_Wins,Draws,Agent1_WinRate%,Agent2_WinRate%,Agent1_AvgTime_ms,Agent2_AvgTime_ms,Agent1_Errors,Agent2_Errors");

        foreach (var result in results)
        {
            float agent1AvgTime = result.agent1TurnTimes.Count > 0 
                ? result.agent1TurnTimes.Average() : 0;
            float agent2AvgTime = result.agent2TurnTimes.Count > 0 
                ? result.agent2TurnTimes.Average() : 0;

            float agent1WinRate = (float)result.agent1Wins / matchesPerPair * 100f;
            float agent2WinRate = (float)result.agent2Wins / matchesPerPair * 100f;

            csv.AppendLine($"{result.agent1Name},{result.agent2Name}," +
                          $"{result.agent1Wins},{result.agent2Wins},{result.draws}," +
                          $"{agent1WinRate:F1},{agent2WinRate:F1}," +
                          $"{agent1AvgTime:F1},{agent2AvgTime:F1}," +
                          $"{result.agent1Errors},{result.agent2Errors}");
        }

        string path = Path.Combine(Application.dataPath, "benchmark_results.csv");
        File.WriteAllText(path, csv.ToString());
        UnityEngine.Debug.Log($"Results saved to: {path}");
    }

    private void SaveDetailedLog()
    {
        string path = Path.Combine(Application.dataPath, "benchmark_detailed_log.txt");
        File.WriteAllText(path, detailedLog.ToString());
        UnityEngine.Debug.Log($"Detailed log saved to: {path}");
    }
}
