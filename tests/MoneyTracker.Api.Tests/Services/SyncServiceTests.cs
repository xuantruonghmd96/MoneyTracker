using System.Text.Json;
using FluentAssertions;
using MoneyTracker.Api.Dtos.Sync;
using MoneyTracker.Api.Services;
using MoneyTracker.Api.Services.Exceptions;
using MoneyTracker.Api.Tests.Helpers;
using MoneyTracker.Domain.Common;
using MoneyTracker.Domain.Entities;
using MoneyTracker.Infrastructure.Persistence;

namespace MoneyTracker.Api.Tests.Services;

public class SyncServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly SyncService _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public SyncServiceTests()
    {
        _db = DbContextFactory.Create();
        _sut = new SyncService(_db);
    }

    public void Dispose() => _db.Dispose();

    private static SyncPushRequest MakeRequest(Guid batchId, List<SyncChangeItem>? participants = null)
        => new(batchId, null, new SyncPushChanges(null, null, null, participants, null));

    private static SyncChangeItem UpsertParticipant(Guid id, string name, DateTimeOffset? updatedAt = null)
    {
        var data = JsonDocument.Parse($@"{{""name"":""{name}"",""note"":null}}").RootElement;
        return new SyncChangeItem(id, "upsert", updatedAt ?? DateTimeOffset.UtcNow, data);
    }

    // ===== Push — Idempotency =====

    [Fact]
    public async Task Push_SameBatchIdTwice_ReturnsCachedResponse()
    {
        var batchId = Guid.NewGuid();
        var req = MakeRequest(batchId);

        var first = await _sut.PushAsync(_userId, req, default);
        var second = await _sut.PushAsync(_userId, req, default);

        _db.SyncBatches.Count().Should().Be(1);
        second.ServerNow.Should().Be(first.ServerNow);
    }

    // ===== Push — Apply =====

    [Fact]
    public async Task Push_NewParticipantUpsert_CreatesParticipant()
    {
        var participantId = Guid.NewGuid();
        var req = MakeRequest(Guid.NewGuid(), new List<SyncChangeItem>
        {
            UpsertParticipant(participantId, "Alice")
        });

        var result = await _sut.PushAsync(_userId, req, default);

        _db.Participants.Should().ContainSingle(p => p.Name == "Alice" && p.UserId == _userId);
        result.Results.Participants!.Should().ContainSingle(r => r.Id == participantId && r.Status == "applied");
    }

    // ===== Push — Validation error / no batch saved =====

    [Fact]
    public async Task Push_NameTakenError_ThrowsAndDoesNotSaveBatch()
    {
        // Seed an existing participant so name will conflict
        _db.Participants.Add(new Participant
        {
            Id = Guid.NewGuid(), UserId = _userId, Name = "Alice", IsDefault = false
        });
        await _db.SaveChangesAsync();

        var req = MakeRequest(Guid.NewGuid(), new List<SyncChangeItem>
        {
            UpsertParticipant(Guid.NewGuid(), "Alice") // duplicate name
        });

        var act = () => _sut.PushAsync(_userId, req, default);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage(ErrorCodes.SyncBatchRejected);

        _db.SyncBatches.Should().BeEmpty();
    }

    // ===== Push — LWW =====

    [Fact]
    public async Task Push_LWW_OlderUpdate_SkipsParticipant()
    {
        var participantId = Guid.NewGuid();
        var serverUpdatedAt = DateTimeOffset.UtcNow;

        _db.Participants.Add(new Participant
        {
            Id = participantId, UserId = _userId,
            Name = "Original", IsDefault = false,
            UpdatedAt = serverUpdatedAt
        });
        await _db.SaveChangesAsync();

        // Client sends update older than server version
        var olderThanServer = serverUpdatedAt.AddSeconds(-10);
        var req = MakeRequest(Guid.NewGuid(), new List<SyncChangeItem>
        {
            UpsertParticipant(participantId, "New Name", olderThanServer)
        });

        var result = await _sut.PushAsync(_userId, req, default);

        var p = _db.Participants.Single(x => x.Id == participantId);
        p.Name.Should().Be("Original"); // unchanged
        result.Results.Participants!.Should().ContainSingle(r => r.Status == "skipped");
    }

    // ===== Pull =====

    [Fact]
    public async Task Pull_NoSince_ReturnsAllUserEntities()
    {
        _db.Participants.Add(new Participant
        {
            Id = Guid.NewGuid(), UserId = _userId, Name = "P1", IsDefault = false,
            UpdatedAt = DateTimeOffset.UtcNow.AddHours(-1)
        });
        _db.Participants.Add(new Participant
        {
            Id = Guid.NewGuid(), UserId = Guid.NewGuid(), // different user
            Name = "Other", IsDefault = false
        });
        await _db.SaveChangesAsync();

        var result = await _sut.PullAsync(_userId, null, default);

        result.Participants.Should().HaveCount(1);
        result.Participants[0].Name.Should().Be("P1");
    }

    [Fact]
    public async Task Pull_WithSince_OnlyReturnsNewerEntities()
    {
        var since = DateTimeOffset.UtcNow.AddHours(-1);

        _db.Participants.Add(new Participant
        {
            Id = Guid.NewGuid(), UserId = _userId, Name = "Old",
            IsDefault = false, UpdatedAt = since.AddMinutes(-10) // older
        });
        _db.Participants.Add(new Participant
        {
            Id = Guid.NewGuid(), UserId = _userId, Name = "New",
            IsDefault = false, UpdatedAt = since.AddMinutes(10) // newer
        });
        await _db.SaveChangesAsync();

        var result = await _sut.PullAsync(_userId, since, default);

        result.Participants.Should().ContainSingle(p => p.Name == "New");
    }
}
