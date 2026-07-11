using System.Net.Http.Json;
using MatchmakingService.Configuration;
using MatchmakingService.Contracts;
using Microsoft.Extensions.Options;

namespace MatchmakingService.Clients;

/// <summary>
/// HTTP client used to request match creation from GameService.
/// </summary>
public class GameServiceClient : IGameServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly GameServiceOptions _options;

    public GameServiceClient(
        HttpClient httpClient,
        IOptions<GameServiceOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<CreateMatchResponse?> CreateMatchAsync(CreateMatchRequest request)
    {
        try
        {
            var url = $"{_options.BaseUrl}/game/matches";

            var response = await _httpClient.PostAsJsonAsync(url, request);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<CreateMatchResponse>();
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }
}