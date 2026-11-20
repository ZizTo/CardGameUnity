using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

public enum BenchmarkMode
{
    /// <summary>Каждый вариант играет только с выбранными эталонными агентами.</summary>
    Gauntlet,
    /// <summary>Полный round-robin: все со всеми. Дорого, включать осознанно.</summary>
    RoundRobin
}

/// <summary>
/// Автоматизированный бенчмарк минимаксных агентов.
///
/// По умолчанию используется режим Gauntlet: каждый вариант играет не со всеми,
/// а только с двумя эталонными противниками (Rule-Based и α-β D=2). Это даёт
/// сопоставимые результаты за минуты, а не часы. Полный round-robin можно
/// включить в инспекторе.
///
/// Все партии воспроизводимы: колоды перемешиваются от seed, а в парах партий
/// агенты по очереди начинают, что компенсирует преимущество первого хода.
/// </summary>
public class AIBenchmark : MonoBehaviour
{
    [Header("Benchmark Settings")]
    public BenchmarkMode mode = BenchmarkMode.Gauntlet;

    [Tooltip("Короткие имена эталонных агентов для режима Gauntlet")]
    public string[] gauntletOpponents = new string[] { "Rule", "AB-D2" };

    [Tooltip("Число партий в каждой паре. Чётное значение даёт зеркальные пары.")]
    public int matchesPerPair = 6;

    [Tooltip("Предохранитель от зависания: максимум действий в одной партии")]
    public int maxActionsPerMatch = 400;

    [Tooltip("Предохранитель: максимум секунд на одну партию (0 — без лимита)")]
    public float maxMatchSeconds = 30f;

    [Tooltip("Общий лимит времени бенчмарка в минутах (0 — без лимита)")]
    public float maxTotalMinutes = 45f;

    public int randomSeed = 12345;
    public bool alternateFirstPlayer = true;
    public bool useTiebreak = true;

    [Header("Output")]
    public string outputFolderName = "BenchmarkResults";
    public bool saveDetailedLogs = true;
    public bool verboseLogging = true;

    private class ProfileEntry
    {
        public string name;
        public string shortName;
        public AiProfile profile;

        public ProfileEntry(string name, string shortName, AiProfile profile)
        {
            this.name = name;
            this.shortName = shortName;
            this.profile = profile;
        }
    }

    private class BenchmarkPair
    {
        public int a;
        public int b;

        public BenchmarkPair(int a, int b)
        {
            this.a = a;
            this.b = b;
        }
    }

    private class AgentMatchStats
    {
        public int decisions;
        public double totalMs;
        public long nodes;
        public long cutoffs;
        public long ttHits;
        public long depthSum;
    }

    private class MatchResult
    {
        public int winner; // 1 — первый агент, 2 — второй, 0 — ничья/прерывание
        public int actions;
        public double seconds;
        public bool aborted;
        public bool tiebreak;
        public AgentMatchStats stats1 = new AgentMatchStats();
        public AgentMatchStats stats2 = new AgentMatchStats();
    }

    private class MatchRecord
    {
        public int pairIndex;
        public int matchIndex;
        public int a;
        public int b;
        public bool firstIsA;
        public int winner;
        public int actions;
        public double seconds;
        public bool aborted;
        public bool tiebreak;
        public AgentMatchStats stats1;
        public AgentMatchStats stats2;
    }

    private class PairResult
    {
        public int a;
        public int b;
        public int matches;
        public int winsA;
        public int winsB;
        public int draws;
        public int aborted;
        public double timeA;
        public double timeB;
        public long nodesA;
        public long nodesB;
        public int decisionsA;
        public int decisionsB;
    }

    private class ProfileAggregate
    {
        public int index;
        public string name;
        public string shortName;
        public int matches;
        public int wins;
        public int losses;
        public int draws;
        public int aborted;
        public int decisions;
        public double totalMs;
        public long nodes;
        public long cutoffs;
        public long ttHits;
        public long depthSum;
        public double elo = 1500.0;

        public double WinRate
        {
            get { return matches > 0 ? (double)wins / matches * 100.0 : 0.0; }
        }

        public double AvgMs
        {
            get { return decisions > 0 ? totalMs / decisions : 0.0; }
        }

