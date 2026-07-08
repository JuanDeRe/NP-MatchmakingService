namespace MatchmakingService.Models;

public class QueuedPlayer
{
    public string PlayerId { get; set; } = string.Empty;

    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;
}