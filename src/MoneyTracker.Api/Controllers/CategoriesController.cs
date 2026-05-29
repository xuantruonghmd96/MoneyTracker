using MoneyTracker.Api.Auth;
using MoneyTracker.Api.Dtos.Categories;
using MoneyTracker.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MoneyTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/categories")]
public class CategoriesController : ControllerBase
{
    private readonly CategoryService _categoryService;
    private readonly ICurrentUser _currentUser;

    public CategoriesController(CategoryService categoryService, ICurrentUser currentUser)
    {
        _categoryService = categoryService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<List<CategoryResponse>>> List(CancellationToken ct)
        => Ok(await _categoryService.ListAsync(_currentUser.Id, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CategoryResponse>> Get(Guid id, CancellationToken ct)
        => Ok(await _categoryService.GetAsync(_currentUser.Id, id, ct));

    [HttpPost]
    public async Task<ActionResult<CategoryResponse>> Create([FromBody] CreateCategoryRequest req, CancellationToken ct)
    {
        var result = await _categoryService.CreateAsync(_currentUser.Id, req, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CategoryResponse>> Update(Guid id, [FromBody] UpdateCategoryRequest req, CancellationToken ct)
        => Ok(await _categoryService.UpdateAsync(_currentUser.Id, id, req, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _categoryService.DeleteAsync(_currentUser.Id, id, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/wallets")]
    public async Task<ActionResult<List<Guid>>> GetAssignedWallets(Guid id, CancellationToken ct)
        => Ok(await _categoryService.GetAssignedWalletsAsync(_currentUser.Id, id, ct));

    [HttpPut("{id:guid}/wallets")]
    public async Task<IActionResult> SetAssignedWallets(Guid id, [FromBody] List<Guid> walletIds, CancellationToken ct)
    {
        await _categoryService.SetAssignedWalletsAsync(_currentUser.Id, id, walletIds, ct);
        return NoContent();
    }
}
