using System.Net.Http.Json;
using MatchmakingService.Contracts;

namespace MatchmakingService.Clients;

/// <summary>
/// HTTP client used to request match creation from
/// a specific GameService instance.
/// </summary>
public class GameServiceClient : IGameServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GameServiceClient> _logger;

    public GameServiceClient(
        HttpClient httpClient,
        ILogger<GameServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<CreateMatchResponse?> CreateMatchAsync(
        CreateMatchRequest request,
        string gameServiceBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(gameServiceBaseUrl))
        {
            _logger.LogWarning(
                "Cannot create match because GameService URL is empty."
            );

            return null;
        }

        try
        {
            var baseUrl = gameServiceBaseUrl.TrimEnd('/');

            var url =
                $"{baseUrl}/game/matches";

            _logger.LogInformation(
                "Requesting match creation from {Url}",
                url
            );

            var response = await _httpClient.PostAsJsonAsync(
                url,
                request
            );

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content
                    .ReadAsStringAsync();

                _logger.LogWarning(
                    "GameService at {Url} rejected match creation. " +
                    "Status: {StatusCode}. Body: {ResponseBody}",
                    url,
                    response.StatusCode,
                    responseBody
                );

                return null;
            }

            return await response.Content
                .ReadFromJsonAsync<CreateMatchResponse>();
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(
                exception,
                "Could not connect to GameService at {BaseUrl}",
                gameServiceBaseUrl
            );

            return null;
        }
        catch (TaskCanceledException exception)
        {
            _logger.LogError(
                exception,
                "Request to GameService at {BaseUrl} timed out",
                gameServiceBaseUrl
            );

            return null;
        }
    }
}