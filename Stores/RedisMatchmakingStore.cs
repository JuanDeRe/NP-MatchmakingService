using System.Text.Json;
using System.Text.Json.Serialization;
using MatchmakingService.Configuration;
using MatchmakingService.Models;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace MatchmakingService.Stores;

/// <summary>
/// Stores waiting rooms and player assignments in Redis so that
/// multiple MatchmakingService instances share the same state.
/// </summary>
public class RedisMatchmakingStore : IMatchmakingStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    private readonly IConnectionMultiplexer _redis;
    private readonly RedisOptions _options;

    public RedisMatchmakingStore(
        IConnectionMultiplexer redis,
        IOptions<RedisOptions> options)
    {
        _redis = redis;
        _options = options.Value;
    }

    public WaitingRoom GetOrCreateOpenRoom(int maxPlayers)
    {
        var database = _redis.GetDatabase();

        return ExecuteWithLock(
            database,
            "rooms",
            TimeSpan.FromSeconds(10),
            () =>
            {
                var existingRoom = GetWaitingRoomsInternal(
                        database
                    )
                    .OrderBy(room => room.CreatedAtUtc)
                    .FirstOrDefault(room =>
                        room.Status !=
                            WaitingRoomStatus.MatchCreated &&
                        room.Players.Count < maxPlayers
                    );

                if (existingRoom is not null)
                {
                    return existingRoom;
                }

                var newRoom = new WaitingRoom
                {
                    RoomId =
                        $"room-{Guid.NewGuid():N}",

                    InviteCode =
                        GenerateUniqueInviteCode(database),

                    Status =
                        WaitingRoomStatus.WaitingForPlayers,

                    CreatedAtUtc = DateTime.UtcNow,

                    Players = []
                };

                SaveRoomInternal(
                    database,
                    newRoom
                );

                database.SetAdd(
                    RoomsSetKey,
                    newRoom.RoomId
                );

                database.StringSet(
                    InviteKey(newRoom.InviteCode),
                    newRoom.RoomId
                );

                return newRoom;
            }
        );
    }

    public WaitingRoom? GetRoomByInviteCode(
        string inviteCode)
    {
        if (string.IsNullOrWhiteSpace(inviteCode))
        {
            return null;
        }

        var database = _redis.GetDatabase();

        var normalizedCode = inviteCode
            .Trim()
            .ToUpperInvariant();

        var roomId = database.StringGet(
            InviteKey(normalizedCode)
        );

        if (!roomId.HasValue)
        {
            return null;
        }

        var room = GetRoomInternal(
            database,
            roomId.ToString()
        );

        if (room is null)
        {
            database.KeyDelete(
                InviteKey(normalizedCode)
            );

            return null;
        }

        if (room.Status ==
            WaitingRoomStatus.MatchCreated)
        {
            return null;
        }

        return room;
    }

    public IReadOnlyCollection<WaitingRoom>
        GetWaitingRooms()
    {
        var database = _redis.GetDatabase();

        return GetWaitingRoomsInternal(
            database
        );
    }

    public bool AddPlayerToRoom(
        string roomId,
        QueuedPlayer player,
        int maxPlayers)
    {
        if (string.IsNullOrWhiteSpace(roomId) ||
            string.IsNullOrWhiteSpace(player.PlayerId))
        {
            return false;
        }

        var database = _redis.GetDatabase();

        return ExecuteWithLock(
            database,
            "rooms",
            TimeSpan.FromSeconds(10),
            () =>
            {
                if (database.KeyExists(
                        AssignmentKey(player.PlayerId)))
                {
                    return false;
                }

                if (database.KeyExists(
                        PlayerRoomKey(player.PlayerId)))
                {
                    return false;
                }

                var room = GetRoomInternal(
                    database,
                    roomId
                );

                if (room is null)
                {
                    return false;
                }

                if (room.Status ==
                    WaitingRoomStatus.MatchCreated)
                {
                    return false;
                }

                if (room.Players.Count >= maxPlayers)
                {
                    return false;
                }

                if (room.Players.Any(existingPlayer =>
                        existingPlayer.PlayerId ==
                        player.PlayerId))
                {
                    return false;
                }

                room.Players.Add(player);

                SaveRoomInternal(
                    database,
                    room
                );

                database.StringSet(
                    PlayerRoomKey(player.PlayerId),
                    room.RoomId
                );

                return true;
            }
        );
    }

    public bool IsPlayerWaiting(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return false;
        }

        var database = _redis.GetDatabase();

        var roomId = database.StringGet(
            PlayerRoomKey(playerId)
        );

        if (!roomId.HasValue)
        {
            return false;
        }

        var room = GetRoomInternal(
            database,
            roomId.ToString()
        );

        if (room is null ||
            room.Status ==
                WaitingRoomStatus.MatchCreated)
        {
            database.KeyDelete(
                PlayerRoomKey(playerId)
            );

            return false;
        }

        return room.Players.Any(player =>
            player.PlayerId == playerId
        );
    }

    public void UpdateRoom(WaitingRoom room)
    {
        if (room is null ||
            string.IsNullOrWhiteSpace(room.RoomId))
        {
            return;
        }

        var database = _redis.GetDatabase();

        ExecuteWithLock(
            database,
            $"room:{room.RoomId}",
            TimeSpan.FromSeconds(10),
            () =>
            {
                if (!database.SetContains(
                        RoomsSetKey,
                        room.RoomId))
                {
                    return false;
                }

                SaveRoomInternal(
                    database,
                    room
                );

                database.StringSet(
                    InviteKey(room.InviteCode),
                    room.RoomId
                );

                foreach (var player in room.Players)
                {
                    if (room.Status ==
                        WaitingRoomStatus.MatchCreated)
                    {
                        database.KeyDelete(
                            PlayerRoomKey(player.PlayerId)
                        );
                    }
                    else
                    {
                        database.StringSet(
                            PlayerRoomKey(player.PlayerId),
                            room.RoomId
                        );
                    }
                }

                return true;
            }
        );
    }

    public void SaveAssignment(
        PlayerMatchAssignment assignment)
    {
        if (assignment is null ||
            string.IsNullOrWhiteSpace(
                assignment.PlayerId))
        {
            return;
        }

        var database = _redis.GetDatabase();

        var json = JsonSerializer.Serialize(
            assignment,
            JsonOptions
        );

        database.StringSet(
            AssignmentKey(assignment.PlayerId),
            json,
            TimeSpan.FromHours(24)
        );

        database.KeyDelete(
            PlayerRoomKey(assignment.PlayerId)
        );
    }

    public PlayerMatchAssignment? GetAssignment(
        string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return null;
        }

        var database = _redis.GetDatabase();

        var json = database.StringGet(
            AssignmentKey(playerId)
        );

        if (!json.HasValue)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<
                PlayerMatchAssignment
            >(
                json.ToString(),
                JsonOptions
            );
        }
        catch (JsonException)
        {
            database.KeyDelete(
                AssignmentKey(playerId)
            );

            return null;
        }
    }

    public WaitingRoom? GetWaitingRoomByPlayerId(
        string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return null;
        }

        var database = _redis.GetDatabase();

        var roomId = database.StringGet(
            PlayerRoomKey(playerId)
        );

        if (!roomId.HasValue)
        {
            return null;
        }

        var room = GetRoomInternal(
            database,
            roomId.ToString()
        );

        if (room is null)
        {
            database.KeyDelete(
                PlayerRoomKey(playerId)
            );

            return null;
        }

        if (room.Status ==
            WaitingRoomStatus.MatchCreated)
        {
            database.KeyDelete(
                PlayerRoomKey(playerId)
            );

            return null;
        }

        if (!room.Players.Any(player =>
                player.PlayerId == playerId))
        {
            database.KeyDelete(
                PlayerRoomKey(playerId)
            );

            return null;
        }

        return room;
    }

    public bool RemoveAssignment(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return false;
        }

        var database = _redis.GetDatabase();

        return database.KeyDelete(
            AssignmentKey(playerId)
        );
    }

    public void ResetRoomForFinishedMatch(
        string matchId)
    {
        if (string.IsNullOrWhiteSpace(matchId))
        {
            return;
        }

        var database = _redis.GetDatabase();

        ExecuteWithLock(
            database,
            "rooms",
            TimeSpan.FromSeconds(10),
            () =>
            {
                var room = GetWaitingRoomsInternal(
                        database
                    )
                    .FirstOrDefault(waitingRoom =>
                        waitingRoom.CurrentMatchId ==
                        matchId
                    );

                if (room is null)
                {
                    return false;
                }

                /*
                 * Eliminamos la sala terminada en vez de
                 * reutilizarla con la lista vieja de jugadores.
                 */
                foreach (var player in room.Players)
                {
                    database.KeyDelete(
                        PlayerRoomKey(player.PlayerId)
                    );
                }

                database.KeyDelete(
                    InviteKey(room.InviteCode)
                );

                database.KeyDelete(
                    RoomKey(room.RoomId)
                );

                database.SetRemove(
                    RoomsSetKey,
                    room.RoomId
                );

                return true;
            }
        );
    }

    private IReadOnlyCollection<WaitingRoom>
        GetWaitingRoomsInternal(
            IDatabase database)
    {
        var roomIds = database.SetMembers(
            RoomsSetKey
        );

        var rooms = new List<WaitingRoom>();

        foreach (var roomIdValue in roomIds)
        {
            var roomId = roomIdValue.ToString();

            var room = GetRoomInternal(
                database,
                roomId
            );

            if (room is null)
            {
                database.SetRemove(
                    RoomsSetKey,
                    roomId
                );

                continue;
            }

            rooms.Add(room);
        }

        return rooms;
    }

    private WaitingRoom? GetRoomInternal(
        IDatabase database,
        string roomId)
    {
        var json = database.StringGet(
            RoomKey(roomId)
        );

        if (!json.HasValue)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<
                WaitingRoom
            >(
                json.ToString(),
                JsonOptions
            );
        }
        catch (JsonException)
        {
            database.KeyDelete(
                RoomKey(roomId)
            );

            database.SetRemove(
                RoomsSetKey,
                roomId
            );

            return null;
        }
    }

    private void SaveRoomInternal(
        IDatabase database,
        WaitingRoom room)
    {
        var json = JsonSerializer.Serialize(
            room,
            JsonOptions
        );

        database.StringSet(
            RoomKey(room.RoomId),
            json,
            TimeSpan.FromHours(24)
        );

        database.SetAdd(
            RoomsSetKey,
            room.RoomId
        );
    }

    private string GenerateUniqueInviteCode(
        IDatabase database)
    {
        for (var attempt = 1;
             attempt <= 20;
             attempt++)
        {
            var inviteCode = Guid.NewGuid()
                .ToString("N")[..6]
                .ToUpperInvariant();

            if (!database.KeyExists(
                    InviteKey(inviteCode)))
            {
                return inviteCode;
            }
        }

        throw new InvalidOperationException(
            "Unable to generate a unique invite code."
        );
    }

    private T ExecuteWithLock<T>(
        IDatabase database,
        string lockName,
        TimeSpan duration,
        Func<T> operation)
    {
        var lockKey = LockKey(lockName);
        var lockToken =
            Guid.NewGuid().ToString("N");

        const int maximumAttempts = 20;

        for (var attempt = 1;
             attempt <= maximumAttempts;
             attempt++)
        {
            var acquired = database.LockTake(
                lockKey,
                lockToken,
                duration
            );

            if (acquired)
            {
                try
                {
                    return operation();
                }
                finally
                {
                    database.LockRelease(
                        lockKey,
                        lockToken
                    );
                }
            }

            Thread.Sleep(50);
        }

        throw new TimeoutException(
            $"Could not acquire Redis lock '{lockName}'."
        );
    }

    private string RoomsSetKey =>
        $"{Prefix}rooms";

    private string RoomKey(string roomId) =>
        $"{Prefix}room:{roomId}";

    private string InviteKey(string inviteCode) =>
        $"{Prefix}invite:{inviteCode}";

    private string PlayerRoomKey(string playerId) =>
        $"{Prefix}player-room:{playerId}";

    private string AssignmentKey(string playerId) =>
        $"{Prefix}assignment:{playerId}";

    private string LockKey(string lockName) =>
        $"{Prefix}lock:{lockName}";

    private string Prefix =>
        $"{_options.InstanceName}mm:";

    private static string GenerateInviteCode()
    {
        return Guid.NewGuid()
            .ToString("N")[..6]
            .ToUpperInvariant();
    }
}