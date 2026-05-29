using MoneyTracker.Api.Auth;
using MoneyTracker.Api.Dtos.Sync;
using MoneyTracker.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MoneyTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/sync")]
public class SyncController : ControllerBase
{
    private readonly SyncService _syncService;
    private readonly ICurrentUser _currentUser;

    public SyncController(SyncService syncService, ICurrentUser currentUser)
    {
        _syncService = syncService;
        _currentUser = currentUser;
    }

    [HttpPost("push")]
    public async Task<ActionResult<SyncPushResponse>> Push([FromBody] SyncPushRequest req, CancellationToken ct)
    {
        var result = await _syncService.PushAsync(_currentUser.Id, req, ct);
        return Ok(result);
    }

    [HttpGet("pull")]
    public async Task<ActionResult<SyncPullResponse>> Pull([FromQuery] DateTimeOffset? since, CancellationToken ct)
    {
        var result = await _syncService.PullAsync(_currentUser.Id, since, ct);
        return Ok(result);
    }
}
