using System.Text.Json;
using MatchmakingService.Contracts;
using MatchmakingService.Stores;
using StackExchange.Redis;

namespace MatchmakingService.BackgroundServices;

/// <summary>
/// Polls the Redis match event stream and processes durable events.
/// </summary>
public class MatchEventStreamProcessor
    : BackgroundService
{
    private const string StreamKey =
        "nightwatch:streams:match-events";

    private const string CursorKey =
        "nightwatch:consumers:matchmaking:last-event-id";

    private readonly IConnectionMultiplexer _redis;
    private readonly IMatchmakingStore _store;

    private readonly ILogger<MatchEventStreamProcessor>
        _logger;

    public MatchEventStreamProcessor(
        IConnectionMultiplexer redis,
        IMatchmakingStore store,
        ILogger<MatchEventStreamProcessor> logger)
    {
        _redis = redis;
        _store = store;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var database = _redis.GetDatabase();

        var lastEventId = GetInitialCursor(database);

        _logger.LogInformation(
            "MatchEventStreamProcessor started at event {EventId}",
            lastEventId
        );

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var entries = database.StreamRead(
                    StreamKey,
                    lastEventId,
                    count: 20
                );

                foreach (var entry in entries)
                {
                    ProcessEntry(entry);

                    lastEventId = entry.Id.ToString();

                    database.StringSet(
                        CursorKey,
                        lastEventId
                    );
                }
            }
            catch (RedisConnectionException exception)
            {
                _logger.LogError(
                    exception,
                    "Redis connection failed while reading match events"
                );
            }
            catch (RedisTimeoutException exception)
            {
                _logger.LogError(
                    exception,
                    "Redis timed out while reading match events"
                );
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Unexpected error while processing match events"
                );
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromMilliseconds(500),
                    stoppingToken
                );
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private static string GetInitialCursor(
        IDatabase database)
    {
        var storedCursor = database.StringGet(
            CursorKey
        );

        if (storedCursor.HasValue)
        {
            return storedCursor.ToString();
        }

        // Procesa también eventos que ya existían.
        return "0-0";
    }

    private void ProcessEntry(StreamEntry entry)
    {
        var eventType = GetValue(
            entry,
            "eventType"
        );

        if (eventType != "MatchFinished")
        {
            _logger.LogWarning(
                "Unsupported match event type {EventType}",
                eventType
            );

            return;
        }

        var payload = GetValue(
            entry,
            "payload"
        );

        if (string.IsNullOrWhiteSpace(payload))
        {
            _logger.LogWarning(
                "Event {EventId} has no payload",
                entry.Id
            );

            return;
        }

        var matchFinishedEvent =
            JsonSerializer.Deserialize<
                MatchFinishedEvent
            >(payload);

        if (matchFinishedEvent is null)
        {
            _logger.LogWarning(
                "Event {EventId} contains an invalid payload",
                entry.Id
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
            "finished match {MatchId}. Event {EventId}",
            matchFinishedEvent.PlayerIds.Count,
            matchFinishedEvent.MatchId,
            entry.Id
        );
    }

    private static string? GetValue(
        StreamEntry entry,
        string name)
    {
        foreach (var pair in entry.Values)
        {
            if (pair.Name == name)
            {
                return pair.Value.ToString();
            }
        }

        return null;
    }
}