using MatchmakingService.Contracts;

namespace MatchmakingService.Services;

public interface IMatchmakingService
{
    Task<JoinQueueResponse> JoinQueueAsync(JoinQueueRequest request);

    MatchmakingStatusResponse GetStatus(string playerId);
}