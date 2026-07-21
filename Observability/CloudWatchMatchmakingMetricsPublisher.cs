using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using MatchmakingService.Models;
using MatchmakingService.Stores;
using Microsoft.Extensions.Options;

namespace MatchmakingService.Observability;

public sealed class CloudWatchMatchmakingMetricsPublisher
    : BackgroundService
{
    private const string ServiceName =
        "matchmaking-service";

    private readonly IMatchmakingStore _store;

    private readonly IMatchmakingMetrics _metrics;

    private readonly IAmazonCloudWatch _cloudWatch;

    private readonly MatchmakingMetricsOptions
        _options;

    private readonly ILogger<
        CloudWatchMatchmakingMetricsPublisher
    > _logger;

    public CloudWatchMatchmakingMetricsPublisher(
        IMatchmakingStore store,
        IMatchmakingMetrics metrics,
        IAmazonCloudWatch cloudWatch,
        IOptions<MatchmakingMetricsOptions> options,
        ILogger<
            CloudWatchMatchmakingMetricsPublisher
        > logger)
    {
        _store = store;
        _metrics = metrics;
        _cloudWatch = cloudWatch;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation(
                "CloudWatch matchmaking metrics " +
                "are disabled."
            );

            return;
        }

        var intervalSeconds =
            _options.PublishIntervalSeconds <= 0
                ? 60
                : _options
                    .PublishIntervalSeconds;

        _logger.LogInformation(
            "CloudWatch MatchmakingService metrics " +
            "publisher started. Namespace={Namespace}, " +
            "Environment={Environment}, " +
            "IntervalSeconds={IntervalSeconds}",
            _options.Namespace,
            _options.Environment,
            intervalSeconds
        );

        await PublishAsync(stoppingToken);

        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(intervalSeconds)
        );

        try
        {
            while (await timer.WaitForNextTickAsync(
                       stoppingToken))
            {
                await PublishAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "CloudWatch MatchmakingService " +
                "metrics publisher stopped."
            );
        }
    }

    private async Task PublishAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var queuedPlayers = _store
                .GetWaitingRooms()
                .Where(room =>
                    room.Status !=
                    WaitingRoomStatus.MatchCreated
                )
                .Sum(room =>
                    room.Players.Count
                );

            var snapshot =
                _metrics.TakeSnapshot();

            var metricData =
                new List<MetricDatum>
                {
                    new()
                    {
                        MetricName =
                            "QueuedPlayers",

                        Value = queuedPlayers,

                        Unit =
                            StandardUnit.Count,

                        Timestamp =
                            DateTime.UtcNow,

                        Dimensions =
                            CreateDimensions()
                    },
                    new()
                    {
                        MetricName =
                            "MatchCreationFailures",

                        Value =
                            snapshot
                                .MatchCreationFailures,

                        Unit =
                            StandardUnit.Count,

                        Timestamp =
                            DateTime.UtcNow,

                        Dimensions =
                            CreateDimensions()
                    }
                };

            if (snapshot.MatchCreationAttempts > 0)
            {
                metricData.Add(
                    new MetricDatum
                    {
                        MetricName =
                            "MatchCreationLatency",

                        Unit =
                            StandardUnit.Milliseconds,

                        Timestamp =
                            DateTime.UtcNow,

                        Dimensions =
                            CreateDimensions(),

                        StatisticValues =
                            new StatisticSet
                            {
                                SampleCount =
                                    snapshot
                                        .MatchCreationAttempts,

                                Sum =
                                    snapshot
                                        .MatchCreationLatencySum,

                                Minimum =
                                    snapshot
                                        .MatchCreationLatencyMinimum,

                                Maximum =
                                    snapshot
                                        .MatchCreationLatencyMaximum
                            }
                    }
                );
            }

            await _cloudWatch.PutMetricDataAsync(
                new PutMetricDataRequest
                {
                    Namespace =
                        _options.Namespace,

                    MetricData =
                        metricData
                },
                cancellationToken
            );

            _logger.LogInformation(
                "Published MatchmakingService metrics. " +
                "QueuedPlayers={QueuedPlayers}, " +
                "MatchCreationFailures={Failures}, " +
                "MatchCreationAttempts={Attempts}",
                queuedPlayers,
                snapshot.MatchCreationFailures,
                snapshot.MatchCreationAttempts
            );
        }
        catch (OperationCanceledException)
            when (cancellationToken
                .IsCancellationRequested)
        {
            // Normal application shutdown.
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to publish MatchmakingService " +
                "metrics to CloudWatch."
            );
        }
    }

    private List<Dimension> CreateDimensions()
    {
        return
        [
            new Dimension
            {
                Name = "Environment",
                Value = _options.Environment
            },
            new Dimension
            {
                Name = "Service",
                Value = ServiceName
            }
        ];
    }
}