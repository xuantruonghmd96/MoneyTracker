using MoneyTracker.Api.Auth;
using MoneyTracker.Api.Common;
using MoneyTracker.Api.Dtos.Participants;
using MoneyTracker.Domain.Common;
using MoneyTracker.Domain.Entities;
using MoneyTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MoneyTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/participants")]
public class ParticipantsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public ParticipantsController(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<List<ParticipantResponse>>> List()
    {
        var list = await _db.Participants
            .Where(p => p.UserId == _currentUser.Id && p.DeletedAt == null)
            .OrderByDescending(p => p.IsDefault).ThenBy(p => p.Name)
            .Select(p => new ParticipantResponse(p.Id, p.Name, p.Note, p.IsDefault, p.CreatedAt, p.UpdatedAt))
            .ToListAsync();
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ParticipantResponse>> Get(Guid id)
    {
        var p = await _db.Participants
            .Where(x => x.Id == id && x.UserId == _currentUser.Id && x.DeletedAt == null)
            .Select(x => new ParticipantResponse(x.Id, x.Name, x.Note, x.IsDefault, x.CreatedAt, x.UpdatedAt))
            .FirstOrDefaultAsync();
        return p == null ? NotFound(new ApiError(ErrorCodes.NotFound)) : Ok(p);
    }

    [HttpPost]
    public async Task<ActionResult<ParticipantResponse>> Create([FromBody] CreateParticipantRequest req)
    {
        var nameTaken = await _db.Participants.AnyAsync(p =>
            p.UserId == _currentUser.Id && p.Name == req.Name && p.DeletedAt == null);
        if (nameTaken)
            return Conflict(new ApiError(ErrorCodes.ParticipantNameTaken));

        var participant = new Participant
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.Id,
            Name = req.Name.Trim(),
            Note = req.Note,
            IsDefault = false
        };
        _db.Participants.Add(participant);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = participant.Id },
            new ParticipantResponse(participant.Id, participant.Name, participant.Note,
                participant.IsDefault, participant.CreatedAt, participant.UpdatedAt));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ParticipantResponse>> Update(Guid id, [FromBody] UpdateParticipantRequest req)
    {
        var p = await _db.Participants.FirstOrDefaultAsync(x =>
            x.Id == id && x.UserId == _currentUser.Id && x.DeletedAt == null);
        if (p == null) return NotFound(new ApiError(ErrorCodes.NotFound));

        if (p.Name != req.Name.Trim())
        {
            var nameTaken = await _db.Participants.AnyAsync(x =>
                x.Id != id && x.UserId == _currentUser.Id && x.Name == req.Name && x.DeletedAt == null);
            if (nameTaken)
                return Conflict(new ApiError(ErrorCodes.ParticipantNameTaken));
        }

        p.Name = req.Name.Trim();
        p.Note = req.Note;
        // IsDefault không sửa được qua API
        await _db.SaveChangesAsync();

        return Ok(new ParticipantResponse(p.Id, p.Name, p.Note, p.IsDefault, p.CreatedAt, p.UpdatedAt));
    }
    // KHÔNG có DELETE endpoint
}
