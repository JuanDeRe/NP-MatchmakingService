namespace MatchmakingService.Contracts;

public class MatchmakingStatusResponse
{
    public string PlayerId { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? MatchId { get; set; }

    public string? GameServiceId { get; set; }

    public string? Role { get; set; }

    public string? AnimatronicType { get; set; }

    public string? InviteCode { get; set; }

    public int CurrentPlayers { get; set; }

    public int MaxPlayers { get; set; }

    public DateTime? CountdownEndsAtUtc { get; set; }
}