using MatchmakingService.Models;

namespace MatchmakingService.Stores;

public interface IMatchmakingStore
{
    WaitingRoom GetOrCreateOpenRoom(int maxPlayers);

    WaitingRoom? GetRoomByInviteCode(string inviteCode);

    IReadOnlyCollection<WaitingRoom> GetWaitingRooms();

    bool AddPlayerToRoom(
        string roomId,
        QueuedPlayer player,
        int maxPlayers
    );
    
    bool IsPlayerWaiting(string playerId);

    void UpdateRoom(WaitingRoom room);

    void SaveAssignment(PlayerMatchAssignment assignment);

    PlayerMatchAssignment? GetAssignment(string playerId);
    
    WaitingRoom? GetWaitingRoomByPlayerId(string playerId);
    
    bool RemoveAssignment(string playerId);

    void ResetRoomForFinishedMatch(string matchId);
}