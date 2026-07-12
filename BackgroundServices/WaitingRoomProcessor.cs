using MatchmakingService.Configuration;
using MatchmakingService.Services;
using Microsoft.Extensions.Options;

namespace MatchmakingService.BackgroundServices;

/// <summary>
/// Periodically processes waiting rooms whose countdown has expired.
/// </summary>
public class WaitingRoomProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly MatchmakingOptions _options;
    private readonly ILogger<WaitingRoomProcessor> _logger;

    public WaitingRoomProcessor(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<MatchmakingOptions> options,
        ILogger<WaitingRoomProcessor> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = _options.ProcessingIntervalSeconds <= 0
            ? 2
            : _options.ProcessingIntervalSeconds;

        _logger.LogInformation(
            "WaitingRoomProcessor started with interval {IntervalSeconds}s",
            intervalSeconds
        );

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();

                var matchmakingService = scope.ServiceProvider
                    .GetRequiredService<IMatchmakingService>();

                var result = await matchmakingService.ProcessWaitingRoomsAsync();

                if (result.MatchesCreated > 0)
                {
                    _logger.LogInformation(
                        "Processed waiting rooms. Matches created: {MatchesCreated}",
                        result.MatchesCreated
                    );
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Error while processing waiting rooms"
                );
            }

            await Task.Delay(
                TimeSpan.FromSeconds(intervalSeconds),
                stoppingToken
            );
        }
    }
}