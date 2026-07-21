namespace MatchmakingService.Observability;

public sealed class MatchmakingMetrics
    : IMatchmakingMetrics
{
    private long _matchCreationFailures;

    private readonly object _latencyLock = new();

    private long _matchCreationAttempts;

    private double _latencySum;

    private double _latencyMinimum =
        double.MaxValue;

    private double _latencyMaximum;

    public void RecordMatchCreationFailure()
    {
        Interlocked.Increment(
            ref _matchCreationFailures
        );
    }

    public void RecordMatchCreationLatency(
        double elapsedMilliseconds)
    {
        lock (_latencyLock)
        {
            _matchCreationAttempts++;

            _latencySum +=
                elapsedMilliseconds;

            _latencyMinimum = Math.Min(
                _latencyMinimum,
                elapsedMilliseconds
            );

            _latencyMaximum = Math.Max(
                _latencyMaximum,
                elapsedMilliseconds
            );
        }
    }

    public MatchmakingMetricsSnapshot TakeSnapshot()
    {
        var failures = Interlocked.Exchange(
            ref _matchCreationFailures,
            0
        );

        lock (_latencyLock)
        {
            var attempts =
                _matchCreationAttempts;

            var minimum =
                attempts == 0
                    ? 0
                    : _latencyMinimum;

            var snapshot =
                new MatchmakingMetricsSnapshot(
                    failures,
                    attempts,
                    _latencySum,
                    minimum,
                    _latencyMaximum
                );

            _matchCreationAttempts = 0;
            _latencySum = 0;
            _latencyMinimum =
                double.MaxValue;
            _latencyMaximum = 0;

            return snapshot;
        }
    }
}