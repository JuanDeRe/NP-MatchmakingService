namespace MatchmakingService.Exceptions;

public class WaitingRoomNotFoundException : Exception
{
    public WaitingRoomNotFoundException(string inviteCode)
        : base($"No waiting room was found with invite code '{inviteCode}'.")
    {
    }
}