        public double AvgNodes
        {
            get { return decisions > 0 ? (double)nodes / decisions : 0.0; }
        }

        public double AvgDepth
        {
            get { return decisions > 0 ? (double)depthSum / decisions : 0.0; }
        }

        public double CutoffRate
        {
            get { return nodes > 0 ? (double)cutoffs / nodes * 100.0 : 0.0; }
        }

        public double TtRate
        {
            get { return nodes > 0 ? (double)ttHits / nodes * 100.0 : 0.0; }
        }

        public double Ci95
        {
            get
            {
                if (matches <= 0) return 0.0;
                double p = (double)wins / matches;
                return 1.96 * Math.Sqrt(p * (1.0 - p) / matches) * 100.0;
            }
        }
    }

    private List<ProfileEntry> profiles = new List<ProfileEntry>();
    private List<BenchmarkPair> pairs = new List<BenchmarkPair>();
    private List<PairResult> pairResults = new List<PairResult>();
    private List<MatchRecord> matchRecords = new List<MatchRecord>();
    private Dictionary<int, ProfileAggregate> aggregates = new Dictionary<int, ProfileAggregate>();

    private StringBuilder detailedLog = new StringBuilder();
    private Stopwatch totalTimer = new Stopwatch();
    private string outputDir;
    private DateTime startedAt;

    public void Start()
    {
        // Бенчмарк запускается вручную из контекстного меню компонента
        // или через Assets/Editor/AIBenchmarkRunner.cs.
    }

    [ContextMenu("Run AI Benchmark")]
    public void RunBenchmarks()
    {
        startedAt = DateTime.Now;
        totalTimer = Stopwatch.StartNew();
        detailedLog = new StringBuilder();
        pairResults = new List<PairResult>();
        matchRecords = new List<MatchRecord>();
        aggregates = new Dictionary<int, ProfileAggregate>();

        profiles = BuildProfiles();
        for (int i = 0; i < profiles.Count; i++)
        {
            aggregates[i] = new ProfileAggregate
            {
                index = i,
                name = profiles[i].name,
                shortName = profiles[i].shortName
            };
        }

        PrepareOutput();

        Log("=== AI BENCHMARK STARTED ===");
        Log("Mode: " + mode);
        Log("Matches per pair: " + matchesPerPair);
        Log("Max actions per match: " + maxActionsPerMatch);
        Log("Max seconds per match: " + maxMatchSeconds.ToString("F1", CultureInfo.InvariantCulture));
        Log("Random seed: " + randomSeed);
        Log("");
        Log("Profiles:");
        for (int i = 0; i < profiles.Count; i++)
            Log("  [" + i + "] " + profiles[i].name + " (" + profiles[i].shortName + ")");
        Log("");

        pairs = BuildPairs(profiles);
        Log("Pairs to play: " + pairs.Count);
        Log("");

        bool stoppedByTime = false;

        for (int pi = 0; pi < pairs.Count; pi++)
        {
            if (maxTotalMinutes > 0 && totalTimer.Elapsed.TotalMinutes >= maxTotalMinutes)
            {
                stoppedByTime = true;
                Log("Total time limit reached. Stopping benchmark.");
                break;
            }

            RunPair(pi, pairs[pi], profiles);
            WriteReports(false);
        }

        totalTimer.Stop();

        Log("=== AI BENCHMARK FINISHED in " +
            totalTimer.Elapsed.TotalMinutes.ToString("F1", CultureInfo.InvariantCulture) + " min ===");

        WriteReports(stoppedByTime);
    }

