using MoneyTracker.Api.Dtos.Participants;
using MoneyTracker.Api.Services.Exceptions;
using MoneyTracker.Domain.Common;
using MoneyTracker.Domain.Entities;
using MoneyTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MoneyTracker.Api.Services;

public class ParticipantService
{
    private readonly AppDbContext _db;

    public ParticipantService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<ParticipantResponse>> ListAsync(Guid userId, CancellationToken ct)
    {
        return await _db.Participants
            .Where(p => p.UserId == userId && p.DeletedAt == null)
            .OrderByDescending(p => p.IsDefault).ThenBy(p => p.Name)
            .Select(p => new ParticipantResponse(p.Id, p.Name, p.Note, p.IsDefault, p.CreatedAt, p.UpdatedAt))
            .ToListAsync(ct);
    }

    public async Task<ParticipantResponse> GetAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var p = await _db.Participants
            .Where(x => x.Id == id && x.UserId == userId && x.DeletedAt == null)
            .Select(x => new ParticipantResponse(x.Id, x.Name, x.Note, x.IsDefault, x.CreatedAt, x.UpdatedAt))
            .FirstOrDefaultAsync(ct);
        if (p == null) throw new NotFoundException(ErrorCodes.NotFound);
        return p;
    }

    public async Task<ParticipantResponse> CreateAsync(Guid userId, CreateParticipantRequest req, CancellationToken ct)
    {
        var nameTaken = await _db.Participants.AnyAsync(p =>
            p.UserId == userId && p.Name == req.Name && p.DeletedAt == null, ct);
        if (nameTaken) throw new ConflictException(ErrorCodes.ParticipantNameTaken);

        var participant = new Participant
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = req.Name.Trim(),
            Note = req.Note,
            IsDefault = false
        };
        _db.Participants.Add(participant);
        await _db.SaveChangesAsync(ct);

        return new ParticipantResponse(
            participant.Id, participant.Name, participant.Note,
            participant.IsDefault, participant.CreatedAt, participant.UpdatedAt);
    }

    public async Task<ParticipantResponse> UpdateAsync(Guid userId, Guid id, UpdateParticipantRequest req, CancellationToken ct)
    {
        var p = await _db.Participants.FirstOrDefaultAsync(x =>
            x.Id == id && x.UserId == userId && x.DeletedAt == null, ct);
        if (p == null) throw new NotFoundException(ErrorCodes.NotFound);

        if (p.Name != req.Name.Trim())
        {
            var nameTaken = await _db.Participants.AnyAsync(x =>
                x.Id != id && x.UserId == userId && x.Name == req.Name && x.DeletedAt == null, ct);
            if (nameTaken) throw new ConflictException(ErrorCodes.ParticipantNameTaken);
        }

        p.Name = req.Name.Trim();
        p.Note = req.Note;
        await _db.SaveChangesAsync(ct);

        return new ParticipantResponse(p.Id, p.Name, p.Note, p.IsDefault, p.CreatedAt, p.UpdatedAt);
    }
}
