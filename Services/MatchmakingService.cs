using MatchmakingService.Clients;
using MatchmakingService.Configuration;
using MatchmakingService.Contracts;
using MatchmakingService.Models;
using MatchmakingService.Stores;
using Microsoft.Extensions.Options;
using MatchmakingService.Exceptions;

namespace MatchmakingService.Services;

/// <summary>
/// Coordinates waiting rooms and match creation.
/// </summary>
public class MatchmakingService : IMatchmakingService
{
    private static readonly AnimatronicType[] AvailableAnimatronics =
        [
            AnimatronicType.Foxy,
            AnimatronicType.Chica,
            AnimatronicType.Bonnie,
            AnimatronicType.Freddy
        ];
    
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

        if (_store.IsPlayerWaiting(request.PlayerId))
        {
            return new JoinQueueResponse
            {
                Status = QueueStatus.Queued.ToString(),
                Message = "Player is already in a waiting room"
            };
        }

        var room = _store.GetOrCreateOpenRoom(_options.MaxPlayersPerMatch);

        var playerAdded = _store.AddPlayerToRoom(
            room.RoomId,
            new QueuedPlayer
            {
                PlayerId = request.PlayerId
            },
            _options.MaxPlayersPerMatch
        );

        if (!playerAdded)
        {
            return new JoinQueueResponse
            {
                Status = "Rejected",
                Message = "Player could not be added to the waiting room."
            };
        }

        room = _store.GetWaitingRooms().First(r => r.RoomId == room.RoomId);

        StartCountdownIfReady(room);

        if (room.Players.Count >= _options.MaxPlayersPerMatch)
        {
            var response = await CreateMatchFromRoomAsync(room, request.PlayerId);

            if (response is not null)
            {
                return response;
            }
        }

