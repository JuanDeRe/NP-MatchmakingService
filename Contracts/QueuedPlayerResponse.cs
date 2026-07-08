namespace MatchmakingService.Contracts;

public class QueuedPlayerResponse
{
    public string PlayerId { get; set; } = string.Empty;

    public DateTime JoinedAtUtc { get; set; }
}