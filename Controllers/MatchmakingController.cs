using MatchmakingService.Contracts;
using MatchmakingService.Services;
using Microsoft.AspNetCore.Mvc;

namespace MatchmakingService.Controllers;

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
}