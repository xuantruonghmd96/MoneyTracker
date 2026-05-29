using FluentAssertions;
using MoneyTracker.Api.Dtos.Participants;
using MoneyTracker.Api.Services;
using MoneyTracker.Api.Services.Exceptions;
using MoneyTracker.Api.Tests.Helpers;
using MoneyTracker.Domain.Common;
using MoneyTracker.Domain.Entities;
using MoneyTracker.Infrastructure.Persistence;

namespace MoneyTracker.Api.Tests.Services;

public class ParticipantServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ParticipantService _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public ParticipantServiceTests()
    {
        _db = DbContextFactory.Create();
        _sut = new ParticipantService(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task<Participant> SeedParticipantAsync(string name, bool isDefault = false)
    {
        var p = new Participant
        {
            Id = Guid.NewGuid(), UserId = _userId,
            Name = name, IsDefault = isDefault
        };
        _db.Participants.Add(p);
        await _db.SaveChangesAsync();
        return p;
    }

    // ===== List =====

    [Fact]
    public async Task List_DefaultParticipantIsFirst()
    {
        await SeedParticipantAsync("Alice");
        await SeedParticipantAsync("Ai đó", isDefault: true);
        await SeedParticipantAsync("Bob");

        var result = await _sut.ListAsync(_userId, default);

        result[0].IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task List_DoesNotReturnDeletedParticipants()
    {
        var p = await SeedParticipantAsync("Ghost");
        p.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        var result = await _sut.ListAsync(_userId, default);

        result.Should().BeEmpty();
    }

    // ===== Get =====

    [Fact]
    public async Task Get_NotFound_ThrowsNotFoundException()
    {
        var act = () => _sut.GetAsync(_userId, Guid.NewGuid(), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ===== Create =====

    [Fact]
    public async Task Create_Success_PersistsWithIsDefaultFalse()
    {
        var result = await _sut.CreateAsync(_userId, new CreateParticipantRequest("Alice", null), default);

        result.Name.Should().Be("Alice");
        result.IsDefault.Should().BeFalse();
        _db.Participants.Should().ContainSingle(p => p.Name == "Alice");
    }

    [Fact]
    public async Task Create_NameTaken_ThrowsConflict()
    {
        await SeedParticipantAsync("Alice");

        var act = () => _sut.CreateAsync(_userId, new CreateParticipantRequest("Alice", null), default);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage(ErrorCodes.ParticipantNameTaken);
    }

    // ===== Update =====

    [Fact]
    public async Task Update_NotFound_ThrowsNotFoundException()
    {
        var act = () => _sut.UpdateAsync(_userId, Guid.NewGuid(), new UpdateParticipantRequest("Name", null), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Update_NameTakenByOther_ThrowsConflict()
    {
        await SeedParticipantAsync("Alice");
        var bob = await SeedParticipantAsync("Bob");

        var act = () => _sut.UpdateAsync(_userId, bob.Id, new UpdateParticipantRequest("Alice", null), default);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage(ErrorCodes.ParticipantNameTaken);
    }

    [Fact]
    public async Task Update_SameName_DoesNotThrow()
    {
        var alice = await SeedParticipantAsync("Alice");

        var act = () => _sut.UpdateAsync(_userId, alice.Id, new UpdateParticipantRequest("Alice", "new note"), default);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Update_Success_UpdatesNameAndNote()
    {
        var p = await SeedParticipantAsync("Old");

        var result = await _sut.UpdateAsync(_userId, p.Id, new UpdateParticipantRequest("New", "My note"), default);

        result.Name.Should().Be("New");
        result.Note.Should().Be("My note");
    }
}