    private List<ProfileEntry> BuildProfiles()
    {
        List<ProfileEntry> list = new List<ProfileEntry>();
        list.Add(new ProfileEntry("Rule-Based", "Rule", AiProfileFactory.RuleBased()));
        list.Add(new ProfileEntry("Minimax без отсечения D=2", "MM-D2", AiProfileFactory.MinimaxClassic(2)));
        list.Add(new ProfileEntry("Minimax α-β D=2", "AB-D2", AiProfileFactory.AlphaBeta(2)));
        list.Add(new ProfileEntry("Minimax α-β D=3", "AB-D3", AiProfileFactory.AlphaBeta(3)));
        list.Add(new ProfileEntry("Minimax α-β без сортировки D=3", "AB-NO-D3", AiProfileFactory.AlphaBetaNoOrdering(3)));
        list.Add(new ProfileEntry("Minimax α-β + ID (100 мс)", "AB-ID", AiProfileFactory.IterativeDeepening(4, 100)));
        list.Add(new ProfileEntry("Minimax α-β + TT D=3", "AB-TT-D3", AiProfileFactory.Transposition(3)));
        list.Add(new ProfileEntry("Minimax α-β + quiescence D=2", "AB-Q-D2", AiProfileFactory.Quiescence(2)));
        list.Add(new ProfileEntry("Minimax α-β + PVS D=3", "AB-PVS-D3", AiProfileFactory.Pvs(3)));
        list.Add(new ProfileEntry("Expectimax D=2", "EXP-D2", AiProfileFactory.Expectimax(2)));
        list.Add(new ProfileEntry("PIMC D=2 (6 детерминизаций)", "PIMC-D2", AiProfileFactory.Pimc(2, 6)));
        list.Add(new ProfileEntry("MCTS 300 итераций", "MCTS-300", AiProfileFactory.Mcts(300, 6)));
        return list;
    }

    private List<BenchmarkPair> BuildPairs(List<ProfileEntry> allProfiles)
    {
        List<BenchmarkPair> result = new List<BenchmarkPair>();

        if (mode == BenchmarkMode.RoundRobin)
        {
            for (int i = 0; i < allProfiles.Count; i++)
                for (int j = i + 1; j < allProfiles.Count; j++)
                    result.Add(new BenchmarkPair(i, j));

            return result;
        }

        List<int> opponentIndices = new List<int>();
        string[] names = gauntletOpponents;
        if (names == null || names.Length == 0)
            names = new string[] { "Rule" };

        foreach (string name in names)
        {
            int index = allProfiles.FindIndex(p => p.shortName == name || p.name == name);
            if (index >= 0)
                opponentIndices.Add(index);
            else
                Debug.LogWarning("AIBenchmark: unknown gauntlet opponent '" + name + "'");
        }

        if (opponentIndices.Count == 0)
            opponentIndices.Add(0);

        HashSet<long> seen = new HashSet<long>();
        for (int i = 0; i < allProfiles.Count; i++)
        {
            foreach (int opponent in opponentIndices)
            {
                if (i == opponent)
                    continue;

                int a = Mathf.Min(i, opponent);
                int b = Mathf.Max(i, opponent);
                long key = ((long)a << 32) | (uint)b;

                if (seen.Add(key))
                    result.Add(new BenchmarkPair(a, b));
            }
        }

        result.Sort((x, y) => x.a != y.a ? x.a.CompareTo(y.a) : x.b.CompareTo(y.b));
        return result;
    }

