namespace MatchmakingService.Contracts;

public class ProcessWaitingRoomsResponse
{
    public int MatchesCreated { get; set; }

    public List<string> CreatedMatchIds { get; set; } = [];
}