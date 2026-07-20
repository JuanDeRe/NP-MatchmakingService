using System.Text.Json.Serialization;
using MatchmakingService.BackgroundServices;
using MatchmakingService.Clients;
using MatchmakingService.Configuration;
using MatchmakingService.ExceptionHandlers;
using MatchmakingService.Services;
using MatchmakingService.Stores;
using StackExchange.Redis;
using MatchmakingService.Registry;

var builder = WebApplication.CreateBuilder(args);

Console.WriteLine(
    $"Runtime actual: " +
    $"{System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}"
);

builder.Services.Configure<GameServiceOptions>(
    builder.Configuration.GetSection("GameService")
);

builder.Services.Configure<MatchmakingOptions>(
    builder.Configuration.GetSection("Matchmaking")
);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddSingleton<
    IMatchmakingStore,
    RedisMatchmakingStore
>();

builder.Services.AddSingleton<
    IGameServiceRegistry,
    RedisGameServiceRegistry
>();

builder.Services.AddHttpClient<IGameServiceClient, GameServiceClient>();

builder.Services.AddScoped<IMatchmakingService, MatchmakingService.Services.MatchmakingService>();
builder.Services.AddHostedService<
    MatchEventStreamProcessor
>();
builder.Services.AddHostedService<WaitingRoomProcessor>();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var allowedOrigins =
    builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>()
    ?? Array.Empty<string>();

const string unityClientCorsPolicy =
    "UnityClientCorsPolicy";

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        unityClientCorsPolicy,
        policy =>
        {
            policy
                .SetIsOriginAllowed(origin =>
                {
                    if (string.IsNullOrWhiteSpace(origin))
                    {
                        return false;
                    }

                    if (!Uri.TryCreate(
                            origin,
                            UriKind.Absolute,
                            out var uri))
                    {
                        return false;
                    }

                    var isLocalDevelopment =
                        uri.Host.Equals(
                            "localhost",
                            StringComparison.OrdinalIgnoreCase
                        ) ||
                        uri.Host.Equals(
                            "127.0.0.1",
                            StringComparison.OrdinalIgnoreCase
                        );

                    var normalizedOrigin =
                        origin.TrimEnd('/');

                    var isConfiguredOrigin =
                        allowedOrigins.Any(
                            allowedOrigin =>
                                string.Equals(
                                    allowedOrigin.TrimEnd('/'),
                                    normalizedOrigin,
                                    StringComparison.OrdinalIgnoreCase
                                )
                        );

                    return isLocalDevelopment ||
                           isConfiguredOrigin;
                })
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    );
});
builder.Services.Configure<RedisOptions>(
    builder.Configuration.GetSection("Redis")
);

builder.Services.AddSingleton<IConnectionMultiplexer>(
    serviceProvider =>
    {
        var redisOptions = serviceProvider
            .GetRequiredService<
                Microsoft.Extensions.Options.IOptions<
                    RedisOptions
                >
            >()
            .Value;

        var configuration =
            ConfigurationOptions.Parse(
                redisOptions.ConnectionString
            );

        configuration.AbortOnConnectFail = false;
        configuration.ConnectRetry = 3;
        configuration.ConnectTimeout = 5000;

        return ConnectionMultiplexer.Connect(
            configuration
        );
    }
);

builder.Services.AddHealthChecks();
var app = builder.Build();

app.UseRouting();

app.UseCors(unityClientCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();