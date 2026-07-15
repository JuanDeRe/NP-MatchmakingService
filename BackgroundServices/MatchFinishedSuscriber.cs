using System.Text.Json;
using MatchmakingService.Contracts;
using MatchmakingService.Stores;
using StackExchange.Redis;

namespace MatchmakingService.BackgroundServices;

/// <summary>
/// Releases player assignments when a match finishes.
/// </summary>
public class MatchFinishedSubscriber
    : BackgroundService
{
    private const string MatchFinishedChannel =
        "nightwatch:events:match-finished";

    private readonly IConnectionMultiplexer _redis;
    private readonly IMatchmakingStore _store;
    private readonly ILogger<MatchFinishedSubscriber>
        _logger;

    public MatchFinishedSubscriber(
        IConnectionMultiplexer redis,
        IMatchmakingStore store,
        ILogger<MatchFinishedSubscriber> logger)
    {
        _redis = redis;
        _store = store;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var subscriber = _redis.GetSubscriber();

        await subscriber.SubscribeAsync(
            RedisChannel.Literal(
                MatchFinishedChannel
            ),
            async (_, value) =>
            {
                try
                {
                    var matchFinishedEvent =
                        JsonSerializer.Deserialize<
                            MatchFinishedEvent
                        >(value.ToString());

                    if (matchFinishedEvent is null)
                    {
                        return;
                    }

                    foreach (var playerId in
                             matchFinishedEvent.PlayerIds)
                    {
                        _store.RemoveAssignment(
                            playerId
                        );
                    }

                    _store.ResetRoomForFinishedMatch(
                        matchFinishedEvent.MatchId
                    );

                    _logger.LogInformation(
                        "Released players for finished " +
                        "match {MatchId}",
                        matchFinishedEvent.MatchId
                    );

                    await Task.CompletedTask;
                }
                catch (Exception exception)
                {
                    _logger.LogError(
                        exception,
                        "Failed to process MatchFinished event"
                    );
                }
            }
        );

        try
        {
            await Task.Delay(
                Timeout.Infinite,
                stoppingToken
            );
        }
        catch (OperationCanceledException)
        {
            // Normal application shutdown.
        }

        await subscriber.UnsubscribeAsync(
            RedisChannel.Literal(
                MatchFinishedChannel
            )
        );
    }
}