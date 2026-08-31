using Applications.Interfaces;
using Applications.Services;
using Domains.Identity;
using Infrastructures.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

namespace api_scm.Tests.Infrastructure;

/// <summary>
/// A fixed acting user for tests, standing in for what the auth service would supply.
/// </summary>
public sealed class FakeCurrentUserService(CurrentUser user) : ICurrentUserService
{
    /// <summary>A representative signed-in operator.</summary>
    public static CurrentUser Operator { get; } = new(
        UserId: "8f3c1d2e-0000-4a11-9b77-1a2b3c4d5e6f",
        DisplayName: "Maria Santos",
        Email: "maria.santos@r3b2p.test",
        Roles: ["Inventory Manager"],
        IsAuthenticated: true);

    public CurrentUser Current { get; } = user;

    public CurrentUser RequireAuthenticated() => Current.IsAuthenticated
        ? Current
        : throw new Domains.Exceptions.NotAuthenticatedException("This operation");
}

/// <summary>
/// Builds services with their real collaborators.
/// </summary>
/// <remarks>
/// Every task in this rebuild adds a dependency to one of these constructors, and updating a dozen
/// call sites each time is noise that obscures the actual change. Construction lives here so a new
/// dependency is a one-line edit.
/// <para>
/// Real implementations rather than mocks on purpose: these services are being tested for their effect
/// on the database, so substituting the posting transaction or the unit converter would test nothing.
/// </para>
/// </remarks>
public static class ServiceFactory
{
    /// <summary>
    /// The user services are constructed with unless a test asks for someone else. Authenticated, so
    /// tests assert on real attribution rather than on the anonymous fallback.
    /// </summary>
    public static ICurrentUserService DefaultUser => new FakeCurrentUserService(FakeCurrentUserService.Operator);

    public static PurchaseOrderService PurchaseOrders(
        ScmDbContext context, ICurrentUserService? user = null)
    {
        var actor = user ?? DefaultUser;
        return new PurchaseOrderService(
            context,
            NullLogger<PurchaseOrderService>.Instance,
            new StatusTransitionGuard(),
            new DocumentNumberService(context),
            new PostingTransaction(context),
            actor,
            new AuditTrail(context, actor),
            new LocationResolver(context));
    }

    public static ProductionService Production(ScmDbContext context, ICurrentUserService? user = null) => new(
        context,
        new StatusTransitionGuard(),
        new UomConversionService(context),
        new PostingTransaction(context),
        user ?? DefaultUser,
        new LocationResolver(context));

    public static StockTransferService StockTransfers(
        ScmDbContext context, ICurrentUserService? user = null)
    {
        var actor = user ?? DefaultUser;
        return new StockTransferService(
            context,
            NullLogger<StockTransferService>.Instance,
            new StatusTransitionGuard(),
            new PostingTransaction(context),
            actor,
            new AuditTrail(context, actor));
    }

    public static RecipeService Recipes(ScmDbContext context) => new(
        context,
        NullLogger<RecipeService>.Instance,
        new UomConversionService(context));

    public static ItemService Items(ScmDbContext context, ICurrentUserService? user = null)
    {
        var actor = user ?? DefaultUser;
        return new ItemService(context, NullLogger<ItemService>.Instance, new AuditTrail(context, actor));
    }

    public static LocationService Locations(ScmDbContext context, ICurrentUserService? user = null)
    {
        var actor = user ?? DefaultUser;
        return new LocationService(context, NullLogger<LocationService>.Instance, new AuditTrail(context, actor));
    }

    public static AuditTrail Audit(ScmDbContext context, ICurrentUserService? user = null)
        => new(context, user ?? DefaultUser);

    public static DocumentNumberService DocumentNumbers(ScmDbContext context) => new(context);

    public static PostingTransaction Posting(ScmDbContext context) => new(context);

    public static UomConversionService UomConversion(ScmDbContext context) => new(context);
}
