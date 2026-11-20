/// <summary>
/// Статистика одного вызова ChooseAction. Используется бенчмарком
/// для сравнения не только силы игры, но и вычислительной эффективности.
/// </summary>
public class AiSearchStats
{
    public long nodes;
    public long cutoffs;
    public long ttHits;
    public int depthReached;
    public long elapsedMs;

    public void Reset()
    {
        nodes = 0;
        cutoffs = 0;
        ttHits = 0;
        depthReached = 0;
        elapsedMs = 0;
    }
}

public interface IAiStatsProvider
{
    AiSearchStats LastStats { get; }
}
