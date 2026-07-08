using MatchmakingService.Clients;
using MatchmakingService.Configuration;
using MatchmakingService.Contracts;
using MatchmakingService.Models;
using MatchmakingService.Stores;
using Microsoft.Extensions.Options;

namespace MatchmakingService.Services;

/// <summary>
/// Coordinates player queueing and match creation.
/// </summary>
public class MatchmakingService : IMatchmakingService
{
    private readonly IMatchmakingStore _store;
    private readonly IGameServiceClient _gameServiceClient;
    private readonly MatchmakingOptions _options;

    public MatchmakingService(
        IMatchmakingStore store,
        IGameServiceClient gameServiceClient,
        IOptions<MatchmakingOptions> options)
    {
        _store = store;
        _gameServiceClient = gameServiceClient;
        _options = options.Value;
    }

    public async Task<JoinQueueResponse> JoinQueueAsync(JoinQueueRequest request)
    {
        var existingAssignment = _store.GetAssignment(request.PlayerId);

        if (existingAssignment is not null)
        {
            return BuildAssignedResponse(existingAssignment);
        }

        if (_store.IsPlayerQueued(request.PlayerId))
        {
            return new JoinQueueResponse
            {
                Status = QueueStatus.Queued.ToString(),
                Message = "Player is already queued"
            };
        }

        _store.Enqueue(new QueuedPlayer
        {
            PlayerId = request.PlayerId
        });

        var queuedPlayers = _store.GetQueuedPlayers();

        if (queuedPlayers.Count < _options.MinPlayersPerMatch)
        {
            return new JoinQueueResponse
            {
                Status = QueueStatus.Queued.ToString(),
                Message = "Player added to queue"
            };
        }

        var playersForMatch = _store.TakePlayersForMatch(_options.MinPlayersPerMatch);

        var createMatchRequest = BuildCreateMatchRequest(playersForMatch);

        var createMatchResponse = await _gameServiceClient.CreateMatchAsync(createMatchRequest);

        if (createMatchResponse is null)
        {
            // If match creation fails, players return to the queue.
            foreach (var player in playersForMatch)
            {
                _store.Enqueue(player);
            }

            return new JoinQueueResponse
            {
                Status = QueueStatus.Queued.ToString(),
                Message = "GameService is not available. Player remains queued."
            };
        }

        var assignments = AssignPlayers(
            playersForMatch,
            createMatchRequest,
            createMatchResponse
        );

        foreach (var assignment in assignments)
        {
            _store.SaveAssignment(assignment);
        }

        var currentPlayerAssignment = assignments.First(a => a.PlayerId == request.PlayerId);

        return BuildAssignedResponse(currentPlayerAssignment);
    }

    public MatchmakingStatusResponse GetStatus(string playerId)
    {
        var assignment = _store.GetAssignment(playerId);

        if (assignment is not null)
        {
            return new MatchmakingStatusResponse
            {
                PlayerId = playerId,
                Status = QueueStatus.Assigned.ToString(),
                MatchId = assignment.MatchId,
                GameServiceId = assignment.GameServiceId,
                Role = assignment.Role.ToString()
            };
        }

        if (_store.IsPlayerQueued(playerId))
        {
            return new MatchmakingStatusResponse
            {
                PlayerId = playerId,
                Status = QueueStatus.Queued.ToString()
            };
        }

        return new MatchmakingStatusResponse
        {
            PlayerId = playerId,
            Status = "NotQueued"
        };
    }

    public IReadOnlyCollection<QueuedPlayerResponse> GetQueue()
    {
        return _store.GetQueuedPlayers()
            .Select(player => new QueuedPlayerResponse
            {
                PlayerId = player.PlayerId,
                JoinedAtUtc = player.JoinedAtUtc
            })
            .ToList();
    }

    private static CreateMatchRequest BuildCreateMatchRequest(List<QueuedPlayer> players)
    {
        var createMatchPlayers = players
            .Select((player, index) => new CreateMatchPlayerDto
            {
                PlayerId = player.PlayerId,
                Role = index == 0 ? PlayerRole.Guard : PlayerRole.Animatronic
            })
            .ToList();

        return new CreateMatchRequest
        {
            Players = createMatchPlayers
        };
    }

    private static List<PlayerMatchAssignment> AssignPlayers(
        List<QueuedPlayer> queuedPlayers,
        CreateMatchRequest createMatchRequest,
        CreateMatchResponse createMatchResponse)
    {
        return queuedPlayers
            .Select(player =>
            {
                var matchPlayer = createMatchRequest.Players
                    .First(p => p.PlayerId == player.PlayerId);

                return new PlayerMatchAssignment
                {
                    PlayerId = player.PlayerId,
                    MatchId = createMatchResponse.MatchId,
                    GameServiceId = createMatchResponse.GameServiceId,
                    Role = matchPlayer.Role
                };
            })
            .ToList();
    }

    private static JoinQueueResponse BuildAssignedResponse(PlayerMatchAssignment assignment)
    {
        return new JoinQueueResponse
        {
            Status = QueueStatus.Assigned.ToString(),
            Message = "Player assigned to match",
            MatchId = assignment.MatchId,
            GameServiceId = assignment.GameServiceId,
            Role = assignment.Role.ToString()
        };
    }
}