namespace MatchmakingService.Contracts;

public class JoinQueueResponse
{
    public string Status { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? MatchId { get; set; }

    public string? GameServiceId { get; set; }

    public string? Role { get; set; }
}