        return new JoinQueueResponse
        {
            Status = QueueStatus.Queued.ToString(),
            Message = "Player added to waiting room",
            MatchId = null,
            GameServiceId = null,
            Role = null
        };
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
                Role = assignment.Role.ToString(),
                AnimatronicType = assignment.AnimatronicType.ToString()
            };
        }

        var room = _store.GetWaitingRoomByPlayerId(playerId);

        if (room is not null)
        {
            DateTime? countdownEndsAtUtc = null;

            if (room.CountdownStartedAtUtc is not null)
            {
                countdownEndsAtUtc = room.CountdownStartedAtUtc.Value
                    .AddSeconds(_options.CountdownSeconds);
            }

            return new MatchmakingStatusResponse
            {
                PlayerId = playerId,
                Status = QueueStatus.Queued.ToString(),
                InviteCode = room.InviteCode,
                CurrentPlayers = room.Players.Count,
                MaxPlayers = _options.MaxPlayersPerMatch,
                CountdownEndsAtUtc = countdownEndsAtUtc
            };
        }

        return new MatchmakingStatusResponse
        {
            PlayerId = playerId,
            Status = "NotQueued"
        };
    }

    public IReadOnlyCollection<WaitingRoomResponse> GetWaitingRooms()
    {
        return _store.GetWaitingRooms()
            .Where(room => room.Status != WaitingRoomStatus.MatchCreated)
            .Select(room => new WaitingRoomResponse
            {
                RoomId = room.RoomId,
                InviteCode = room.InviteCode,
                Status = room.Status.ToString(),
                CurrentPlayers = room.Players.Count,
                MaxPlayers = _options.MaxPlayersPerMatch,
                CreatedAtUtc = room.CreatedAtUtc,
                CountdownStartedAtUtc = room.CountdownStartedAtUtc
            })
            .ToList();
    }

    public async Task<ProcessWaitingRoomsResponse> ProcessWaitingRoomsAsync()
    {
        var response = new ProcessWaitingRoomsResponse();

        var rooms = _store.GetWaitingRooms()
            .Where(room =>
                room.Status == WaitingRoomStatus.CountdownStarted &&
                room.CountdownStartedAtUtc is not null &&
                room.Players.Count >= _options.MinPlayersPerMatch
            )
            .ToList();

        foreach (var room in rooms)
        {
            var elapsedSeconds = (DateTime.UtcNow - room.CountdownStartedAtUtc!.Value).TotalSeconds;

            if (elapsedSeconds < _options.CountdownSeconds)
            {
                continue;
            }

            var createdResponse = await CreateMatchFromRoomAsync(room, null);

            if (createdResponse?.MatchId is not null)
            {
                response.MatchesCreated++;
                response.CreatedMatchIds.Add(createdResponse.MatchId);
            }
        }

        return response;
    }
    public async Task<JoinQueueResponse> JoinRoomByCodeAsync(
        JoinRoomByCodeRequest request)
    {
        var existingAssignment = _store.GetAssignment(request.PlayerId);

        if (existingAssignment is not null)
        {
            return BuildAssignedResponse(existingAssignment);
        }

        if (_store.IsPlayerWaiting(request.PlayerId))
        {
            return new JoinQueueResponse
            {
                Status = QueueStatus.Queued.ToString(),
                Message = "Player is already in a waiting room."
            };
        }

        var normalizedCode = request.InviteCode
            .Trim()
            .ToUpperInvariant();

        var room = _store.GetRoomByInviteCode(normalizedCode);

        if (room is null)
        {
            throw new WaitingRoomNotFoundException(normalizedCode);
        }

        if (room.Status == WaitingRoomStatus.MatchCreated)
        {
            throw new WaitingRoomUnavailableException(normalizedCode);
        }

        if (room.Players.Count >= _options.MaxPlayersPerMatch)
        {
            throw new WaitingRoomFullException(normalizedCode);
        }

        var playerAdded = _store.AddPlayerToRoom(
            room.RoomId,
            new QueuedPlayer
            {
                PlayerId = request.PlayerId
            },
            _options.MaxPlayersPerMatch
        );

        if (!playerAdded)
        {
            throw new WaitingRoomUnavailableException(normalizedCode);
        }

        room = _store.GetWaitingRooms()
            .First(waitingRoom => waitingRoom.RoomId == room.RoomId);

        StartCountdownIfReady(room);

        if (room.Players.Count >= _options.MaxPlayersPerMatch)
        {
            var assignedResponse = await CreateMatchFromRoomAsync(
                room,
                request.PlayerId
            );

            if (assignedResponse is not null)
            {
                return assignedResponse;
            }
        }

        return new JoinQueueResponse
        {
            Status = QueueStatus.Queued.ToString(),
            Message = $"Player joined waiting room {normalizedCode}."
        };
    }

    private async Task<JoinQueueResponse?> CreateMatchFromRoomAsync(
        WaitingRoom room,
        string? requestingPlayerId)
    {
        var createMatchRequest = BuildCreateMatchRequest(room.Players);

        var createMatchResponse = await _gameServiceClient.CreateMatchAsync(createMatchRequest);

        if (createMatchResponse is null)
        {
            return null;
        }

        var assignments = AssignPlayers(
            room.Players,
            createMatchRequest,
            createMatchResponse
        );

        foreach (var assignment in assignments)
        {
            _store.SaveAssignment(assignment);
        }

        room.Status = WaitingRoomStatus.MatchCreated;
        _store.UpdateRoom(room);

        if (requestingPlayerId is null)
        {
            return new JoinQueueResponse
            {
                Status = QueueStatus.Assigned.ToString(),
                Message = "Match created",
                MatchId = createMatchResponse.MatchId,
                GameServiceId = createMatchResponse.GameServiceId
            };
        }

        var currentPlayerAssignment = assignments
            .FirstOrDefault(assignment => assignment.PlayerId == requestingPlayerId);

        if (currentPlayerAssignment is null)
        {
            return new JoinQueueResponse
            {
                Status = QueueStatus.Queued.ToString(),
                Message = "Player added to waiting room. Another group was assigned first."
            };
        }

        return BuildAssignedResponse(currentPlayerAssignment);
    }

    private static CreateMatchRequest BuildCreateMatchRequest(
        List<QueuedPlayer> players)
    {
        if (players.Count > AvailableAnimatronics.Length + 1)
        {
            throw new InvalidOperationException(
                "A match cannot contain more than one guard and four animatronics."
            );
        }

        var createMatchPlayers = players
            .Select((player, index) => new CreateMatchPlayerDto
            {
                PlayerId = player.PlayerId,

                Role = index == 0
                    ? PlayerRole.Guard
                    : PlayerRole.Animatronic,

                AnimatronicType = index == 0
                    ? AnimatronicType.None
                    : AvailableAnimatronics[index - 1]
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
                    Role = matchPlayer.Role,
                    AnimatronicType = matchPlayer.AnimatronicType
                };
            })
            .ToList();
    }

    private static JoinQueueResponse BuildAssignedResponse(
        PlayerMatchAssignment assignment)
    {
        return new JoinQueueResponse
        {
            Status = QueueStatus.Assigned.ToString(),
            Message = "Player assigned to match",
            MatchId = assignment.MatchId,
            GameServiceId = assignment.GameServiceId,
            Role = assignment.Role.ToString(),
            AnimatronicType = assignment.AnimatronicType.ToString()
        };
    }
    private void StartCountdownIfReady(WaitingRoom room)
    {
        if (room.Players.Count < _options.MinPlayersPerMatch)
        {
            return;
        }

        if (room.Status != WaitingRoomStatus.WaitingForPlayers)
        {
            return;
        }

        room.Status = WaitingRoomStatus.CountdownStarted;
        room.CountdownStartedAtUtc = DateTime.UtcNow;

        _store.UpdateRoom(room);
    }
}