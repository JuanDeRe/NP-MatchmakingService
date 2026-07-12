using MatchmakingService.Contracts;

namespace MatchmakingService.Services;

public interface IMatchmakingService
{
    Task<JoinQueueResponse> JoinQueueAsync(JoinQueueRequest request);

    MatchmakingStatusResponse GetStatus(string playerId);

    IReadOnlyCollection<WaitingRoomResponse> GetWaitingRooms();

    Task<ProcessWaitingRoomsResponse> ProcessWaitingRoomsAsync();
    Task<JoinQueueResponse> JoinRoomByCodeAsync(
            JoinRoomByCodeRequest request
        );
        
}