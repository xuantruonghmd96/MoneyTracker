using System.ComponentModel.DataAnnotations;

namespace MoneyTracker.Api.Dtos.Participants;

public record CreateParticipantRequest(
    [Required(ErrorMessage = "REQUIRED"), MaxLength(128, ErrorMessage = "TOO_LONG")] string Name,
    [MaxLength(512, ErrorMessage = "TOO_LONG")] string? Note);

public record UpdateParticipantRequest(
    [Required(ErrorMessage = "REQUIRED"), MaxLength(128, ErrorMessage = "TOO_LONG")] string Name,
    [MaxLength(512, ErrorMessage = "TOO_LONG")] string? Note);

public record ParticipantResponse(
    Guid Id,
    string Name,
    string? Note,
    bool IsDefault,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
