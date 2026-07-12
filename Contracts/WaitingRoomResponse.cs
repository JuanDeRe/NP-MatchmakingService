namespace MatchmakingService.Contracts;

public class WaitingRoomResponse
{
    public string RoomId { get; set; } = string.Empty;

    public string InviteCode { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public int CurrentPlayers { get; set; }

    public int MaxPlayers { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? CountdownStartedAtUtc { get; set; }
}