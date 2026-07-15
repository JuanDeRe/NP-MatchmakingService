using System.Text.Json;
using MatchmakingService.Configuration;
using MatchmakingService.Models;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace MatchmakingService.Registry;

public class RedisGameServiceRegistry
    : IGameServiceRegistry
{
    private static readonly JsonSerializerOptions JsonOptions =
        new()
        {
            PropertyNamingPolicy =
                JsonNamingPolicy.CamelCase,

            PropertyNameCaseInsensitive = true
        };

    private readonly IConnectionMultiplexer _redis;
    private readonly RedisOptions _options;

    public RedisGameServiceRegistry(
        IConnectionMultiplexer redis,
        IOptions<RedisOptions> options)
    {
        _redis = redis;
        _options = options.Value;
    }

    public GameServiceInstance? FindAvailableInstance()
    {
        var database = _redis.GetDatabase();

        var instanceIds = database.SetMembers(
            InstancesSetKey
        );

        var availableInstances =
            new List<GameServiceInstance>();

        foreach (var instanceIdValue in instanceIds)
        {
            var instanceId =
                instanceIdValue.ToString();

            var json = database.StringGet(
                InstanceKey(instanceId)
            );

            if (!json.HasValue)
            {
                database.SetRemove(
                    InstancesSetKey,
                    instanceId
                );

                continue;
            }

            try
            {
                var instance =
                    JsonSerializer.Deserialize<
                        GameServiceInstance
                    >(
                        json.ToString(),
                        JsonOptions
                    );

                if (instance is null ||
                    !instance.HasCapacity)
                {
                    continue;
                }

                if (DateTime.UtcNow -
                    instance.LastHeartbeatUtc >
                    TimeSpan.FromSeconds(20))
                {
                    continue;
                }

                availableInstances.Add(instance);
            }
            catch (JsonException)
            {
                database.SetRemove(
                    InstancesSetKey,
                    instanceId
                );
            }
        }

        return availableInstances
            .OrderBy(instance =>
                instance.ActiveMatches
            )
            .ThenBy(instance =>
                instance.InstanceId
            )
            .FirstOrDefault();
    }

    public void SaveMatchRoute(MatchRoute route)
    {
        var database = _redis.GetDatabase();

        var json = JsonSerializer.Serialize(
            route,
            JsonOptions
        );

        database.StringSet(
            MatchRouteKey(route.MatchId),
            json,
            TimeSpan.FromHours(24)
        );
    }

    public MatchRoute? GetMatchRoute(string matchId)
    {
        var database = _redis.GetDatabase();

        var json = database.StringGet(
            MatchRouteKey(matchId)
        );

        if (!json.HasValue)
        {
            return null;
        }

        return JsonSerializer.Deserialize<MatchRoute>(
            json.ToString(),
            JsonOptions
        );
    }

    public void RemoveMatchRoute(string matchId)
    {
        _redis.GetDatabase().KeyDelete(
            MatchRouteKey(matchId)
        );
    }

    private string InstancesSetKey =>
        $"{_options.InstanceName}" +
        "game-services:instances";

    private string InstanceKey(string instanceId) =>
        $"{_options.InstanceName}" +
        $"game-services:instance:{instanceId}";

    private string MatchRouteKey(string matchId) =>
        $"{_options.InstanceName}" +
        $"routing:match:{matchId}";
}