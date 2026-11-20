using System;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Запуск бенчмарка из меню Unity и из командной строки (batchmode).
///
/// Меню: Tools -> AI Benchmark -> Run Gauntlet / Run Round Robin.
///
/// Batchmode:
/// Unity.exe -batchmode -quit -projectPath "путь/к/CardGameUnity"
///   -executeMethod AIBenchmarkRunner.RunFromCommandLine
///   -benchMode gauntlet -benchMatches 6 -benchSeed 12345
///   -logFile "путь/к/unity_benchmark.log"
/// </summary>
public static class AIBenchmarkRunner
{
    [MenuItem("Tools/AI Benchmark/Run Gauntlet")]
    public static void RunGauntlet()
    {
        RunInEditor(BenchmarkMode.Gauntlet, 6, 12345);
    }

    [MenuItem("Tools/AI Benchmark/Run Round Robin")]
    public static void RunRoundRobin()
    {
        // Полный round-robin дорогой: 12 профилей дают 66 пар.
        // Начинайте с 2 партий на пару, чтобы оценить время.
        RunInEditor(BenchmarkMode.RoundRobin, 10, 12348);
    }

    private static void RunInEditor(BenchmarkMode mode, int matches, int seed)
    {
        GameObject runner = new GameObject("AIBenchmarkRunner");
        AIBenchmark benchmark = runner.AddComponent<AIBenchmark>();
        benchmark.mode = mode;
        benchmark.matchesPerPair = matches;
        benchmark.randomSeed = seed;

        try
        {
            benchmark.RunBenchmarks();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(runner);
            AssetDatabase.Refresh();
        }
    }

    /// <summary>
    /// Точка входа для Unity -executeMethod. Аргументы после -executeMethod
    /// читаются из Environment.GetCommandLineArgs().
    /// </summary>
    public static void RunFromCommandLine()
    {
        BenchmarkMode mode = BenchmarkMode.Gauntlet;
        int matches = 6;
        int seed = 12345;

        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "-benchMode", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(args[i + 1], "roundrobin", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(args[i + 1], "round-robin", StringComparison.OrdinalIgnoreCase))
                    mode = BenchmarkMode.RoundRobin;
            }
            else if (string.Equals(args[i], "-benchMatches", StringComparison.OrdinalIgnoreCase))
            {
                int.TryParse(args[i + 1], out matches);
            }
            else if (string.Equals(args[i], "-benchSeed", StringComparison.OrdinalIgnoreCase))
            {
                int.TryParse(args[i + 1], out seed);
            }
        }

        GameObject runner = new GameObject("AIBenchmarkRunner");
        AIBenchmark benchmark = runner.AddComponent<AIBenchmark>();
        benchmark.mode = mode;
        benchmark.matchesPerPair = Mathf.Max(1, matches);
        benchmark.randomSeed = seed;

        try
        {
            benchmark.RunBenchmarks();
            UnityEngine.Object.DestroyImmediate(runner);
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            UnityEngine.Object.DestroyImmediate(runner);
            EditorApplication.Exit(1);
        }
    }
}
