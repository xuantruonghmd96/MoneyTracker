using MoneyTracker.Api.Auth;
using MoneyTracker.Api.Dtos.Transactions;
using MoneyTracker.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MoneyTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/transactions")]
public class TransactionsController : ControllerBase
{
    private readonly TransactionService _transactionService;
    private readonly ICurrentUser _currentUser;

    public TransactionsController(TransactionService transactionService, ICurrentUser currentUser)
    {
        _transactionService = transactionService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<List<TransactionResponse>>> List(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        [FromQuery] Guid? walletId,
        [FromQuery] Guid? categoryId,
        [FromQuery] Guid? participantId,
        CancellationToken ct)
    {
        var result = await _transactionService.ListAsync(
            _currentUser.Id, from, to, walletId, categoryId, participantId, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TransactionResponse>> Get(Guid id, CancellationToken ct)
        => Ok(await _transactionService.GetAsync(_currentUser.Id, id, ct));

    [HttpPost]
    public async Task<ActionResult<TransactionResponse>> Create(
        [FromBody] CreateTransactionRequest req, CancellationToken ct)
    {
        var result = await _transactionService.CreateAsync(_currentUser.Id, req, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TransactionResponse>> Update(
        Guid id, [FromBody] UpdateTransactionRequest req, CancellationToken ct)
        => Ok(await _transactionService.UpdateAsync(_currentUser.Id, id, req, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _transactionService.DeleteAsync(_currentUser.Id, id, ct);
        return NoContent();
    }
}
