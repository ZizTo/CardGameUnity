using UnityEngine;

[System.Serializable]
public class AiProfile
{
    public AiType aiType = AiType.RuleBased;

    public int minimaxDepth = 2;
    public int mctsIterations = 100;
    public int rolloutDepth = 6;
    public bool useAlphaBeta = true;
    
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