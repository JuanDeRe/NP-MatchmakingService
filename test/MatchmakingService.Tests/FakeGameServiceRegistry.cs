using MatchmakingService.Models;
using MatchmakingService.Registry;

namespace MatchmakingService.Tests;

public class FakeGameServiceRegistry
    : IGameServiceRegistry
{
    private readonly GameServiceInstance?
        _availableInstance;

    private readonly Dictionary<string, MatchRoute>
        _routes = new();

    public FakeGameServiceRegistry(
        bool hasAvailableInstance = true)
    {
        if (hasAvailableInstance)
        {
            _availableInstance = new GameServiceInstance
            {
                InstanceId = "game-service-local",

                InternalUrl =
                    "http://localhost:5002",

                ActiveMatches = 0,
                MaxMatches = 20,
                Status = "Available",

                LastHeartbeatUtc =
                    DateTime.UtcNow
            };
        }
    }

    public GameServiceInstance?
        FindAvailableInstance()
    {
        return _availableInstance;
    }

    public void SaveMatchRoute(MatchRoute route)
    {
        _routes[route.MatchId] = route;
    }

    public MatchRoute? GetMatchRoute(string matchId)
    {
        return _routes.GetValueOrDefault(matchId);
    }

    public void RemoveMatchRoute(string matchId)
    {
        _routes.Remove(matchId);
    }
}
