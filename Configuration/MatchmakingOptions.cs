namespace MatchmakingService.Configuration;

public class MatchmakingOptions
{
    public int MinPlayersPerMatch { get; set; }

    public int MaxPlayersPerMatch { get; set; }

    public int CountdownSeconds { get; set; }

    public int ProcessingIntervalSeconds { get; set; }
}