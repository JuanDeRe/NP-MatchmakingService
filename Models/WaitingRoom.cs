namespace  MatchmakingService.Models;

public class WaitingRoom
{
    public string RoomId { get; set; } = string.Empty;

    public string InviteCode { get; set; } = string.Empty;

    public WaitingRoomStatus Status { get; set; }

    public List<QueuedPlayer> Players { get; set; } = [];

    public string? CurrentMatchId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? CountdownStartedAtUtc { get; set; }
}