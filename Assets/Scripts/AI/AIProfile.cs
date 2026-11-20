using UnityEngine;

[System.Serializable]
public class AiProfile
{
    public AiType aiType = AiType.RuleBased;

    [Header("Basic")]
    public int minimaxDepth = 2;
    public int mctsIterations = 100;
    public int rolloutDepth = 6;
    public bool useAlphaBeta = true;

    [Header("Minimax options")]
    [Tooltip("0 — без ограничения по времени, иначе бюджет времени на один ход в миллисекундах")]
    public int timeLimitMs = 0;
    public bool useMoveOrdering = true;
    public bool useIterativeDeepening = false;
    public bool useTranspositionTable = false;
    public bool useQuiescence = false;
    public bool usePvs = false;
    public bool useExpectimax = false;
    public int quiescenceDepth = 3;

    [Tooltip("Количество детерминизаций для PIMC (0 — режим выключен)")]
    public int pimcSamples = 0;
    public int pimcSeed = 12345;

    [Tooltip("Максимальное число рассматриваемых действий в узле (0 — без ограничения)")]
    public int maxBranching = 6;

    [Tooltip("Максимальное число действий одного игрока за один ход в поиске")]
    public int maxActionsPerTurn = 4;

    public AiProfile() {}

    public AiProfile(AiType naiType, int nminimaxDepth, int nmctsIterations, int nrolloutDepth, bool nuseAlphaBeta)
    {
        aiType = naiType;
        minimaxDepth = nminimaxDepth;
        mctsIterations = nmctsIterations;
        rolloutDepth = nrolloutDepth;
        useAlphaBeta = nuseAlphaBeta;
    }
}
