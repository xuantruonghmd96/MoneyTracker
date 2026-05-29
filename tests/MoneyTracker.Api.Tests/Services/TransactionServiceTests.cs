using FluentAssertions;
using MoneyTracker.Api.Dtos.Transactions;
using MoneyTracker.Api.Services;
using MoneyTracker.Api.Services.Exceptions;
using MoneyTracker.Api.Tests.Helpers;
using MoneyTracker.Domain.Common;
using MoneyTracker.Domain.Entities;
using MoneyTracker.Infrastructure.Persistence;

namespace MoneyTracker.Api.Tests.Services;

public class TransactionServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly TransactionService _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public TransactionServiceTests()
    {
        _db = DbContextFactory.Create();
        _sut = new TransactionService(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task<(Wallet wallet, Category category)> SeedWalletAndCategoryAsync(
        CategoryType catType = CategoryType.Expense,
        bool appliesToAll = true)
    {
        var wallet = new Wallet
        {
            Id = Guid.NewGuid(), UserId = _userId,
            Name = "Test Wallet", Type = WalletType.Regular,
            InitialBalance = 0, Currency = "VND"
        };
        var category = new Category
        {
            Id = Guid.NewGuid(), UserId = _userId,
            Name = "Test Cat", Type = catType,
            AppliesToAllWallets = appliesToAll
        };
        _db.Wallets.Add(wallet);
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();
        return (wallet, category);
    }

    private async Task<Participant> SeedDefaultParticipantAsync()
    {
        var p = new Participant
        {
            Id = Guid.NewGuid(), UserId = _userId,
            Name = "Ai đó", IsDefault = true
        };
        _db.Participants.Add(p);
        await _db.SaveChangesAsync();
        return p;
    }

    private async Task<Category> SeedDebtCategoryAsync()
    {
        var cat = new Category
        {
            Id = Guid.NewGuid(), UserId = null,
            Name = "Cho vay", SystemKey = "DEBT_LEND",
            Type = CategoryType.Debt, AppliesToAllWallets = true
        };
        _db.Categories.Add(cat);
        await _db.SaveChangesAsync();
        return cat;
    }

    // ===== Create =====

    [Fact]
    public async Task Create_Success_PersistsToDb()
    {
        var (wallet, category) = await SeedWalletAndCategoryAsync();
        var req = new CreateTransactionRequest(null, 100m, DateTimeOffset.UtcNow, wallet.Id, category.Id, null, "Note");

        var result = await _sut.CreateAsync(_userId, req, default);

        result.Amount.Should().Be(100m);
        _db.Transactions.Should().ContainSingle(t => t.UserId == _userId);
    }

    [Fact]
    public async Task Create_DuplicateId_ThrowsConflict()
    {
        var (wallet, category) = await SeedWalletAndCategoryAsync();
        var id = Guid.NewGuid();
        _db.Transactions.Add(new Transaction
        {
            Id = id, UserId = _userId, WalletId = wallet.Id,
            CategoryId = category.Id, Amount = 50, OccurredAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync();

        var req = new CreateTransactionRequest(id, 100m, DateTimeOffset.UtcNow, wallet.Id, category.Id, null, null);

        var act = () => _sut.CreateAsync(_userId, req, default);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage(ErrorCodes.IdAlreadyExists);
    }

    [Fact]
    public async Task Create_MissingWallet_ThrowsNotFound()
    {
        var category = new Category
        {
            Id = Guid.NewGuid(), UserId = _userId,
            Name = "Cat", Type = CategoryType.Expense, AppliesToAllWallets = true
        };
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        var req = new CreateTransactionRequest(null, 100m, DateTimeOffset.UtcNow, Guid.NewGuid(), category.Id, null, null);

        var act = () => _sut.CreateAsync(_userId, req, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Create_CategoryNotAssignedToWallet_ThrowsValidation()
    {
        var (wallet, category) = await SeedWalletAndCategoryAsync(appliesToAll: false);
        // No WalletCategory row — category not assigned

        var req = new CreateTransactionRequest(null, 100m, DateTimeOffset.UtcNow, wallet.Id, category.Id, null, null);

        var act = () => _sut.CreateAsync(_userId, req, default);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage(ErrorCodes.CategoryNotAssignedToWallet);
    }

    [Fact]
    public async Task Create_DebtCategory_AutoResolvesDefaultParticipant()
    {
        var wallet = new Wallet
        {
            Id = Guid.NewGuid(), UserId = _userId,
            Name = "W", Type = WalletType.Regular, InitialBalance = 0, Currency = "VND"
        };
        _db.Wallets.Add(wallet);
        var debtCat = await SeedDebtCategoryAsync();
        var defaultP = await SeedDefaultParticipantAsync();

        var req = new CreateTransactionRequest(null, 100m, DateTimeOffset.UtcNow, wallet.Id, debtCat.Id, null, null);
        var result = await _sut.CreateAsync(_userId, req, default);

        var saved = _db.Transactions.Single(t => t.UserId == _userId);
        saved.ParticipantId.Should().Be(defaultP.Id);
    }

    [Fact]
    public async Task Create_DebtCategory_MissingDefaultParticipant_ThrowsDomainException()
    {
        var wallet = new Wallet
        {
            Id = Guid.NewGuid(), UserId = _userId,
            Name = "W", Type = WalletType.Regular, InitialBalance = 0, Currency = "VND"
        };
        _db.Wallets.Add(wallet);
        var debtCat = await SeedDebtCategoryAsync();
        // No default participant seeded

        var req = new CreateTransactionRequest(null, 100m, DateTimeOffset.UtcNow, wallet.Id, debtCat.Id, null, null);

        var act = () => _sut.CreateAsync(_userId, req, default);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage(ErrorCodes.DefaultParticipantMissing);
    }

    // ===== Update =====

    [Fact]
    public async Task Update_TransactionNotFound_ThrowsNotFound()
    {
        var (wallet, category) = await SeedWalletAndCategoryAsync();
        var req = new UpdateTransactionRequest(200m, DateTimeOffset.UtcNow, wallet.Id, category.Id, null, null);

        var act = () => _sut.UpdateAsync(_userId, Guid.NewGuid(), req, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ===== Delete =====

    [Fact]
    public async Task Delete_TransactionNotFound_ThrowsNotFound()
    {
        var act = () => _sut.DeleteAsync(_userId, Guid.NewGuid(), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Delete_Success_SetsDeletedAt()
    {
        var (wallet, category) = await SeedWalletAndCategoryAsync();
        var tx = new Transaction
        {
            Id = Guid.NewGuid(), UserId = _userId,
            WalletId = wallet.Id, CategoryId = category.Id,
            Amount = 100, OccurredAt = DateTimeOffset.UtcNow
        };
        _db.Transactions.Add(tx);
        await _db.SaveChangesAsync();

        await _sut.DeleteAsync(_userId, tx.Id, default);

        tx.DeletedAt.Should().NotBeNull();
    }
}
