using MatchmakingService.Clients;
using MatchmakingService.Contracts;

namespace MatchmakingService.Tests;

public class FakeGameServiceClient : IGameServiceClient
{
    private readonly bool _shouldFail;

    public FakeGameServiceClient(bool shouldFail = false)
    {
        _shouldFail = shouldFail;
    }

    public Task<CreateMatchResponse?> CreateMatchAsync(CreateMatchRequest request)
    {
        if (_shouldFail)
        {
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