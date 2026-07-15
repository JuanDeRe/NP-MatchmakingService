using MatchmakingService.Models;

namespace MatchmakingService.Registry;

public interface IGameServiceRegistry
{
    GameServiceInstance? FindAvailableInstance();

    void SaveMatchRoute(MatchRoute route);

    MatchRoute? GetMatchRoute(string matchId);

    void RemoveMatchRoute(string matchId);
}