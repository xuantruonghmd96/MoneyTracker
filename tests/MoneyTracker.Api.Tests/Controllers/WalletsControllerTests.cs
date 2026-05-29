using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using MoneyTracker.Api.Common;
using MoneyTracker.Api.Controllers;
using MoneyTracker.Api.Dtos.Wallets;
using MoneyTracker.Api.Tests.Helpers;
using MoneyTracker.Domain.Common;
using MoneyTracker.Domain.Entities;
using MoneyTracker.Infrastructure.Persistence;

namespace MoneyTracker.Api.Tests.Controllers;

public class WalletsControllerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly WalletsController _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public WalletsControllerTests()
    {
        _db = DbContextFactory.Create();
        _sut = new WalletsController(_db, new FakeCurrentUser(_userId));
    }

    public void Dispose() => _db.Dispose();

    private async Task<Wallet> SeedWalletAsync(WalletType type = WalletType.Regular, decimal? creditLimit = null)
    {
        var w = new Wallet
        {
            Id = Guid.NewGuid(), UserId = _userId, Name = "My Wallet",
            Type = type, CreditLimit = creditLimit,
            InitialBalance = 0, Currency = "VND"
        };
        _db.Wallets.Add(w);
        await _db.SaveChangesAsync();
        return w;
    }

    // ===== List =====

    [Fact]
    public async Task List_ReturnsOnlyCurrentUserWallets()
    {
        await SeedWalletAsync();
        _db.Wallets.Add(new Wallet
        {
            Id = Guid.NewGuid(), UserId = Guid.NewGuid(), // other user
            Name = "Other", Type = WalletType.Regular, InitialBalance = 0, Currency = "VND"
        });
        await _db.SaveChangesAsync();

        var result = await _sut.List();

        var ok = result.Result as OkObjectResult;
        var wallets = ok!.Value as List<WalletResponse>;
        wallets.Should().HaveCount(1);
    }

    // ===== Get =====

    [Fact]
    public async Task Get_Found_Returns200()
    {
        var w = await SeedWalletAsync();

        var result = await _sut.Get(w.Id);

        var ok = result.Result as OkObjectResult;
        ok.Should().NotBeNull();
        var dto = ok!.Value as WalletResponse;
        dto!.Id.Should().Be(w.Id);
    }

    [Fact]
    public async Task Get_NotFound_Returns404()
    {
        var result = await _sut.Get(Guid.NewGuid());

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    // ===== Create =====

    [Fact]
    public async Task Create_RegularWallet_Returns201()
    {
        var req = new CreateWalletRequest(null, "Cash", WalletType.Regular, null, 0, "VND", null, null);

        var result = await _sut.Create(req);

        var created = result.Result as CreatedAtActionResult;
        created.Should().NotBeNull();
        var dto = created!.Value as WalletResponse;
        dto!.Name.Should().Be("Cash");
    }

    [Fact]
    public async Task Create_CreditWallet_NullLimit_Returns400()
    {
        var req = new CreateWalletRequest(null, "CC", WalletType.Credit, null, 0, "VND", null, null);

        var result = await _sut.Create(req);

        var bad = result.Result as BadRequestObjectResult;
        bad.Should().NotBeNull();
        var err = bad!.Value as ApiError;
        err!.Error.Should().Be(ErrorCodes.InvalidCreditLimit);
    }

    [Fact]
    public async Task Create_DuplicateId_Returns409()
    {
        var w = await SeedWalletAsync();
        var req = new CreateWalletRequest(w.Id, "Dup", WalletType.Regular, null, 0, "VND", null, null);

        var result = await _sut.Create(req);

        result.Result.Should().BeOfType<ConflictObjectResult>();
    }

    // ===== Update =====

    [Fact]
    public async Task Update_NotFound_Returns404()
    {
        var req = new UpdateWalletRequest("New", null, null, null, null);

        var result = await _sut.Update(Guid.NewGuid(), req);

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Update_CreditWalletBadLimit_Returns400()
    {
        var w = await SeedWalletAsync(WalletType.Credit, 5000);
        var req = new UpdateWalletRequest("CC", null, null, null, null); // null creditLimit

        var result = await _sut.Update(w.Id, req);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ===== Delete =====

    [Fact]
    public async Task Delete_Success_SetsDeletedAtAndReturns204()
    {
        var w = await SeedWalletAsync();

        var result = await _sut.Delete(w.Id);

        result.Should().BeOfType<NoContentResult>();
        w.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Delete_NotFound_Returns404()
    {
        var result = await _sut.Delete(Guid.NewGuid());

        result.Should().BeOfType<NotFoundObjectResult>();
    }
}
