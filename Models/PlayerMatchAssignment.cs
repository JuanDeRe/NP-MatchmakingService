namespace MatchmakingService.Models;

public class PlayerMatchAssignment
{
    public string PlayerId { get; set; } = string.Empty;

    public string MatchId { get; set; } = string.Empty;

    public string GameServiceId { get; set; } = string.Empty;

    public PlayerRole Role { get; set; }

    public DateTime AssignedAtUtc { get; set; } = DateTime.UtcNow;
    
    public AnimatronicType AnimatronicType { get; set; } = AnimatronicType.None;
}