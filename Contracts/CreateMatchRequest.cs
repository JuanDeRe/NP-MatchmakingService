namespace MatchmakingService.Contracts;

public class CreateMatchRequest
{
    public List<CreateMatchPlayerDto> Players { get; set; } = [];
}