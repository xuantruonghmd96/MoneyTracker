using FluentAssertions;
using MoneyTracker.Api.Services;
using MoneyTracker.Api.Services.Exceptions;
using MoneyTracker.Api.Tests.Helpers;
using MoneyTracker.Domain.Common;
using MoneyTracker.Domain.Entities;
using MoneyTracker.Infrastructure.Persistence;

namespace MoneyTracker.Api.Tests.Services;

public class ReportServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ReportService _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public ReportServiceTests()
    {
        _db = DbContextFactory.Create();
        _sut = new ReportService(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task<(Wallet wallet, Category income, Category expense, Category debtLend, Category debtBorrow)> SeedCategoriesAsync()
    {
        var wallet = new Wallet
        {
            Id = Guid.NewGuid(), UserId = _userId, Name = "W",
            Type = WalletType.Regular, InitialBalance = 0, Currency = "VND"
        };
        var income = new Category
        {
            Id = Guid.NewGuid(), UserId = _userId, Name = "Income",
            Type = CategoryType.Income, AppliesToAllWallets = true
        };
        var expense = new Category
        {
            Id = Guid.NewGuid(), UserId = _userId, Name = "Expense",
            Type = CategoryType.Expense, AppliesToAllWallets = true
        };
        var debtLend = new Category
        {
            Id = Guid.NewGuid(), UserId = null, SystemKey = "DEBT_LEND",
            Name = "Cho vay", Type = CategoryType.Debt, AppliesToAllWallets = true
        };
        var debtBorrow = new Category
        {
            Id = Guid.NewGuid(), UserId = null, SystemKey = "DEBT_BORROW",
            Name = "Vay nợ", Type = CategoryType.Debt, AppliesToAllWallets = true
        };
        _db.Wallets.Add(wallet);
        _db.Categories.AddRange(income, expense, debtLend, debtBorrow);
        await _db.SaveChangesAsync();
        return (wallet, income, expense, debtLend, debtBorrow);
    }

    private Transaction MakeTx(Guid walletId, Guid categoryId, decimal amount, int year, int month, Guid? participantId = null)
        => new()
        {
            Id = Guid.NewGuid(), UserId = _userId,
            WalletId = walletId, CategoryId = categoryId,
            ParticipantId = participantId,
            Amount = amount,
            OccurredAt = new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero)
        };

    // ===== Monthly validation =====

    [Fact]
    public async Task Monthly_YearBelow2000_ThrowsValidation()
    {
        var act = () => _sut.MonthlyAsync(_userId, 1999, 6, default);
        await act.Should().ThrowAsync<ValidationException>().WithMessage(ErrorCodes.OutOfRange);
    }

    [Fact]
    public async Task Monthly_MonthAbove12_ThrowsValidation()
    {
        var act = () => _sut.MonthlyAsync(_userId, 2024, 13, default);
        await act.Should().ThrowAsync<ValidationException>().WithMessage(ErrorCodes.OutOfRange);
    }

    [Fact]
    public async Task Monthly_MonthBelow1_ThrowsValidation()
    {
        var act = () => _sut.MonthlyAsync(_userId, 2024, 0, default);
        await act.Should().ThrowAsync<ValidationException>().WithMessage(ErrorCodes.OutOfRange);
    }

    // ===== Monthly aggregation =====

    [Fact]
    public async Task Monthly_CorrectIncomeSums()
    {
        var (wallet, income, expense, _, _) = await SeedCategoriesAsync();
        _db.Transactions.AddRange(
            MakeTx(wallet.Id, income.Id, 1000, 2024, 3),
            MakeTx(wallet.Id, income.Id, 500, 2024, 3),
            MakeTx(wallet.Id, expense.Id, 200, 2024, 3));
        await _db.SaveChangesAsync();

        var result = await _sut.MonthlyAsync(_userId, 2024, 3, default);

        result.TotalIncome.Should().Be(1500);
        result.TotalExpense.Should().Be(200);
        result.Net.Should().Be(1300);
    }

    [Fact]
    public async Task Monthly_DoesNotIncludeOtherMonths()
    {
        var (wallet, income, _, _, _) = await SeedCategoriesAsync();
        _db.Transactions.Add(MakeTx(wallet.Id, income.Id, 100, 2024, 4)); // different month
        await _db.SaveChangesAsync();

        var result = await _sut.MonthlyAsync(_userId, 2024, 3, default);

        result.TotalIncome.Should().Be(0);
    }

    // ===== Yearly validation =====

    [Fact]
    public async Task Yearly_InvalidYear_ThrowsValidation()
    {
        var act = () => _sut.YearlyAsync(_userId, 2101, default);
        await act.Should().ThrowAsync<ValidationException>().WithMessage(ErrorCodes.OutOfRange);
    }

    // ===== Yearly aggregation =====

    [Fact]
    public async Task Yearly_CorrectMonthlyBreakdown()
    {
        var (wallet, income, expense, _, _) = await SeedCategoriesAsync();
        _db.Transactions.AddRange(
            MakeTx(wallet.Id, income.Id, 1000, 2024, 1),
            MakeTx(wallet.Id, expense.Id, 400, 2024, 1),
            MakeTx(wallet.Id, income.Id, 600, 2024, 6));
        await _db.SaveChangesAsync();

        var result = await _sut.YearlyAsync(_userId, 2024, default);

        result.Months.Should().HaveCount(12);
        var jan = result.Months.Single(m => m.Month == 1);
        jan.TotalIncome.Should().Be(1000);
        jan.TotalExpense.Should().Be(400);
        var jun = result.Months.Single(m => m.Month == 6);
        jun.TotalIncome.Should().Be(600);
    }

    // ===== Debt report =====

    [Fact]
    public async Task Debt_CorrectTheyOweMeAndIOweThemCalculation()
    {
        var (wallet, _, _, debtLend, debtBorrow) = await SeedCategoriesAsync();
        var participant = new Participant
        {
            Id = Guid.NewGuid(), UserId = _userId, Name = "Bob", IsDefault = false
        };
        _db.Participants.Add(participant);
        await _db.SaveChangesAsync();

        _db.Transactions.AddRange(
            MakeTx(wallet.Id, debtLend.Id, 500, 2024, 1, participant.Id),   // lent 500
            MakeTx(wallet.Id, debtBorrow.Id, 200, 2024, 1, participant.Id)); // borrowed 200
        await _db.SaveChangesAsync();

        var result = await _sut.DebtAsync(_userId, default);

        result.TheyOweMe.Should().ContainSingle(e => e.Outstanding == 500);
        result.IOWeThem.Should().ContainSingle(e => e.Outstanding == 200);
    }
}
