using System.Text.Json.Serialization;
using MatchmakingService.Clients;
using MatchmakingService.Configuration;
using MatchmakingService.Services;
using MatchmakingService.Stores;

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

var app = builder.Build();

app.MapControllers();

app.MapGet("/", () => "Nightwatch Protocol - MatchmakingService running");

app.Run();