    private void RunPair(int pairIndex, BenchmarkPair pair, List<ProfileEntry> allProfiles)
    {
        ProfileEntry first = allProfiles[pair.a];
        ProfileEntry second = allProfiles[pair.b];
        PairResult pairResult = new PairResult { a = pair.a, b = pair.b };

        Log("--- Pair " + (pairIndex + 1) + ": " + first.shortName + " vs " + second.shortName + " ---");

        int mirrorSeed = randomSeed + pairIndex * 100000;

        for (int matchIndex = 0; matchIndex < matchesPerPair; matchIndex++)
        {
            if (maxTotalMinutes > 0 && totalTimer.Elapsed.TotalMinutes >= maxTotalMinutes)
                break;

            // Две партии с одинаковыми колодами; агенты по очереди начинают,
            // что компенсирует преимущество первого хода.
            int seed = mirrorSeed + (matchIndex / 2) * 1000;
            bool firstIsA = !alternateFirstPlayer || (matchIndex % 2 == 0);

            MatchResult match = SimulateMatch(first.profile, second.profile, firstIsA, seed);

            pairResult.matches++;
            if (match.winner == 1) pairResult.winsA++;
            else if (match.winner == 2) pairResult.winsB++;
            else pairResult.draws++;
            if (match.aborted) pairResult.aborted++;

            pairResult.timeA += match.stats1.totalMs;
            pairResult.timeB += match.stats2.totalMs;
            pairResult.nodesA += match.stats1.nodes;
            pairResult.nodesB += match.stats2.nodes;
            pairResult.decisionsA += match.stats1.decisions;
            pairResult.decisionsB += match.stats2.decisions;

            matchRecords.Add(new MatchRecord
            {
                pairIndex = pairIndex,
                matchIndex = matchIndex,
                a = pair.a,
                b = pair.b,
                firstIsA = firstIsA,
                winner = match.winner,
                actions = match.actions,
                seconds = match.seconds,
                aborted = match.aborted,
                tiebreak = match.tiebreak,
                stats1 = match.stats1,
                stats2 = match.stats2
            });

            UpdateAggregates(pair.a, pair.b, match);

            string winnerName = match.winner == 0
                ? "draw"
                : (match.winner == 1 ? first.shortName : second.shortName);

            Log("  match " + (matchIndex + 1) + "/" + matchesPerPair +
                " first=" + (firstIsA ? first.shortName : second.shortName) +
                " winner=" + winnerName +
                " actions=" + match.actions +
                " time=" + match.seconds.ToString("F1", CultureInfo.InvariantCulture) + "s" +
                (match.aborted ? " ABORTED" : "") +
                (match.tiebreak ? " TIEBREAK" : ""));
        }

        pairResults.Add(pairResult);
    }

    private MatchResult SimulateMatch(AiProfile profile1, AiProfile profile2, bool firstIsA, int seed)
    {
        MatchResult result = new MatchResult();
        SimGameState state = CreateInitialState(seed);

        IAiAgent agent1 = CreateAgent(profile1);
        IAiAgent agent2 = CreateAgent(profile2);

        // Для воспроизводимости MCTS, который использует UnityEngine.Random.
        Random.InitState(seed);

        bool isAgent1Turn = firstIsA;
        int actionCount = 0;
        Stopwatch matchTimer = Stopwatch.StartNew();

        while (actionCount < maxActionsPerMatch && !SimRules.IsTerminal(state))
        {
            if (maxMatchSeconds > 0 && matchTimer.Elapsed.TotalSeconds >= maxMatchSeconds)
            {
                result.aborted = true;
                break;
            }

            IAiAgent currentAgent = isAgent1Turn ? agent1 : agent2;
            AiProfile currentProfile = isAgent1Turn ? profile1 : profile2;

            List<SimAction> actions = SimActionGenerator.GenerateActions(state);

            if (actions == null || actions.Count == 0)
            {
                SimRules.EndTurn(state, false);
                isAgent1Turn = !isAgent1Turn;
                actionCount++;
                continue;
            }

            Stopwatch decisionTimer = Stopwatch.StartNew();
            SimAction chosenAction = null;

            try
            {
                chosenAction = currentAgent.ChooseAction(state, actions, currentProfile);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("AIBenchmark: agent exception: " + exception.Message);
                chosenAction = actions[actions.Count - 1];
            }

            decisionTimer.Stop();

            AgentMatchStats stats = isAgent1Turn ? result.stats1 : result.stats2;
            stats.decisions++;
            stats.totalMs += decisionTimer.Elapsed.TotalMilliseconds;

            if (currentAgent is IAiStatsProvider provider)
            {
                AiSearchStats searchStats = provider.LastStats;
                if (searchStats != null)
                {
                    stats.nodes += searchStats.nodes;
                    stats.cutoffs += searchStats.cutoffs;
                    stats.ttHits += searchStats.ttHits;
                    stats.depthSum += searchStats.depthReached;
                }
            }

            if (chosenAction == null)
                chosenAction = actions[actions.Count - 1];

            // В самой партии добор детерминированный: колода уже перемешана
            // от seed, поэтому последовательность добора однозначно определена.
            SimRules.ApplyAction(state, chosenAction, false);

            if (chosenAction.actionType == SimActionType.EndTurn)
                isAgent1Turn = !isAgent1Turn;

            actionCount++;
        }

        matchTimer.Stop();
        result.actions = actionCount;
        result.seconds = matchTimer.Elapsed.TotalSeconds;

        if (result.aborted)
        {
            result.winner = 0;
        }
        else if (SimRules.IsTerminal(state))
        {
            result.winner = DetermineWinner(state, isAgent1Turn);
        }
        else
        {
            // Партия не завершилась за отведенное число действий.
            // В игре нет усталости, поэтому возможны позиции без летального
            // исхода. Для эксперимента используется детерминированный
            // тай-брейк: здоровье героя, затем сила стола.
            result.tiebreak = useTiebreak;
            result.winner = useTiebreak ? DetermineWinnerByScore(state, isAgent1Turn) : 0;
        }

        return result;
    }

