namespace MatchmakingService.Observability;

public sealed record MatchmakingMetricsSnapshot(
    long MatchCreationFailures,
    long MatchCreationAttempts,
    double MatchCreationLatencySum,
    double MatchCreationLatencyMinimum,
    double MatchCreationLatencyMaximum
);