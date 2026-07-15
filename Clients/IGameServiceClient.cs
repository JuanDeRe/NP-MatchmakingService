using MatchmakingService.Contracts;

namespace MatchmakingService.Clients;

public interface IGameServiceClient
{
    Task<CreateMatchResponse?> CreateMatchAsync(
        CreateMatchRequest request,
        string gameServiceBaseUrl
    );
}