using MatchmakingService.Contracts;
using MatchmakingService.Services;
using Microsoft.AspNetCore.Mvc;
using MatchmakingService.Exceptions;
namespace MatchmakingService.Controllers;

/// <summary>
/// Exposes matchmaking endpoints for waiting rooms and match assignment.
/// </summary>
[ApiController]
[Route("matchmaking")]
public class MatchmakingController : ControllerBase
{
    private readonly IMatchmakingService _matchmakingService;

    public MatchmakingController(IMatchmakingService matchmakingService)
    {
        _matchmakingService = matchmakingService;
    }

    [HttpPost("join")]
    public async Task<ActionResult<JoinQueueResponse>> JoinQueue(
        [FromBody] JoinQueueRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PlayerId))
        {
            return BadRequest(new
            {
                message = "PlayerId is required"
            });
        }

        var response = await _matchmakingService.JoinQueueAsync(request);

        return Ok(response);
    }

    [HttpGet("status/{playerId}")]
    public ActionResult<MatchmakingStatusResponse> GetStatus(string playerId)
    {
        var response = _matchmakingService.GetStatus(playerId);

        return Ok(response);
    }

    [HttpGet("rooms")]
    public ActionResult<IReadOnlyCollection<WaitingRoomResponse>> GetWaitingRooms()
    {
        var rooms = _matchmakingService.GetWaitingRooms();

        return Ok(rooms);
    }

    [HttpPost("process-waiting-rooms")]
    public async Task<ActionResult<ProcessWaitingRoomsResponse>> ProcessWaitingRooms()
    {
        var response = await _matchmakingService.ProcessWaitingRoomsAsync();

        return Ok(response);
    }
    
    [HttpPost("rooms/join-by-code")]
    public async Task<ActionResult<JoinQueueResponse>> JoinRoomByCode(
        [FromBody] JoinRoomByCodeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PlayerId))
        {
            return BadRequest(new
            {
                message = "PlayerId is required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.InviteCode))
        {
            return BadRequest(new
            {
                message = "InviteCode is required."
            });
        }

        var response = await _matchmakingService
            .JoinRoomByCodeAsync(request);

        return Ok(response);
    }
}