using MatchmakingService.Clients;
using MatchmakingService.Contracts;

namespace MatchmakingService.Tests;

public class FailOnceGameServiceClient : IGameServiceClient
{
    private bool _hasFailed;

    public Task<CreateMatchResponse?> CreateMatchAsync(CreateMatchRequest request)
    {
        if (!_hasFailed)
        {
            _hasFailed = true;
            return Task.FromResult<CreateMatchResponse?>(null);
        }

        return Task.FromResult<CreateMatchResponse?>(new CreateMatchResponse
        {
            MatchId = "match-test",
            GameServiceId = "game-service-local",
            Status = "Active"
        });
    }
}