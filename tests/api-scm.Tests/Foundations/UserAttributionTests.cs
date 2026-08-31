using System.Security.Claims;
using api_scm.Contracts.Requests;
using api_scm.Tests.Infrastructure;
using Domains.Exceptions;
using Domains.Identity;
using Infrastructures.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace api_scm.Tests.Foundations;

/// <summary>
/// Attribution must be truthful: a real user when one is present, and visibly absent when not.
/// </summary>
public sealed class CurrentUserResolutionTests
{
    private static HttpContextCurrentUserService ServiceFor(ClaimsPrincipal? principal)
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = principal ?? new ClaimsPrincipal() }
        };
        return new HttpContextCurrentUserService(accessor);
    }

    /// <summary>Builds a principal shaped like the token the auth service issues.</summary>
    private static ClaimsPrincipal AuthServicePrincipal(params (string Type, string Value)[] claims)
    {
        var identity = new ClaimsIdentity(
            claims.Select(c => new Claim(c.Type, c.Value)),
            authenticationType: "Bearer");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public void Claims_from_the_auth_service_resolve_to_a_real_user()
    {
        // Claim names match the auth service's AuthClaimsService: OpenIddict writes sub/name/email/role.
        var principal = AuthServicePrincipal(
            ("sub", "3f9a1b2c-dead-4beef-8888-0123456789ab"),
            ("name", "Maria Santos"),
            ("email", "maria.santos@r3b2p.test"),
            ("role", "Inventory Manager"),
            ("role", "QA Officer"));

        var user = ServiceFor(principal).Current;

        user.IsAuthenticated.Should().BeTrue();
        user.UserId.Should().Be("3f9a1b2c-dead-4beef-8888-0123456789ab");
        user.DisplayName.Should().Be("Maria Santos");
        user.Email.Should().Be("maria.santos@r3b2p.test");
        user.Roles.Should().BeEquivalentTo(["Inventory Manager", "QA Officer"]);
        user.IsInRole("inventory manager").Should().BeTrue("role checks are case-insensitive");
    }

    [Fact]
    public void The_longer_ClaimTypes_uris_are_accepted_too()
    {
        // Whether the short or the URI form arrives depends on claim-type mapping in the pipeline.
        var principal = AuthServicePrincipal(
            (ClaimTypes.NameIdentifier, "user-42"),
            (ClaimTypes.Name, "Jose Rizal"),
            (ClaimTypes.Role, "Admin"));

        var user = ServiceFor(principal).Current;

        user.UserId.Should().Be("user-42");
        user.DisplayName.Should().Be("Jose Rizal");
        user.Roles.Should().ContainSingle().Which.Should().Be("Admin");
    }

    [Fact]
    public void A_request_with_no_identity_resolves_to_anonymous_not_to_a_fabricated_person()
    {
        var user = ServiceFor(null).Current;

        user.IsAuthenticated.Should().BeFalse();
        user.UserId.Should().Be(SystemUsers.Unauthenticated);
        SystemUsers.IsSystemActor(user.UserId).Should().BeTrue();

        // The point of the whole task: the absence of a user is recorded as an absence.
        user.UserId.Should().NotBe("1");
    }

    [Fact]
    public void An_authenticated_token_with_no_subject_is_treated_as_anonymous()
    {
        // A token that proves nothing about who the caller is cannot be used for attribution.
        var principal = AuthServicePrincipal(("name", "Nameless"));

        ServiceFor(principal).Current.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public void RequireAuthenticated_throws_when_there_is_no_user()
    {
        var act = () => ServiceFor(null).RequireAuthenticated();

        act.Should().Throw<NotAuthenticatedException>()
            .WithMessage("*requires an authenticated user*");
    }

    [Fact]
    public void RequireAuthenticated_returns_the_user_when_one_is_present()
    {
        var principal = AuthServicePrincipal(("sub", "abc"), ("name", "Someone"));

        ServiceFor(principal).RequireAuthenticated().UserId.Should().Be("abc");
    }

    [Fact]
    public void Legacy_and_system_identifiers_are_recognisable_and_cannot_collide_with_a_real_subject()
    {
        SystemUsers.Legacy(1).Should().Be("legacy:1");
        SystemUsers.IsSystemActor(SystemUsers.Legacy(1)).Should().BeTrue();
        SystemUsers.IsSystemActor(SystemUsers.System).Should().BeTrue();
        SystemUsers.IsSystemActor(SystemUsers.Migration).Should().BeTrue();

        // A genuine auth subject is never mistaken for a system actor.
        SystemUsers.IsSystemActor("3f9a1b2c-dead-4beef-8888-0123456789ab").Should().BeFalse();
    }

    [Fact]
    public void AuditName_falls_back_to_the_identifier_so_a_trail_is_never_blank()
    {
        new CurrentUser("abc", null, null, [], true).AuditName.Should().Be("abc");
        new CurrentUser("abc", "   ", null, [], true).AuditName.Should().Be("abc");
        new CurrentUser("abc", "Real Name", null, [], true).AuditName.Should().Be("Real Name");
    }
}

/// <summary>
/// End-to-end attribution: what actually lands in the database when an operation is posted.
/// </summary>
[Collection(ScmDatabaseCollection.Name)]
public sealed class AuditAttributionTests(ScmDatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    [Fact]
    public async Task An_audit_entry_names_the_acting_user()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);
        await TestDataSeeder.GiveStockAsync(
            context, world.UbeHalaya500ItemId, world.FinishedGoodsLocationId, 50);

        var created = await ServiceFactory.StockTransfers(context).CreateTransferAsync(
            new CreateStockTransferRequest
            {
                ProductId = world.UbeHalaya500ProductId,
                SourceLocationId = world.FinishedGoodsLocationId,
                DestLocationId = world.BranchManilaId,
                TransferQuantity = 5m
            });
        created.Success.Should().BeTrue(created.Message);

        await using var verify = CreateContext();
        var entry = await verify.AuditLogs.SingleAsync(a => a.Action == "Created");

        entry.UserId.Should().Be(FakeCurrentUserService.Operator.UserId);
        entry.UserName.Should().Be("Maria Santos");
        entry.EntityName.Should().Be("StockTransfer");
    }

    [Fact]
    public async Task An_unattributed_request_is_recorded_as_anonymous_rather_than_as_someone()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);
        await TestDataSeeder.GiveStockAsync(
            context, world.UbeHalaya500ItemId, world.FinishedGoodsLocationId, 50);

        var anonymous = new FakeCurrentUserService(CurrentUser.Anonymous);

        await ServiceFactory.StockTransfers(context, anonymous).CreateTransferAsync(
            new CreateStockTransferRequest
            {
                ProductId = world.UbeHalaya500ProductId,
                SourceLocationId = world.FinishedGoodsLocationId,
                DestLocationId = world.BranchManilaId,
                TransferQuantity = 5m
            });

        await using var verify = CreateContext();
        var entry = await verify.AuditLogs.SingleAsync(a => a.Action == "Created");

        entry.UserId.Should().Be(SystemUsers.Unauthenticated);
        entry.UserName.Should().Be("Unauthenticated",
            "an unattributed action says so, instead of crediting user 1");
    }

    [Fact]
    public async Task Audit_entries_roll_back_with_the_operation_they_describe()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);
        await TestDataSeeder.GiveStockAsync(
            context, world.UbeHalaya500ItemId, world.FinishedGoodsLocationId, 50);

        var service = ServiceFactory.StockTransfers(context);
        var created = await service.CreateTransferAsync(new CreateStockTransferRequest
        {
            ProductId = world.UbeHalaya500ProductId,
            SourceLocationId = world.FinishedGoodsLocationId,
            DestLocationId = world.BranchManilaId,
            TransferQuantity = 50m
        });

        // Drain the stock so dispatch fails after the audit entry has been staged.
        await using (var drain = CreateContext())
        {
            var balance = await drain.Inventories.SingleAsync(i =>
                i.ItemId == world.UbeHalaya500ItemId && i.LocationId == world.FinishedGoodsLocationId);
            balance.CurrentStock = 1m;
            await drain.SaveChangesAsync();
        }

        await using var dispatchContext = CreateContext();
        var dispatch = await ServiceFactory.StockTransfers(dispatchContext).UpdateTransferStatusAsync(
            created.Data!.TransferId,
            new UpdateStockTransferStatusRequest { Status = "In Transit" });

        dispatch.Success.Should().BeFalse();

        await using var verify = CreateContext();
        (await verify.AuditLogs.CountAsync(a => a.Action == "StatusUpdated")).Should().Be(0,
            "an audit row claiming a dispatch happened, when it did not, is worse than no row");
    }

    [Fact]
    public async Task Item_changes_no_longer_abuse_the_FieldName_column_to_store_a_username()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        var result = await ServiceFactory.Items(context).CreateItemAsync(new CreateItemRequest
        {
            ItemName = "Vanilla Extract",
            UomId = world.KgUomId,
            CategoryId = world.RawMaterialCategoryId,
            MinStockLevel = 1m,
            MaxStockLevel = 10m,
            IsActive = true
        });

        result.Success.Should().BeTrue(result.Message);

        await using var verify = CreateContext();
        var entry = await verify.AuditLogs.SingleAsync(a => a.EntityId == "Vanilla Extract");

        entry.UserName.Should().Be("Maria Santos");
        entry.FieldName.Should().NotBe("scmsuser",
            "the old code stuffed a username into the column meant for the changed field");
    }
}
