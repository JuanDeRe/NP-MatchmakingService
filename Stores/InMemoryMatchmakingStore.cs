using System.Collections.Concurrent;
using MatchmakingService.Models;

namespace MatchmakingService.Stores;

/// <summary>
/// Temporary in-memory storage for waiting rooms and match assignments.
/// </summary>
public class InMemoryMatchmakingStore : IMatchmakingStore
{
    private readonly object _syncRoot = new();

    private readonly List<WaitingRoom> _waitingRooms = [];

    private readonly ConcurrentDictionary<string, PlayerMatchAssignment> _assignments = new();

    public WaitingRoom GetOrCreateOpenRoom(int maxPlayers)
    {
        lock (_syncRoot)
        {
            var room = _waitingRooms.FirstOrDefault(room =>
                room.Status != WaitingRoomStatus.MatchCreated &&
                room.Players.Count < maxPlayers
            );

            if (room is not null)
            {
                return room;
            }

            var newRoom = new WaitingRoom
            {
                RoomId = $"room-{Guid.NewGuid():N}",
                InviteCode = GenerateInviteCode(),
                Status = WaitingRoomStatus.WaitingForPlayers,
                CreatedAtUtc = DateTime.UtcNow
            };

            _waitingRooms.Add(newRoom);

            return newRoom;
        }
    }

    public WaitingRoom? GetRoomByInviteCode(string inviteCode)
    {
        lock (_syncRoot)
        {
            return _waitingRooms.FirstOrDefault(room =>
                room.InviteCode.Equals(inviteCode, StringComparison.OrdinalIgnoreCase) &&
                room.Status != WaitingRoomStatus.MatchCreated
            );
        }
    }

    public IReadOnlyCollection<WaitingRoom> GetWaitingRooms()
    {
        lock (_syncRoot)
        {
            return _waitingRooms
                .Select(CloneRoom)
                .ToList();
        }
    }

    public bool AddPlayerToRoom(
        string roomId,
        QueuedPlayer player,
        int maxPlayers)
    {
        lock (_syncRoot)
        {
            if (IsPlayerWaitingInternal(player.PlayerId))
            {
                return false;
            }

            if (_assignments.ContainsKey(player.PlayerId))
            {
                return false;
            }

            var room = _waitingRooms.FirstOrDefault(
                waitingRoom => waitingRoom.RoomId == roomId
            );

            if (room is null)
            {
                return false;
            }

            if (room.Status == WaitingRoomStatus.MatchCreated)
            {
                return false;
            }

            if (room.Players.Count >= maxPlayers)
            {
                return false;
            }

            room.Players.Add(player);
            return true;
        }
    }

    public bool IsPlayerWaiting(string playerId)
    {
        lock (_syncRoot)
        {
            return IsPlayerWaitingInternal(playerId);
        }
    }

    public void UpdateRoom(WaitingRoom room)
    {
        lock (_syncRoot)
        {
            var index = _waitingRooms.FindIndex(existingRoom => existingRoom.RoomId == room.RoomId);

            if (index >= 0)
            {
                _waitingRooms[index] = room;
            }
        }
    }

    public void SaveAssignment(PlayerMatchAssignment assignment)
    {
        _assignments[assignment.PlayerId] = assignment;
    }

    public PlayerMatchAssignment? GetAssignment(string playerId)
    {
        return _assignments.GetValueOrDefault(playerId);
    }

    private bool IsPlayerWaitingInternal(string playerId)
    {
        return _waitingRooms.Any(room =>
            room.Status != WaitingRoomStatus.MatchCreated &&
            room.Players.Any(player => player.PlayerId == playerId)
        );
    }

    private static WaitingRoom CloneRoom(WaitingRoom room)
    {
        return new WaitingRoom
        {
            RoomId = room.RoomId,
            InviteCode = room.InviteCode,
            Status = room.Status,
            CreatedAtUtc = room.CreatedAtUtc,
            CountdownStartedAtUtc = room.CountdownStartedAtUtc,
            Players = room.Players
                .Select(player => new QueuedPlayer
                {
                    PlayerId = player.PlayerId,
                    JoinedAtUtc = player.JoinedAtUtc
                })
                .ToList()
        };
    }

    private static string GenerateInviteCode()
    {
        return Guid.NewGuid()
            .ToString("N")[..6]
            .ToUpperInvariant();
    }
    public WaitingRoom? GetWaitingRoomByPlayerId(string playerId)
    {
        lock (_syncRoot)
        {
            var room = _waitingRooms.FirstOrDefault(room =>
                room.Status != WaitingRoomStatus.MatchCreated &&
                room.Players.Any(player => player.PlayerId == playerId)
            );

            return room is null ? null : CloneRoom(room);
        }
    }
    public bool RemoveAssignment(string playerId)
    {
        return _assignments.TryRemove(
            playerId,
            out _
        );
    }

    public void ResetRoomForFinishedMatch(
        string matchId)
    {
        lock (_syncRoot)
        {
            var room = _waitingRooms.FirstOrDefault(
                room => room.CurrentMatchId == matchId
            );

            if (room is null)
            {
                return;
            }

            room.CurrentMatchId = null;
            room.Status =
                WaitingRoomStatus.WaitingForPlayers;

            room.CountdownStartedAtUtc = null;
        }
    }
}