    private int DetermineWinnerByScore(SimGameState state, bool isAgent1Turn)
    {
        int currentScore = state.me.heroHp * 100 + BoardScore(state.me.board);
        int opponentScore = state.enemy.heroHp * 100 + BoardScore(state.enemy.board);

        if (currentScore == opponentScore)
            return 0;

        bool currentWins = currentScore > opponentScore;
        if (currentWins)
            return isAgent1Turn ? 1 : 2;

        return isAgent1Turn ? 2 : 1;
    }

    private static int BoardScore(System.Collections.Generic.List<SimCard> board)
    {
        int score = 0;
        for (int i = 0; i < board.Count; i++)
            score += board[i].attack * 2 + board[i].hp;
        return score;
    }

    private int DetermineWinner(SimGameState state, bool isAgent1Turn)
    {
        if (state.me.heroHp <= 0 && state.enemy.heroHp <= 0)
            return 0;

        if (state.enemy.heroHp <= 0)
            return isAgent1Turn ? 1 : 2;

        if (state.me.heroHp <= 0)
            return isAgent1Turn ? 2 : 1;

        return 0;
    }

    private SimGameState CreateInitialState(int seed)
    {
        // state.me — сторона, которая ходит первой, state.enemy — вторая.
        // Привязка к конкретному агенту выполняется через isAgent1Turn,
        // поэтому в зеркальных партиях первый игрок всегда получает одну
        // и ту же колоду, а второй — другую.
        SimPlayerState firstPlayer = CreatePlayer(seed);
        SimPlayerState secondPlayer = CreatePlayer(unchecked(seed * 31 + 17));

        SimGameState state = new SimGameState();
        state.me = firstPlayer;
        state.enemy = secondPlayer;

        // Первый ход даёт 1 ману, как и в игровом цикле Unity.
        state.orderKol = 1;
        state.maxManaCap = 5;
        state.isMyTurn = true;
        state.turnInRound = 0;
        state.me.mana = 1;
        state.me.maxMana = 1;
        state.enemy.mana = 0;
        state.enemy.maxMana = 0;

        DrawStartingHand(state.me, 3);
        DrawStartingHand(state.enemy, 3);
        return state;
    }

    private SimPlayerState CreatePlayer(int seed)
    {
        SimPlayerState player = new SimPlayerState();
        player.heroHp = 20;
        player.deck = CreateDeck(seed);
        return player;
    }

    private static void DrawStartingHand(SimPlayerState player, int count)
    {
        for (int i = 0; i < count && player.deck.Count > 0; i++)
        {
            SimCard card = player.deck[0];
            player.deck.RemoveAt(0);
            card.canAttack = false;
            player.hand.Add(card);
        }
    }

    private List<SimCard> CreateDeck(int seed)
    {
        List<SimCard> deck = new List<SimCard>();

        AddCards(deck, "Goblin", 1, 1, 3, false, 4);
        AddCards(deck, "Thief", 2, 3, 1, false, 3);
        AddCards(deck, "Knight", 3, 3, 4, true, 3);
        AddCards(deck, "Mage", 4, 5, 4, false, 2);

        System.Random rng = new System.Random(seed);
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            SimCard temp = deck[i];
            deck[i] = deck[j];
            deck[j] = temp;
        }

