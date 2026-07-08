namespace MatchmakingService.Contracts;

public class CreateMatchResponse
{
    public string MatchId { get; set; } = string.Empty;

    public string GameServiceId { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
}