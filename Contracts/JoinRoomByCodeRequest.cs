namespace MatchmakingService.Contracts;

public class JoinRoomByCodeRequest
{
    public string PlayerId { get; set; } = string.Empty;

    public string InviteCode { get; set; } = string.Empty;
}
