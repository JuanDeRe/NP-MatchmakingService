namespace MatchmakingService.Exceptions;

public class WaitingRoomUnavailableException : Exception
{
    public WaitingRoomUnavailableException(string inviteCode)
        : base($"The waiting room with code '{inviteCode}' is not accepting players.")
    {
    }
}