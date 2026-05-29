using System.ComponentModel.DataAnnotations;
using MoneyTracker.Domain.Entities;

namespace MoneyTracker.Api.Dtos.Categories;

public record CreateCategoryRequest(
    Guid? Id,
    [Required(ErrorMessage = "REQUIRED"), MaxLength(128, ErrorMessage = "TOO_LONG")] string Name,
    [Required(ErrorMessage = "REQUIRED")] CategoryType Type,
    Guid? ParentId,
    bool AppliesToAllWallets,
    [MaxLength(64, ErrorMessage = "TOO_LONG")] string? Icon,
    [MaxLength(16, ErrorMessage = "TOO_LONG")] string? Color,
    // Nếu AppliesToAllWallets = false, có thể đính kèm list ví muốn assign ngay.
    List<Guid>? AssignToWalletIds);

public record UpdateCategoryRequest(
    [Required(ErrorMessage = "REQUIRED"), MaxLength(128, ErrorMessage = "TOO_LONG")] string Name,
    Guid? ParentId,
    bool AppliesToAllWallets,
    [MaxLength(64, ErrorMessage = "TOO_LONG")] string? Icon,
    [MaxLength(16, ErrorMessage = "TOO_LONG")] string? Color);

public record CategoryResponse(
    Guid Id,
    string Name,
    CategoryType Type,
    Guid? ParentId,
    bool AppliesToAllWallets,
    string? Icon,
    string? Color,
    bool IsSystem,
    string? SystemKey,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record AssignCategoryToWalletsRequest(
    [Required(ErrorMessage = "REQUIRED")] Guid CategoryId,
    [Required(ErrorMessage = "REQUIRED")] List<Guid> WalletIds);

public record WalletCategoryAssignmentResponse(
    Guid Id,
    Guid WalletId,
    Guid CategoryId,
    DateTimeOffset CreatedAt);
