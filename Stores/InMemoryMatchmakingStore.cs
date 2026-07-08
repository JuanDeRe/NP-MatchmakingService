using System.Collections.Concurrent;
using MatchmakingService.Models;

namespace MatchmakingService.Stores;

public class InMemoryMatchmakingStore : IMatchmakingStore
{
    private readonly object _syncRoot = new();

    private readonly Queue<QueuedPlayer> _queue = new();

    private readonly ConcurrentDictionary<string, PlayerMatchAssignment> _assignments = new();

    public void Enqueue(QueuedPlayer player)
    {
        lock (_syncRoot)
        {
            if (_queue.Any(p => p.PlayerId == player.PlayerId))
            {
                return;
            }

            if (_assignments.ContainsKey(player.PlayerId))
            {
                return;
            }

            _queue.Enqueue(player);
        }
    }

    public IReadOnlyCollection<QueuedPlayer> GetQueuedPlayers()
    {
        lock (_syncRoot)
        {
            return _queue.ToList();
        }
    }

    public List<QueuedPlayer> TakePlayersForMatch(int amount)
    {
        lock (_syncRoot)
        {
            var players = new List<QueuedPlayer>();

            while (_queue.Count > 0 && players.Count < amount)
            {
                players.Add(_queue.Dequeue());
            }

            return players;
        }
    }

    public bool IsPlayerQueued(string playerId)
    {
        lock (_syncRoot)
        {
            return _queue.Any(player => player.PlayerId == playerId);
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
}