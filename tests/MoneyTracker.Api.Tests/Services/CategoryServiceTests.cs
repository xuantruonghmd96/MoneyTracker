using FluentAssertions;
using MoneyTracker.Api.Dtos.Categories;
using MoneyTracker.Api.Services;
using MoneyTracker.Api.Services.Exceptions;
using MoneyTracker.Api.Tests.Helpers;
using MoneyTracker.Domain.Common;
using MoneyTracker.Domain.Entities;
using MoneyTracker.Infrastructure.Persistence;

namespace MoneyTracker.Api.Tests.Services;

public class CategoryServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly CategoryService _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public CategoryServiceTests()
    {
        _db = DbContextFactory.Create();
        _sut = new CategoryService(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task SeedSystemCategoryAsync()
    {
        _db.Categories.Add(new Category
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111001"),
            UserId = null,
            Name = "Cho vay", SystemKey = "DEBT_LEND",
            Type = CategoryType.Debt, AppliesToAllWallets = true
        });
        await _db.SaveChangesAsync();
    }

    private async Task<Category> SeedUserCategoryAsync(CategoryType type = CategoryType.Expense, Guid? id = null)
    {
        var cat = new Category
        {
            Id = id ?? Guid.NewGuid(),
            UserId = _userId,
            Name = "Test Category",
            Type = type,
            AppliesToAllWallets = true
        };
        _db.Categories.Add(cat);
        await _db.SaveChangesAsync();
        return cat;
    }

    // ===== List =====

    [Fact]
    public async Task List_ReturnsUserCategoriesAndSystemCategories()
    {
        await SeedSystemCategoryAsync();
        await SeedUserCategoryAsync();

        var result = await _sut.ListAsync(_userId, default);

        result.Should().HaveCount(2);
        result.Should().Contain(c => c.IsSystem);
        result.Should().Contain(c => !c.IsSystem);
    }

    [Fact]
    public async Task List_DoesNotReturnDeletedCategories()
    {
        var cat = await SeedUserCategoryAsync();
        cat.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        var result = await _sut.ListAsync(_userId, default);

        result.Should().BeEmpty();
    }

    // ===== Get =====

    [Fact]
    public async Task Get_ExistingCategory_ReturnsDto()
    {
        var cat = await SeedUserCategoryAsync();

        var result = await _sut.GetAsync(_userId, cat.Id, default);

        result.Id.Should().Be(cat.Id);
        result.Name.Should().Be("Test Category");
    }

    [Fact]
    public async Task Get_NotFound_ThrowsNotFoundException()
    {
        var act = () => _sut.GetAsync(_userId, Guid.NewGuid(), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ===== Create =====

    [Fact]
    public async Task Create_Success_PersistsToDb()
    {
        var req = new CreateCategoryRequest(null, "Food", CategoryType.Expense, null, true, null, null, null);

        var result = await _sut.CreateAsync(_userId, req, default);

        result.Name.Should().Be("Food");
        _db.Categories.Should().ContainSingle(c => c.Name == "Food" && c.UserId == _userId);
    }

    [Fact]
    public async Task Create_DuplicateId_ThrowsConflict()
    {
        var existingId = Guid.NewGuid();
        await SeedUserCategoryAsync(id: existingId);

        var req = new CreateCategoryRequest(existingId, "Another", CategoryType.Expense, null, true, null, null, null);

        var act = () => _sut.CreateAsync(_userId, req, default);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage(ErrorCodes.IdAlreadyExists);
    }

    [Fact]
    public async Task Create_ParentNotFound_ThrowsValidation()
    {
        var req = new CreateCategoryRequest(null, "Child", CategoryType.Expense, Guid.NewGuid(), true, null, null, null);

        var act = () => _sut.CreateAsync(_userId, req, default);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage(ErrorCodes.ParentNotFound);
    }

    [Fact]
    public async Task Create_ParentTypeMismatch_ThrowsValidation()
    {
        var parent = await SeedUserCategoryAsync(CategoryType.Income);
        var req = new CreateCategoryRequest(null, "Child", CategoryType.Expense, parent.Id, true, null, null, null);

        var act = () => _sut.CreateAsync(_userId, req, default);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage(ErrorCodes.ParentTypeMismatch);
    }

    // ===== Update =====

    [Fact]
    public async Task Update_Success_UpdatesFields()
    {
        var cat = await SeedUserCategoryAsync();
        var req = new UpdateCategoryRequest("Updated Name", null, true, null, "#fff");

        var result = await _sut.UpdateAsync(_userId, cat.Id, req, default);

        result.Name.Should().Be("Updated Name");
        result.Color.Should().Be("#fff");
    }

    [Fact]
    public async Task Update_SystemCategory_ThrowsForbidden()
    {
        await SeedSystemCategoryAsync();
        var systemId = Guid.Parse("11111111-1111-1111-1111-111111111001");
        var req = new UpdateCategoryRequest("Hack", null, true, null, null);

        var act = () => _sut.UpdateAsync(_userId, systemId, req, default);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage(ErrorCodes.SystemCategoryReadOnly);
    }

    [Fact]
    public async Task Update_SelfAsParent_ThrowsValidation()
    {
        var cat = await SeedUserCategoryAsync();
        var req = new UpdateCategoryRequest("Name", cat.Id, true, null, null);

        var act = () => _sut.UpdateAsync(_userId, cat.Id, req, default);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage(ErrorCodes.CannotBeOwnParent);
    }

    // ===== Delete =====

    [Fact]
    public async Task Delete_Success_SetsDeletedAt()
    {
        var cat = await SeedUserCategoryAsync();

        await _sut.DeleteAsync(_userId, cat.Id, default);

        cat.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Delete_SystemCategory_ThrowsForbidden()
    {
        await SeedSystemCategoryAsync();
        var systemId = Guid.Parse("11111111-1111-1111-1111-111111111001");

        var act = () => _sut.DeleteAsync(_userId, systemId, default);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage(ErrorCodes.SystemCategoryReadOnly);
    }

    [Fact]
    public async Task Delete_HasActiveChildren_ThrowsValidation()
    {
        var parent = await SeedUserCategoryAsync();
        _db.Categories.Add(new Category
        {
            Id = Guid.NewGuid(), UserId = _userId,
            Name = "Child", Type = CategoryType.Expense,
            ParentId = parent.Id, AppliesToAllWallets = true
        });
        await _db.SaveChangesAsync();

        var act = () => _sut.DeleteAsync(_userId, parent.Id, default);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage(ErrorCodes.HasChildren);
    }

    [Fact]
    public async Task Delete_HasTransactions_ThrowsValidation()
    {
        var cat = await SeedUserCategoryAsync();
        var wallet = new Wallet
        {
            Id = Guid.NewGuid(), UserId = _userId, Name = "W",
            Type = WalletType.Regular, InitialBalance = 0, Currency = "VND"
        };
        _db.Wallets.Add(wallet);
        _db.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(), UserId = _userId,
            WalletId = wallet.Id, CategoryId = cat.Id,
            Amount = 100, OccurredAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync();

        var act = () => _sut.DeleteAsync(_userId, cat.Id, default);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage(ErrorCodes.HasTransactions);
    }
}
