namespace MatchmakingService.Models;

public class GameServiceInstance
{
    public string InstanceId { get; set; } =
        string.Empty;

    public string InternalUrl { get; set; } =
        string.Empty;

    public int ActiveMatches { get; set; }

    public int MaxMatches { get; set; }

    public string Status { get; set; } =
        string.Empty;

    public DateTime LastHeartbeatUtc { get; set; }

    public bool HasCapacity =>
        Status == "Available" &&
        ActiveMatches < MaxMatches;
}