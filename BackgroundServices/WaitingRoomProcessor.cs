using MatchmakingService.Configuration;
using MatchmakingService.Services;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace MatchmakingService.BackgroundServices;

/// <summary>
/// Periodically processes waiting rooms.
/// A Redis lock guarantees that only one service instance performs
/// matchmaking processing at a time.
/// </summary>
public class WaitingRoomProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IConnectionMultiplexer _redis;
    private readonly MatchmakingOptions _options;
    private readonly RedisOptions _redisOptions;
    private readonly ILogger<WaitingRoomProcessor> _logger;

    private readonly string _instanceId =
        $"{Environment.MachineName}-" +
        $"{Guid.NewGuid():N}";

    public WaitingRoomProcessor(
        IServiceScopeFactory serviceScopeFactory,
        IConnectionMultiplexer redis,
        IOptions<MatchmakingOptions> options,
        IOptions<RedisOptions> redisOptions,
        ILogger<WaitingRoomProcessor> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _redis = redis;
        _options = options.Value;
        _redisOptions = redisOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var intervalSeconds =
            _options.ProcessingIntervalSeconds <= 0
                ? 2
                : _options.ProcessingIntervalSeconds;

        _logger.LogInformation(
            "WaitingRoomProcessor instance {InstanceId} " +
            "started with interval {IntervalSeconds}s",
            _instanceId,
            intervalSeconds
        );

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TryProcessWaitingRoomsAsync(
                    stoppingToken
                );
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Error while processing waiting rooms " +
                    "in instance {InstanceId}",
                    _instanceId
                );
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(intervalSeconds),
                    stoppingToken
                );
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation(
            "WaitingRoomProcessor instance {InstanceId} stopped",
            _instanceId
        );
    }

    private async Task TryProcessWaitingRoomsAsync(
        CancellationToken stoppingToken)
    {
        var database = _redis.GetDatabase();

        var lockKey =
            $"{_redisOptions.InstanceName}" +
            "mm:lock:waiting-room-processor";

        var lockToken =
            $"{_instanceId}-{Guid.NewGuid():N}";

        /*
         * El procesamiento suele tomar pocos segundos.
         * La expiración evita que el lock quede bloqueado
         * permanentemente si la instancia se cae.
         */
        var lockDuration = TimeSpan.FromSeconds(60);

        var lockAcquired = database.LockTake(
            lockKey,
            lockToken,
            lockDuration
        );

        if (!lockAcquired)
        {
            _logger.LogDebug(
                "Instance {InstanceId} skipped processing " +
                "because another instance owns the lock",
                _instanceId
            );

            return;
        }

        try
        {
            stoppingToken.ThrowIfCancellationRequested();

            using var scope =
                _serviceScopeFactory.CreateScope();

            var matchmakingService =
                scope.ServiceProvider
                    .GetRequiredService<
                        IMatchmakingService
                    >();

            var result =
                await matchmakingService
                    .ProcessWaitingRoomsAsync();

            if (result.MatchesCreated > 0)
            {
                _logger.LogInformation(
                    "Instance {InstanceId} processed waiting " +
                    "rooms. Matches created: {MatchesCreated}",
                    _instanceId,
                    result.MatchesCreated
                );
            }
        }
        finally
        {
            /*
             * LockRelease solo elimina el lock si todavía
             * contiene nuestro token.
             */
            var released = database.LockRelease(
                lockKey,
                lockToken
            );

            if (!released)
            {
                _logger.LogWarning(
                    "Instance {InstanceId} could not release " +
                    "the waiting room processor lock. " +
                    "It may have expired.",
                    _instanceId
                );
            }
        }
    }
}