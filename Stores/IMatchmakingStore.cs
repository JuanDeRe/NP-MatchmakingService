using MatchmakingService.Models;

namespace MatchmakingService.Stores;

public interface IMatchmakingStore
{
    void Enqueue(QueuedPlayer player);

    IReadOnlyCollection<QueuedPlayer> GetQueuedPlayers();

    List<QueuedPlayer> TakePlayersForMatch(int amount);

    bool IsPlayerQueued(string playerId);

    void SaveAssignment(PlayerMatchAssignment assignment);

    PlayerMatchAssignment? GetAssignment(string playerId);
}