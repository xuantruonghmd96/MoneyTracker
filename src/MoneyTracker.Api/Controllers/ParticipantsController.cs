using MoneyTracker.Api.Auth;
using MoneyTracker.Api.Dtos.Participants;
using MoneyTracker.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MoneyTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/participants")]
public class ParticipantsController : ControllerBase
{
    private readonly ParticipantService _participantService;
    private readonly ICurrentUser _currentUser;

    public ParticipantsController(ParticipantService participantService, ICurrentUser currentUser)
    {
        _participantService = participantService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<List<ParticipantResponse>>> List(CancellationToken ct)
        => Ok(await _participantService.ListAsync(_currentUser.Id, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ParticipantResponse>> Get(Guid id, CancellationToken ct)
        => Ok(await _participantService.GetAsync(_currentUser.Id, id, ct));

    [HttpPost]
    public async Task<ActionResult<ParticipantResponse>> Create([FromBody] CreateParticipantRequest req, CancellationToken ct)
    {
        var result = await _participantService.CreateAsync(_currentUser.Id, req, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ParticipantResponse>> Update(Guid id, [FromBody] UpdateParticipantRequest req, CancellationToken ct)
        => Ok(await _participantService.UpdateAsync(_currentUser.Id, id, req, ct));
    // KHÔNG có DELETE endpoint
}
