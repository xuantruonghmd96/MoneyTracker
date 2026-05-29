using MoneyTracker.Api.Dtos.Auth;
using MoneyTracker.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MoneyTracker.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest req, CancellationToken ct)
    {
        var result = await _authService.RegisterAsync(req, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return Ok(result);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(req, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return Ok(result);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshRequest req, CancellationToken ct)
    {
        var result = await _authService.RefreshAsync(req.RefreshToken, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return Ok(result);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest req, CancellationToken ct)
    {
        await _authService.LogoutAsync(req.RefreshToken, ct);
        return NoContent();
    }
}
