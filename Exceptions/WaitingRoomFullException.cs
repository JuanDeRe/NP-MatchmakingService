namespace MatchmakingService.Exceptions;

public class WaitingRoomFullException : Exception
{
    public WaitingRoomFullException(string inviteCode)
        : base($"The waiting room with code '{inviteCode}' is full.")
    {
    }
}