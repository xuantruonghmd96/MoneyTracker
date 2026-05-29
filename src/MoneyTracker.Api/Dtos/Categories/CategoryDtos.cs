using System.ComponentModel.DataAnnotations;
using MoneyTracker.Domain.Entities;

namespace MoneyTracker.Api.Dtos.Categories;

public record CreateCategoryRequest(
    Guid? Id,
    [Required, MaxLength(128)] string Name,
    [Required] CategoryType Type,
    Guid? ParentId,
    bool AppliesToAllWallets,
    [MaxLength(64)] string? Icon,
    [MaxLength(16)] string? Color,
    // Nếu AppliesToAllWallets = false, có thể đính kèm list ví muốn assign ngay.
    List<Guid>? AssignToWalletIds);

public record UpdateCategoryRequest(
    [Required, MaxLength(128)] string Name,
    Guid? ParentId,
    bool AppliesToAllWallets,
    [MaxLength(64)] string? Icon,
    [MaxLength(16)] string? Color);

public record CategoryResponse(
    Guid Id,
    string Name,
    CategoryType Type,
    Guid? ParentId,
    bool AppliesToAllWallets,
    string? Icon,
    string? Color,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record AssignCategoryToWalletsRequest(
    [Required] Guid CategoryId,
    [Required] List<Guid> WalletIds);

public record WalletCategoryAssignmentResponse(
    Guid Id,
    Guid WalletId,
    Guid CategoryId,
    DateTimeOffset CreatedAt);
