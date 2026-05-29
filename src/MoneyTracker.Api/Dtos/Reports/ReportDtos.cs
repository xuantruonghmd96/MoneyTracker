using MoneyTracker.Domain.Entities;

namespace MoneyTracker.Api.Dtos.Reports;

public record CategorySummary(
    Guid CategoryId,
    string Name,
    bool IsSystem,
    string? SystemKey,
    CategoryType Type,
    Guid? ParentId,
    decimal Amount,
    int TransactionCount);

public record TopTransaction(
    Guid Id,
    decimal Amount,
    DateTimeOffset OccurredAt,
    string? Note,
    Guid CategoryId,
    string CategoryName);

public record MonthlyReportResponse(
    int Year,
    int Month,
    decimal TotalIncome,
    decimal TotalExpense,
    decimal TotalDebtOut,
    decimal TotalDebtIn,
    decimal Net,
    List<CategorySummary> ByCategory,
    List<TopTransaction> TopExpenses);

public record MonthlySummary(
    int Month,
    decimal TotalIncome,
    decimal TotalExpense,
    decimal TotalDebtOut,
    decimal TotalDebtIn,
    decimal Net);

public record YearlyReportResponse(
    int Year,
    List<MonthlySummary> Months);

public record DebtEntry(
    Guid ParticipantId,
    string ParticipantName,
    bool IsDefault,
    decimal Lent,
    decimal Collected,
    decimal Outstanding);

public record DebtOwedEntry(
    Guid ParticipantId,
    string ParticipantName,
    bool IsDefault,
    decimal Borrowed,
    decimal Repaid,
    decimal Outstanding);

public record DebtReportResponse(
    List<DebtEntry> TheyOweMe,
    List<DebtOwedEntry> IOWeThem);
