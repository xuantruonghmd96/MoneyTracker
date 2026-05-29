using MoneyTracker.Api.Auth;
using MoneyTracker.Api.Dtos.Reports;
using MoneyTracker.Domain.Entities;
using MoneyTracker.Infrastructure.Persistence;
using MoneyTracker.Infrastructure.Persistence.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MoneyTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public ReportsController(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet("monthly")]
    public async Task<ActionResult<MonthlyReportResponse>> Monthly([FromQuery] int year, [FromQuery] int month)
    {
        if (year < 2000 || year > 2100 || month < 1 || month > 12)
            return BadRequest();

        var txs = await _db.Transactions
            .Where(t => t.UserId == _currentUser.Id && t.DeletedAt == null
                     && t.OccurredAt.Year == year && t.OccurredAt.Month == month)
            .Include(t => t.Category)
            .ToListAsync();

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

        return Ok(new MonthlyReportResponse(
            year, month,
            totalIncome, totalExpense, totalDebtOut, totalDebtIn,
            totalIncome - totalExpense,
            byCategory, topExpenses));
    }

    [HttpGet("yearly")]
    public async Task<ActionResult<YearlyReportResponse>> Yearly([FromQuery] int year)
    {
        if (year < 2000 || year > 2100)
            return BadRequest();

        var txs = await _db.Transactions
            .Where(t => t.UserId == _currentUser.Id && t.DeletedAt == null
                     && t.OccurredAt.Year == year)
            .Include(t => t.Category)
            .ToListAsync();

        var months = Enumerable.Range(1, 12).Select(m =>
        {
            var monthTxs = txs.Where(t => t.OccurredAt.Month == m).ToList();
            var income  = monthTxs.Where(t => t.Category!.Type == CategoryType.Income).Sum(t => t.Amount);
            var expense = monthTxs.Where(t => t.Category!.Type == CategoryType.Expense).Sum(t => t.Amount);
            var debtOut = monthTxs.Where(t => t.Category!.SystemKey is "DEBT_LEND" or "DEBT_REPAY").Sum(t => t.Amount);
            var debtIn  = monthTxs.Where(t => t.Category!.SystemKey is "DEBT_COLLECT" or "DEBT_BORROW").Sum(t => t.Amount);
            return new MonthlySummary(m, income, expense, debtOut, debtIn, income - expense);
        }).ToList();

        return Ok(new YearlyReportResponse(year, months));
    }

    [HttpGet("debt")]
    public async Task<ActionResult<DebtReportResponse>> Debt()
    {
        var debtSystemKeys = new[] { "DEBT_LEND", "DEBT_COLLECT", "DEBT_BORROW", "DEBT_REPAY" };

        var txs = await _db.Transactions
            .Where(t => t.UserId == _currentUser.Id && t.DeletedAt == null
                     && t.Category != null && debtSystemKeys.Contains(t.Category.SystemKey))
            .Include(t => t.Category)
            .Include(t => t.Participant)
            .ToListAsync();

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

        return Ok(new DebtReportResponse(
            theyOweMe.OrderByDescending(x => x.Outstanding).ToList(),
            iOweThem.OrderByDescending(x => x.Outstanding).ToList()));
    }
}
