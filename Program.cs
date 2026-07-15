using System.Text.Json.Serialization;
using MatchmakingService.BackgroundServices;
using MatchmakingService.Clients;
using MatchmakingService.Configuration;
using MatchmakingService.ExceptionHandlers;
using MatchmakingService.Services;
using MatchmakingService.Stores;
using MatchmakingService.Configuration;
using StackExchange.Redis;
var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddSingleton<IMatchmakingStore, InMemoryMatchmakingStore>();

builder.Services.AddHttpClient<IGameServiceClient, GameServiceClient>();

builder.Services.AddScoped<IMatchmakingService, MatchmakingService.Services.MatchmakingService>();

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
        var configuration =
            serviceProvider
                .GetRequiredService<
                    Microsoft.Extensions.Options.IOptions<
                        RedisOptions
                    >
                >()
                .Value;

        return ConnectionMultiplexer.Connect(
            configuration.ConnectionString
        );
    }
);
builder.Services.AddHostedService<MatchFinishedSubscriber>();

var app = builder.Build();

app.UseCors("UnityWebClient");

app.UseExceptionHandler();

app.MapControllers();

app.MapGet("/", () => "Nightwatch Protocol - MatchmakingService running");

app.Run();