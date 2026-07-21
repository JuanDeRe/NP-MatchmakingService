using MatchmakingService.Observability;

namespace MatchmakingService.Tests;

public sealed class FakeMatchmakingMetrics
    : IMatchmakingMetrics
{
    public void RecordMatchCreationFailure()
    {
    }

    public void RecordMatchCreationLatency(
        double elapsedMilliseconds)
    {
    }

    public MatchmakingMetricsSnapshot TakeSnapshot()
    {
        return new MatchmakingMetricsSnapshot(
            0,
            0,
            0,
            0,
            0
        );
    }
}