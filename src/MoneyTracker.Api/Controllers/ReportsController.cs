using MoneyTracker.Api.Auth;
using MoneyTracker.Api.Dtos.Reports;
using MoneyTracker.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MoneyTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly ReportService _reportService;
    private readonly ICurrentUser _currentUser;

    public ReportsController(ReportService reportService, ICurrentUser currentUser)
    {
        _reportService = reportService;
        _currentUser = currentUser;
    }

    [HttpGet("monthly")]
    public async Task<ActionResult<MonthlyReportResponse>> Monthly([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
        => Ok(await _reportService.MonthlyAsync(_currentUser.Id, year, month, ct));

    [HttpGet("yearly")]
    public async Task<ActionResult<YearlyReportResponse>> Yearly([FromQuery] int year, CancellationToken ct)
        => Ok(await _reportService.YearlyAsync(_currentUser.Id, year, ct));

    [HttpGet("debt")]
    public async Task<ActionResult<DebtReportResponse>> Debt(CancellationToken ct)
        => Ok(await _reportService.DebtAsync(_currentUser.Id, ct));
}
