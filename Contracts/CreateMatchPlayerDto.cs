using MatchmakingService.Models;

namespace MatchmakingService.Contracts;

public class CreateMatchPlayerDto
{
    public string PlayerId { get; set; } = string.Empty;

    public PlayerRole Role { get; set; }
}