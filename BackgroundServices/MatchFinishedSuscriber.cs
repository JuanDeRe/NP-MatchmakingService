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
    private static readonly RedisChannel Channel =
        RedisChannel.Literal(
            "nightwatch:events:match-finished"
        );

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
            Channel,
            HandleMessage
        );

        _logger.LogInformation(
            "Subscribed to Redis channel {Channel}",
            Channel.ToString()
        );

        try
        {
            await Task.Delay(
                Timeout.InfiniteTimeSpan,
                stoppingToken
            );
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Cierre normal de la aplicación.
        }
        finally
        {
            await subscriber.UnsubscribeAsync(
                Channel,
                HandleMessage
            );
        }
    }

    private void HandleMessage(
        RedisChannel channel,
        RedisValue value)
    {
        try
        {
            var matchFinishedEvent =
                JsonSerializer.Deserialize<
                    MatchFinishedEvent
                >(value.ToString());

            if (matchFinishedEvent is null)
            {
                _logger.LogWarning(
                    "Received an invalid MatchFinished event"
                );

                return;
            }

            foreach (var playerId in
                     matchFinishedEvent.PlayerIds)
            {
                _store.RemoveAssignment(playerId);
            }

            _store.ResetRoomForFinishedMatch(
                matchFinishedEvent.MatchId
            );

            _logger.LogInformation(
                "Released {PlayerCount} players from " +
                "finished match {MatchId}",
                matchFinishedEvent.PlayerIds.Count,
                matchFinishedEvent.MatchId
            );
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to process MatchFinished event " +
                "from channel {Channel}",
                channel.ToString()
            );
        }
    }
}