        return deck;
    }

    private static void AddCards(List<SimCard> deck, string name, int mana, int attack,
                                 int hp, bool provoc, int count)
    {
        for (int i = 0; i < count; i++)
        {
            deck.Add(new SimCard
            {
                cardName = name,
                mana = mana,
                attack = attack,
                hp = hp,
                provoc = provoc
            });
        }
    }

    private IAiAgent CreateAgent(AiProfile profile)
    {
        switch (profile.aiType)
        {
            case AiType.Minimax:
            case AiType.Expectimax:
                return new MinimaxAgent();
            case AiType.Mcts:
                return new MctsAgent();
            default:
                return new RuleBasedAgent();
        }
    }

    private void UpdateAggregates(int a, int b, MatchResult match)
    {
        ProfileAggregate ga = aggregates[a];
        ProfileAggregate gb = aggregates[b];

        ga.matches++;
        gb.matches++;

        if (match.winner == 1)
        {
            ga.wins++;
            gb.losses++;
        }
        else if (match.winner == 2)
        {
            gb.wins++;
            ga.losses++;
        }
        else
        {
            ga.draws++;
            gb.draws++;
        }

        if (match.aborted)
        {
            ga.aborted++;
            gb.aborted++;
        }

        MergeStats(ga, match.stats1);
        MergeStats(gb, match.stats2);

        double expectedA = 1.0 / (1.0 + Math.Pow(10.0, (gb.elo - ga.elo) / 400.0));
        double scoreA = match.winner == 1 ? 1.0 : (match.winner == 2 ? 0.0 : 0.5);
        const double k = 16.0;

        ga.elo += k * (scoreA - expectedA);
        gb.elo += k * ((1.0 - scoreA) - (1.0 - expectedA));
    }

    private static void MergeStats(ProfileAggregate aggregate, AgentMatchStats stats)
    {
        aggregate.decisions += stats.decisions;
        aggregate.totalMs += stats.totalMs;
        aggregate.nodes += stats.nodes;
        aggregate.cutoffs += stats.cutoffs;
        aggregate.ttHits += stats.ttHits;
        aggregate.depthSum += stats.depthSum;
    }

    private void PrepareOutput()
    {
        string projectRoot = null;

        try
        {
            DirectoryInfo parent = Directory.GetParent(Application.dataPath);
            if (parent != null)
                projectRoot = parent.FullName;
        }
        catch
        {
            projectRoot = null;
        }

        if (string.IsNullOrEmpty(projectRoot))
            projectRoot = Application.persistentDataPath;

        string folder = string.IsNullOrEmpty(outputFolderName) ? "BenchmarkResults" : outputFolderName;
        outputDir = Path.Combine(projectRoot, folder);

        try
        {
            Directory.CreateDirectory(outputDir);
        }
        catch (Exception exception)
        {
            Debug.LogError("AIBenchmark: cannot create output directory: " + exception.Message);
            outputDir = Application.persistentDataPath;
            Directory.CreateDirectory(outputDir);
        }
    }

    private void WriteReports(bool stoppedByTime)
    {
        if (string.IsNullOrEmpty(outputDir))
            return;

        WriteMatchesCsv();
        WritePairsCsv();
        WriteSummaryCsv();
        WriteMarkdown(stoppedByTime);

        if (saveDetailedLogs)
            WriteDetailedLog();
    }

    private void WriteMatchesCsv()
    {
        StringBuilder csv = new StringBuilder();
        csv.AppendLine("pair;match;agent1;agent2;first;winner;actions;seconds;aborted;tiebreak;a1_ms;a2_ms;a1_nodes;a2_nodes;a1_depth;a2_depth");

        foreach (MatchRecord record in matchRecords)
        {
            string nameA = profiles[record.a].shortName;
            string nameB = profiles[record.b].shortName;
            string winner = record.winner == 1 ? nameA : (record.winner == 2 ? nameB : "draw");

            csv.AppendLine(string.Join(";", new string[]
            {
                (record.pairIndex + 1).ToString(CultureInfo.InvariantCulture),
                (record.matchIndex + 1).ToString(CultureInfo.InvariantCulture),
                nameA,
                nameB,
                record.firstIsA ? nameA : nameB,
                winner,
                record.actions.ToString(CultureInfo.InvariantCulture),
                F2(record.seconds),
                record.aborted ? "1" : "0",
                record.tiebreak ? "1" : "0",
                F2(AvgMs(record.stats1)),
                F2(AvgMs(record.stats2)),
                F2(AvgNodes(record.stats1)),
                F2(AvgNodes(record.stats2)),
                F2(AvgDepth(record.stats1)),
                F2(AvgDepth(record.stats2))
            }));
        }

        WriteTextFile(Path.Combine(outputDir, "benchmark_matches.csv"), csv.ToString());
    }

    private void WritePairsCsv()
    {
        StringBuilder csv = new StringBuilder();
        csv.AppendLine("pair;agent1;agent2;matches;wins1;wins2;draws;aborted;winrate1;winrate2;avg_ms1;avg_ms2;avg_nodes1;avg_nodes2");

        for (int i = 0; i < pairResults.Count; i++)
        {
            PairResult pair = pairResults[i];
            double winRateA = pair.matches > 0 ? (double)pair.winsA / pair.matches * 100.0 : 0.0;
            double winRateB = pair.matches > 0 ? (double)pair.winsB / pair.matches * 100.0 : 0.0;

            csv.AppendLine(string.Join(";", new string[]
            {
                (i + 1).ToString(CultureInfo.InvariantCulture),
                profiles[pair.a].shortName,
                profiles[pair.b].shortName,
                pair.matches.ToString(CultureInfo.InvariantCulture),
                pair.winsA.ToString(CultureInfo.InvariantCulture),
                pair.winsB.ToString(CultureInfo.InvariantCulture),
                pair.draws.ToString(CultureInfo.InvariantCulture),
                pair.aborted.ToString(CultureInfo.InvariantCulture),
                F2(winRateA),
                F2(winRateB),
                F2(pair.decisionsA > 0 ? pair.timeA / pair.decisionsA : 0.0),
                F2(pair.decisionsB > 0 ? pair.timeB / pair.decisionsB : 0.0),
                F2(pair.decisionsA > 0 ? (double)pair.nodesA / pair.decisionsA : 0.0),
                F2(pair.decisionsB > 0 ? (double)pair.nodesB / pair.decisionsB : 0.0)
            }));
        }

        WriteTextFile(Path.Combine(outputDir, "benchmark_pairs.csv"), csv.ToString());
    }

    private void WriteSummaryCsv()
    {
        StringBuilder csv = new StringBuilder();
        csv.AppendLine("rank;agent;short;matches;wins;losses;draws;aborted;winrate;ci95;avg_ms;avg_nodes;avg_depth;cutoff_rate;tt_rate;elo");

        List<ProfileAggregate> sorted = SortedAggregates();
        for (int i = 0; i < sorted.Count; i++)
        {
            ProfileAggregate aggregate = sorted[i];
            csv.AppendLine(string.Join(";", new string[]
            {
                (i + 1).ToString(CultureInfo.InvariantCulture),
                aggregate.name,
                aggregate.shortName,
                aggregate.matches.ToString(CultureInfo.InvariantCulture),
                aggregate.wins.ToString(CultureInfo.InvariantCulture),
                aggregate.losses.ToString(CultureInfo.InvariantCulture),
                aggregate.draws.ToString(CultureInfo.InvariantCulture),
                aggregate.aborted.ToString(CultureInfo.InvariantCulture),
                F2(aggregate.WinRate),
                F2(aggregate.Ci95),
                F2(aggregate.AvgMs),
                F2(aggregate.AvgNodes),
                F2(aggregate.AvgDepth),
                F2(aggregate.CutoffRate),
                F2(aggregate.TtRate),
                F2(aggregate.elo)
            }));
        }

        WriteTextFile(Path.Combine(outputDir, "benchmark_summary.csv"), csv.ToString());
    }

    private void WriteMarkdown(bool stoppedByTime)
    {
        StringBuilder md = new StringBuilder();
        md.AppendLine("# Результаты автоматизированного тестирования AI");
        md.AppendLine();
        md.AppendLine("- Дата: " + startedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
        md.AppendLine("- Режим: " + (mode == BenchmarkMode.Gauntlet ? "gauntlet" : "round-robin"));
        md.AppendLine("- Партий на пару: " + matchesPerPair);
        md.AppendLine("- Seed: " + randomSeed);
        md.AppendLine("- Общее время: " + F1(totalTimer.Elapsed.TotalMinutes) + " мин");

        if (stoppedByTime)
            md.AppendLine("- **Бенчмарк остановлен по общему лимиту времени, результаты неполные.**");

        md.AppendLine();
        md.AppendLine("## Конфигурации вариантов");
        md.AppendLine();
        md.AppendLine("| # | Вариант | Глубина | Лимит, мс | α-β | Порядок | ID | TT | Q | PVS | EXP | PIMC |");
        md.AppendLine("|---:|---|---:|---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|");

        for (int i = 0; i < profiles.Count; i++)
        {
            AiProfile p = profiles[i].profile;
            md.AppendLine("| " + (i + 1) + " | " + profiles[i].name + " | " +
                          p.minimaxDepth + " | " +
                          (p.timeLimitMs > 0 ? p.timeLimitMs.ToString(CultureInfo.InvariantCulture) : "—") + " | " +
                          Mark(p.useAlphaBeta) + " | " +
                          Mark(p.useMoveOrdering) + " | " +
                          Mark(p.useIterativeDeepening) + " | " +
                          Mark(p.useTranspositionTable) + " | " +
                          Mark(p.useQuiescence) + " | " +
                          Mark(p.usePvs) + " | " +
                          Mark(p.useExpectimax) + " | " +
                          (p.pimcSamples > 0 ? p.pimcSamples.ToString(CultureInfo.InvariantCulture) : "—") + " |");
        }

        md.AppendLine();
        md.AppendLine("## Итоговые результаты");
        md.AppendLine();
        md.AppendLine("| Место | Вариант | Матчи | W | L | D | Winrate, % | ±95% | Время, мс | Узлы | Эло |");
        md.AppendLine("|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|");

        List<ProfileAggregate> sorted = SortedAggregates();
        for (int i = 0; i < sorted.Count; i++)
        {
            ProfileAggregate aggregate = sorted[i];
            md.AppendLine("| " + (i + 1) + " | " + aggregate.name + " | " +
                          aggregate.matches + " | " +
                          aggregate.wins + " | " +
                          aggregate.losses + " | " +
                          aggregate.draws + " | " +
                          F1(aggregate.WinRate) + " | " +
                          "±" + F1(aggregate.Ci95) + " | " +
                          F1(aggregate.AvgMs) + " | " +
                          F1(aggregate.AvgNodes) + " | " +
                          F1(aggregate.elo) + " |");
        }

        md.AppendLine();
        md.AppendLine("Время и узлы усреднены на одно решение агента. " +
                      "Прерванные по таймауту партии считаются ничьими и отмечены в CSV.");

        WriteTextFile(Path.Combine(outputDir, "benchmark_summary.md"), md.ToString());
    }

    private void WriteDetailedLog()
    {
        WriteTextFile(Path.Combine(outputDir, "benchmark_log.txt"), detailedLog.ToString());
    }

    private List<ProfileAggregate> SortedAggregates()
    {
        return aggregates.Values
            .OrderByDescending(a => a.elo)
            .ThenByDescending(a => a.wins)
            .ThenBy(a => a.name)
            .ToList();
    }

    private static double AvgMs(AgentMatchStats stats)
    {
        return stats.decisions > 0 ? stats.totalMs / stats.decisions : 0.0;
    }

    private static double AvgNodes(AgentMatchStats stats)
    {
        return stats.decisions > 0 ? (double)stats.nodes / stats.decisions : 0.0;
    }

    private static double AvgDepth(AgentMatchStats stats)
    {
        return stats.decisions > 0 ? (double)stats.depthSum / stats.decisions : 0.0;
    }

    private void Log(string line)
    {
        detailedLog.AppendLine(line);
        if (verboseLogging)
            Debug.Log(line);
    }

    private static string Mark(bool value)
    {
        return value ? "+" : "—";
    }

    private static string F1(double value)
    {
        return value.ToString("F1", CultureInfo.InvariantCulture);
    }

    private static string F2(double value)
    {
        return value.ToString("F2", CultureInfo.InvariantCulture);
    }

    private void WriteTextFile(string path, string content)
    {
        try
        {
            File.WriteAllText(path, content, new UTF8Encoding(true));
        }
        catch (Exception exception)
        {
            Debug.LogError("AIBenchmark: cannot write " + path + ": " + exception.Message);
        }
    }
}
