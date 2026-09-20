using api_scm.Tests.Infrastructure;
using Applications.Services;
using Domains.Entities;
using Domains.Enums;
using Infrastructures.Persistence;
using Microsoft.EntityFrameworkCore;

namespace api_scm.Tests.Foundations;

[Collection(ScmDatabaseCollection.Name)]
public sealed class LocationRoleTests(ScmDatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    [Fact]
    public async Task The_seeder_creates_one_system_location_per_role()
    {
        await using var context = CreateContext();
        LocationSeeder.Seed(context);

        var systemLocations = await context.Locations
            .Where(l => l.IsSystemLocation)
            .ToListAsync();

        systemLocations.Select(l => l.LocationType).Should().BeEquivalentTo(new[]
        {
            LocationType.Warehouse, LocationType.Production, LocationType.Wip,
            LocationType.Quarantine, LocationType.FinishedGoods, LocationType.Disposal
        });
    }

    [Fact]
    public async Task The_seeder_is_idempotent()
    {
        await using var context = CreateContext();
        LocationSeeder.Seed(context);
        var first = await context.Locations.CountAsync();

        LocationSeeder.Seed(context);
        LocationSeeder.Seed(context);

        (await context.Locations.CountAsync()).Should().Be(first);
    }

    [Fact]
    public async Task The_seeder_adopts_a_location_the_old_auto_create_code_left_behind()
    {
        await using var context = CreateContext();

        // Exactly what the old AddBatchToInventoryAsync created on the fly: right name, no type.
        context.Locations.Add(new Location
        {
            LocationName = "Finished Goods",
            LocationType = LocationType.Unspecified,
            Address = "Main Plant",
            Status = "Active"
        });
        await context.SaveChangesAsync();

        LocationSeeder.Seed(context);

        var finishedGoods = await context.Locations
            .Where(l => l.LocationType == LocationType.FinishedGoods)
            .ToListAsync();

        finishedGoods.Should().ContainSingle("the orphan is adopted, not duplicated");
        finishedGoods[0].IsSystemLocation.Should().BeTrue();
        finishedGoods[0].Address.Should().Be("Main Plant", "the existing row is kept, not replaced");
    }

    [Fact]
    public async Task A_second_system_location_for_the_same_role_is_rejected_by_the_database()
    {
        await using var context = CreateContext();
        LocationSeeder.Seed(context);

        await using var second = CreateContext();
        second.Locations.Add(new Location
        {
            LocationName = "Another Warehouse",
            LocationType = LocationType.Warehouse,
            IsSystemLocation = true,
            Status = "Active"
        });

        var act = async () => await second.SaveChangesAsync();

        // A filtered unique index: resolving a role must always have exactly one answer.
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Ordinary_locations_may_share_a_role_freely()
    {
        await using var context = CreateContext();
        LocationSeeder.Seed(context);

        context.Locations.AddRange(
            new Location { LocationName = "Branch A", LocationType = LocationType.Branch, Status = "Active" },
            new Location { LocationName = "Branch B", LocationType = LocationType.Branch, Status = "Active" });

        await context.SaveChangesAsync();

        (await context.Locations.CountAsync(l => l.LocationType == LocationType.Branch))
            .Should().Be(2, "the unique index only constrains system locations");
    }

    [Fact]
    public async Task Resolving_a_role_with_no_system_location_fails_loudly_instead_of_creating_one()
    {
        await using var context = CreateContext();
        var resolver = new LocationResolver(context);

        var act = async () => await resolver.RequireSystemLocationAsync(LocationType.FinishedGoods);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*No system location is configured for Finished Goods*");

        (await context.Locations.CountAsync()).Should().Be(0,
            "the old code would have created one here, which is how duplicate warehouses appeared");
    }

    [Fact]
    public async Task An_inactive_location_cannot_receive_stock()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        var warehouse = await context.Locations.SingleAsync(l => l.LocationId == world.MainWarehouseId);
        warehouse.IsActive = false;
        await context.SaveChangesAsync();

        var act = async () => await new LocationResolver(context)
            .ResolveReceivingLocationAsync(world.MainWarehouseId);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*not active*");
    }

    [Fact]
    public async Task In_transit_lanes_are_created_one_per_branch()
    {
        await using var context = CreateContext();
        LocationSeeder.Seed(context);

        context.Locations.AddRange(
            new Location { LocationName = "Branch Manila", LocationType = LocationType.Branch, Status = "Active" },
            new Location { LocationName = "Bazaar SM", LocationType = LocationType.Bazaar, Status = "Active" });
        await context.SaveChangesAsync();

        LocationSeeder.SeedInTransitLanesForBranches(context);

        var lanes = await context.Locations
            .Where(l => l.LocationType == LocationType.InTransit)
            .ToListAsync();

        lanes.Should().HaveCount(2);
        lanes.Should().OnlyContain(l => l.ParentLocationId != null,
            "a lane belongs to the destination it feeds");
        lanes.Select(l => l.LocationName).Should().Contain("In Transit - Branch Manila");

        // Running again adds nothing.
        LocationSeeder.SeedInTransitLanesForBranches(context);
        (await context.Locations.CountAsync(l => l.LocationType == LocationType.InTransit)).Should().Be(2);
    }

    [Fact]
    public async Task A_system_locations_role_cannot_be_changed_through_the_api()
    {
        await using var context = CreateContext();
        LocationSeeder.Seed(context);

        var warehouse = await context.Locations
            .SingleAsync(l => l.IsSystemLocation && l.LocationType == LocationType.Warehouse);

        var result = await ServiceFactory.Locations(context).UpdateLocationAsync(
            warehouse.LocationId,
            new api_scm.Contracts.Requests.UpdateLocationRequest
            {
                LocationName = warehouse.LocationName,
                LocationType = "Branch",
                Address = warehouse.Address,
                IsActive = true
            });

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("system location");
    }

    [Fact]
    public async Task An_unknown_location_type_is_refused_with_the_accepted_list()
    {
        await using var context = CreateContext();

        var result = await ServiceFactory.Locations(context).CreateLocationAsync(
            new api_scm.Contracts.Requests.CreateLocationRequest
            {
                LocationName = "Somewhere",
                LocationType = "Teleporter",
                Address = "x",
                IsActive = true
            });

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not a valid location type");
        result.Message.Should().Contain("Warehouse");
    }
}
