namespace MatchmakingService.Observability;

public interface IMatchmakingMetrics
{
    void RecordMatchCreationFailure();

    void RecordMatchCreationLatency(
        double elapsedMilliseconds
    );

    MatchmakingMetricsSnapshot TakeSnapshot();
}