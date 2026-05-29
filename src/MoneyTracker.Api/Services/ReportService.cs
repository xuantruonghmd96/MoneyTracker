using MoneyTracker.Api.Dtos.Reports;
using MoneyTracker.Api.Services.Exceptions;
using MoneyTracker.Domain.Common;
using MoneyTracker.Domain.Entities;
using MoneyTracker.Infrastructure.Persistence;
using MoneyTracker.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace MoneyTracker.Api.Services;

public class ReportService
{
    private readonly AppDbContext _db;

    public ReportService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<MonthlyReportResponse> MonthlyAsync(Guid userId, int year, int month, CancellationToken ct)
    {
        if (year < 2000 || year > 2100 || month < 1 || month > 12)
            throw new ValidationException(ErrorCodes.OutOfRange);

        var txs = await _db.Transactions
            .Where(t => t.UserId == userId && t.DeletedAt == null
                     && t.OccurredAt.Year == year && t.OccurredAt.Month == month)
            .Include(t => t.Category)
            .ToListAsync(ct);

        var totalIncome  = txs.Where(t => t.Category!.Type == CategoryType.Income).Sum(t => t.Amount);
        var totalExpense = txs.Where(t => t.Category!.Type == CategoryType.Expense).Sum(t => t.Amount);
        var totalDebtOut = txs.Where(t => t.Category!.SystemKey is "DEBT_LEND" or "DEBT_REPAY").Sum(t => t.Amount);
        var totalDebtIn  = txs.Where(t => t.Category!.SystemKey is "DEBT_COLLECT" or "DEBT_BORROW").Sum(t => t.Amount);

        var byCategory = txs
            .GroupBy(t => t.Category!)
            .Select(g => new CategorySummary(
                g.Key.Id, g.Key.Name, g.Key.UserId == null, g.Key.SystemKey,
                g.Key.Type, g.Key.ParentId,
                g.Sum(t => t.Amount), g.Count()))
            .OrderByDescending(x => x.Amount)
            .ToList();

        var topExpenses = txs
            .Where(t => t.Category!.Type == CategoryType.Expense)
            .OrderByDescending(t => t.Amount)
            .Take(5)
            .Select(t => new TopTransaction(
                t.Id, t.Amount, t.OccurredAt, t.Note,
                t.CategoryId, t.Category!.Name))
            .ToList();

        return new MonthlyReportResponse(
            year, month,
            totalIncome, totalExpense, totalDebtOut, totalDebtIn,
            totalIncome - totalExpense,
            byCategory, topExpenses);
    }

    public async Task<YearlyReportResponse> YearlyAsync(Guid userId, int year, CancellationToken ct)
    {
        if (year < 2000 || year > 2100)
            throw new ValidationException(ErrorCodes.OutOfRange);

        var txs = await _db.Transactions
            .Where(t => t.UserId == userId && t.DeletedAt == null
                     && t.OccurredAt.Year == year)
            .Include(t => t.Category)
            .ToListAsync(ct);

        var months = Enumerable.Range(1, 12).Select(m =>
        {
            var monthTxs = txs.Where(t => t.OccurredAt.Month == m).ToList();
            var income  = monthTxs.Where(t => t.Category!.Type == CategoryType.Income).Sum(t => t.Amount);
            var expense = monthTxs.Where(t => t.Category!.Type == CategoryType.Expense).Sum(t => t.Amount);
            var debtOut = monthTxs.Where(t => t.Category!.SystemKey is "DEBT_LEND" or "DEBT_REPAY").Sum(t => t.Amount);
            var debtIn  = monthTxs.Where(t => t.Category!.SystemKey is "DEBT_COLLECT" or "DEBT_BORROW").Sum(t => t.Amount);
            return new MonthlySummary(m, income, expense, debtOut, debtIn, income - expense);
        }).ToList();

        return new YearlyReportResponse(year, months);
    }

    public async Task<DebtReportResponse> DebtAsync(Guid userId, CancellationToken ct)
    {
        var debtSystemKeys = new[] { "DEBT_LEND", "DEBT_COLLECT", "DEBT_BORROW", "DEBT_REPAY" };

        var txs = await _db.Transactions
            .Where(t => t.UserId == userId && t.DeletedAt == null
                     && t.Category != null && debtSystemKeys.Contains(t.Category.SystemKey))
            .Include(t => t.Category)
            .Include(t => t.Participant)
            .ToListAsync(ct);

        var grouped = txs.GroupBy(t => t.Participant!);

        var theyOweMe = new List<DebtEntry>();
        var iOweThem  = new List<DebtOwedEntry>();

        foreach (var g in grouped)
        {
            var participant = g.Key;
            var lent      = g.Where(t => t.Category!.SystemKey == "DEBT_LEND").Sum(t => t.Amount);
            var collected = g.Where(t => t.Category!.SystemKey == "DEBT_COLLECT").Sum(t => t.Amount);
            var borrowed  = g.Where(t => t.Category!.SystemKey == "DEBT_BORROW").Sum(t => t.Amount);
            var repaid    = g.Where(t => t.Category!.SystemKey == "DEBT_REPAY").Sum(t => t.Amount);

            var theyOwe = lent - collected;
            var iOwe    = borrowed - repaid;

            if (theyOwe > 0)
                theyOweMe.Add(new DebtEntry(
                    participant?.Id ?? Guid.Empty,
                    participant?.Name ?? "?",
                    participant?.IsDefault ?? false,
                    lent, collected, theyOwe));

            if (iOwe > 0)
                iOweThem.Add(new DebtOwedEntry(
                    participant?.Id ?? Guid.Empty,
                    participant?.Name ?? "?",
                    participant?.IsDefault ?? false,
                    borrowed, repaid, iOwe));
        }

        return new DebtReportResponse(
            theyOweMe.OrderByDescending(x => x.Outstanding).ToList(),
            iOweThem.OrderByDescending(x => x.Outstanding).ToList());
    }
}
