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

builder.Services.AddCors(options =>
{
    options.AddPolicy("UnityWebClient", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
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

app.UseCors("UnityWebClient");

app.UseExceptionHandler();

app.MapControllers();

app.MapGet("/", () => "Nightwatch Protocol - MatchmakingService running");
app.MapHealthChecks("/health");
app.Run();