using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;
using api_scm.Tests.Infrastructure;
using Applications.Services;
using Domains.Entities;
using Domains.Enums;
using Domains.Identity;
using Infrastructures.Persistence;
using Microsoft.EntityFrameworkCore;

namespace api_scm.Tests.Characterization;

/// <summary>
/// Pins the behaviour of purchase order receiving, including the behaviour that is wrong. Each test
/// names the defect it documents, so when the rebuild changes the behaviour the failing test is a
/// deliberate signal rather than an accident.
/// </summary>
[Collection(ScmDatabaseCollection.Name)]
public sealed class PurchaseOrderReceivingCharacterizationTests(ScmDatabaseFixture fixture)
    : DatabaseTestBase(fixture)
{
    /// <summary>
    /// Walks the real lifecycle Pending -> Arrived -> Completed. Since Task 2 the guard refuses to jump
    /// straight to Completed, because QA inspection happens on arrival.
    /// </summary>
    private static async Task<ApiResponse<PurchaseOrderResponse>> ReceiveAsync(
        PurchaseOrderService service,
        int poId,
        UpdatePurchaseOrderQaRequest? qa = null)
    {
        await service.UpdateOrderStatusAsync(poId, new UpdatePurchaseOrderQaRequest
        {
            Status = "Arrived"
        });

        qa ??= new UpdatePurchaseOrderQaRequest();
        qa.Status = "Completed";
        return await service.UpdateOrderStatusAsync(poId, qa);
    }

    [Fact(DisplayName = "Defect 06: receiving is all-or-nothing - partial acceptance cannot be recorded")]
    public async Task Defect06_Receiving_Is_All_Or_Nothing()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);
        var order = await TestDataSeeder.AddPurchaseOrderAsync(
            context, world.SupplierAId, world.UbeItemId, quantity: 90);

        var service = ServiceFactory.PurchaseOrders(context);

        // There is no way to say "80 arrived good, 10 were rejected". The only lever is the status.
        var result = await ReceiveAsync(service, order.PoId, new UpdatePurchaseOrderQaRequest
        {
            QaStatus = "Passed",
            QaNotes = "10 kg was mouldy",
            InspectedBy = "QA Officer"
        });

        result.Success.Should().BeTrue(result.Message);

        await using var verify = CreateContext();

        // Navigated via the include, not filtered on PoId: PoId is a dead column (see defect 24).
        var stored = await verify.PurchaseOrders
            .Include(o => o.PurchaseOrderItems)
            .SingleAsync(o => o.PoId == order.PoId);
        var line = stored.PurchaseOrderItems.Single();

        // The full ordered quantity is force-assigned regardless of what actually passed QA.
        line.ReceivedQuantity.Should().Be(90m,
            "current code assigns ReceivedQuantity = PoItemQuantity with no accepted/rejected split");

        var onHand = await verify.Inventories
            .Where(i => i.ItemId == world.UbeItemId)
            .SumAsync(i => i.CurrentStock);

        onHand.Should().Be(90m,
            "all 90 kg enters stock even though only 80 kg was fit for use - the 10 kg is silently " +
            "lost. Tasks 17 and 18 replace this with a receipt and a per-line inspection");
    }

    [Fact(DisplayName = "Defect 07: QA verdict is header-level, so a multi-item PO gets one verdict")]
    public async Task Defect07_Qa_Is_Header_Level_Only()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        var order = new PurchaseOrder
        {
            PoNumber = $"PO-{DateTime.UtcNow.Year}-9001",
            SupplierId = world.SupplierAId,
            OrderDate = DateTime.UtcNow,
            ExpectedArrivalDate = DateTime.UtcNow.AddDays(2),
            Status = PurchaseOrderStatus.Pending,
            PaymentType = "Cash",
            ProofImageUrl = string.Empty,
            PurchaseOrderItems =
            [
                new PurchaseOrderItem { ItemId = world.UbeItemId, SupplierId = world.SupplierAId, PoItemQuantity = 90m },
                new PurchaseOrderItem { ItemId = world.SugarItemId, SupplierId = world.SupplierAId, PoItemQuantity = 50m }
            ]
        };
        context.PurchaseOrders.Add(order);
        await context.SaveChangesAsync();

        await ReceiveAsync(ServiceFactory.PurchaseOrders(context), order.PoId, new UpdatePurchaseOrderQaRequest
        {
            QaStatus = "Passed",
            QaNotes = "Ube good, sugar wet"
        });

        await using var verify = CreateContext();
        var stored = await verify.PurchaseOrders
            .Include(o => o.PurchaseOrderItems)
            .SingleAsync(o => o.PoId == order.PoId);

        // One QaStatus for the whole document. The per-line reality is unrepresentable.
        stored.QaStatus.Should().Be("Passed");
        stored.PurchaseOrderItems.Should().OnlyContain(l => l.ReceivedQuantity == l.PoItemQuantity,
            "both lines are accepted in full because the verdict has nowhere per-line to live");
    }

    [Fact(DisplayName = "Defect 08 FIXED in Task 7: stock lands in the designated receiving warehouse")]
    public async Task Defect08_Fixed_Receiving_Resolves_The_Warehouse_By_Role()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        var order = await TestDataSeeder.AddPurchaseOrderAsync(
            context, world.SupplierAId, world.UbeItemId, quantity: 25);

        await ReceiveAsync(ServiceFactory.PurchaseOrders(context), order.PoId);

        await using var verify = CreateContext();
        var inventory = await verify.Inventories.SingleAsync(i => i.ItemId == world.UbeItemId);

        inventory.LocationId.Should().Be(world.MainWarehouseId,
            "the location is resolved by its Warehouse role, not by whichever row sorted first");

        // No driver is invented. A driver belongs to a shipment, not to a stock balance.
        (await verify.Drivers.CountAsync()).Should().Be(0,
            "the old code fabricated a 'Default Driver' purely to fill a column");
        inventory.DriverId.Should().BeNull();
    }

    [Fact(DisplayName = "Task 7: receiving into a non-receiving location is refused")]
    public async Task Task07_Receiving_Into_A_Branch_Is_Refused()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);
        var order = await TestDataSeeder.AddPurchaseOrderAsync(
            context, world.SupplierAId, world.UbeItemId, quantity: 25);

        var service = ServiceFactory.PurchaseOrders(context);
        await service.UpdateOrderStatusAsync(order.PoId, new UpdatePurchaseOrderQaRequest { Status = "Arrived" });

        var result = await service.UpdateOrderStatusAsync(order.PoId, new UpdatePurchaseOrderQaRequest
        {
            Status = "Completed",
            ReceivingLocationId = world.BranchManilaId
        });

        result.Success.Should().BeFalse("a retail branch is not a goods-receiving location");
        result.Message.Should().Contain("Branch");

        await using var verify = CreateContext();
        (await verify.Inventories.CountAsync()).Should().Be(0, "and nothing was posted");
    }

    [Fact(DisplayName = "Task 7: an explicit receiving location is honoured")]
    public async Task Task07_An_Explicit_Receiving_Location_Is_Used()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);
        var order = await TestDataSeeder.AddPurchaseOrderAsync(
            context, world.SupplierAId, world.UbeItemId, quantity: 25);

        var service = ServiceFactory.PurchaseOrders(context);
        await service.UpdateOrderStatusAsync(order.PoId, new UpdatePurchaseOrderQaRequest { Status = "Arrived" });
        var result = await service.UpdateOrderStatusAsync(order.PoId, new UpdatePurchaseOrderQaRequest
        {
            Status = "Completed",
            ReceivingLocationId = world.QuarantineLocationId
        });

        result.Success.Should().BeTrue(result.Message);

        await using var verify = CreateContext();
        (await verify.Inventories.SingleAsync(i => i.ItemId == world.UbeItemId))
            .LocationId.Should().Be(world.QuarantineLocationId);
    }

    [Fact(DisplayName = "Defect 09: editing a received PO wipes ReceivedQuantity while stock stays")]
    public async Task Defect09_Received_Po_Is_Still_Editable_And_Corrupts_Data()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);
        var order = await TestDataSeeder.AddPurchaseOrderAsync(
            context, world.SupplierAId, world.UbeItemId, quantity: 90);

        var service = ServiceFactory.PurchaseOrders(context);
        await ReceiveAsync(service, order.PoId);

        await using (var afterReceipt = CreateContext())
        {
            var received = await afterReceipt.PurchaseOrders
                .Include(o => o.PurchaseOrderItems)
                .SingleAsync(o => o.PoId == order.PoId);
            received.PurchaseOrderItems.Single().ReceivedQuantity.Should().Be(90m);
        }

        // The PO is Completed and its stock is already on hand, yet the edit path accepts it.
        var edit = await service.UpdatePurchaseOrderAsync(order.PoId, new CreatePurchaseOrderRequest
        {
            SupplierId = world.SupplierAId,
            ExpectedArrivalDate = DateTime.UtcNow.AddDays(5),
            PaymentType = "Cash",
            Items = [new CreatePurchaseOrderItemRequest { ItemId = world.UbeItemId, PoItemQuantity = 5m }]
        });

        edit.Success.Should().BeTrue("there is no status guard on UpdatePurchaseOrderAsync");

        await using var verify = CreateContext();
        var afterEdit = await verify.PurchaseOrders
            .Include(o => o.PurchaseOrderItems)
            .SingleAsync(o => o.PoId == order.PoId);
        var line = afterEdit.PurchaseOrderItems.Single();
        var onHand = await verify.Inventories
            .Where(i => i.ItemId == world.UbeItemId)
            .SumAsync(i => i.CurrentStock);

        line.PoItemQuantity.Should().Be(5m, "the original line was deleted and replaced");
        line.ReceivedQuantity.Should().Be(0m, "the receipt record is destroyed");
        onHand.Should().Be(90m, "but the 90 kg it accounted for is still sitting in inventory");

        // Zero received against ninety on hand is unreconcilable. Task 14 adds the immutability guard.
    }

    [Fact(DisplayName = "Defect 16 FIXED in Task 6: receiving is attributed to the acting user")]
    public async Task Defect16_Fixed_Movement_Log_Records_The_Real_User()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);
        var order = await TestDataSeeder.AddPurchaseOrderAsync(
            context, world.SupplierAId, world.UbeItemId, quantity: 12);

        await ReceiveAsync(ServiceFactory.PurchaseOrders(context), order.PoId);

        await using var verify = CreateContext();
        var log = await verify.InventoryMovementLogs.SingleAsync();

        log.UserId.Should().Be(FakeCurrentUserService.Operator.UserId,
            "the movement is attributed to whoever made the request, not to a hardcoded 1");
        log.UserName.Should().Be("Maria Santos",
            "the display name is captured so the trail reads without calling the auth service");

        log.ActionType.Should().Be("Order Arrival",
            "free-text action types are inconsistent across services - see defect 15");
    }

    [Fact(DisplayName = "Defect 24 (NEW): PurchaseOrderItem.PoId is a dead column; the real FK is a nullable shadow")]
    public async Task Defect24_PoId_Is_Never_Populated_Because_The_Fk_Is_A_Shadow_Property()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);
        var order = await TestDataSeeder.AddPurchaseOrderAsync(
            context, world.SupplierAId, world.UbeItemId, quantity: 90);

        order.PoId.Should().BeGreaterThan(0);

        await using var verify = CreateContext();

        // The line was inserted and is reachable through the navigation.
        var viaNavigation = await verify.PurchaseOrders
            .Include(o => o.PurchaseOrderItems)
            .SingleAsync(o => o.PoId == order.PoId);
        viaNavigation.PurchaseOrderItems.Should().HaveCount(1);

        // But the declared PoId property was never wired up as the foreign key, so it stays 0.
        viaNavigation.PurchaseOrderItems.Single().PoId.Should().Be(0,
            "EF Core convention did not adopt PoId as the FK for the PurchaseOrder navigation");

        // Which means the obvious query returns nothing at all.
        var viaPoId = await verify.PurchaseOrderItems.Where(l => l.PoId == order.PoId).ToListAsync();
        viaPoId.Should().BeEmpty(
            "any report or join written against PurchaseOrderItem.PoId silently returns no rows");

        // The relationship actually lives in a shadow property, and it is nullable, so the database
        // permits orphaned purchase order lines.
        var entityType = verify.Model.FindEntityType(typeof(PurchaseOrderItem))!;
        var shadowFk = entityType.GetForeignKeys()
            .Single(fk => fk.PrincipalEntityType.ClrType == typeof(PurchaseOrder));

        shadowFk.Properties.Should().ContainSingle()
            .Which.Name.Should().Be("PurchaseOrderPoId");
        shadowFk.Properties.Single().IsShadowProperty().Should().BeTrue();
        shadowFk.Properties.Single().IsNullable.Should().BeTrue(
            "a purchase order line can exist with no parent order");

        // Every other relationship in the schema mapped onto its declared property correctly. This one
        // entity is the exception, which is why it went unnoticed. Task 14 fixes it.
        var recipeIngredientFk = verify.Model.FindEntityType(typeof(RecipeIngredient))!
            .GetForeignKeys()
            .Single(fk => fk.PrincipalEntityType.ClrType == typeof(Recipe));
        recipeIngredientFk.Properties.Single().Name.Should().Be("RecipeId");
        recipeIngredientFk.Properties.Single().IsShadowProperty().Should().BeFalse();
    }

    [Fact(DisplayName = "Defect 15: action type vocabulary is inconsistent free text")]
    public async Task Defect15_ActionType_Is_Unconstrained_Free_Text()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        // Nothing prevents an arbitrary string from entering the ledger.
        context.InventoryMovementLogs.Add(new InventoryMovementLog
        {
            ItemId = world.UbeItemId,
            LocationId = world.MainWarehouseId,
            ChangeQuantity = 1m,
            ActionType = "totally-made-up-action",
            ReferenceId = "n/a",
            UserId = SystemUsers.System,
            Timestamp = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        await using var verify = CreateContext();
        var log = await verify.InventoryMovementLogs.SingleAsync();
        log.ActionType.Should().Be("totally-made-up-action",
            "there is no enum or check constraint on ActionType, so reports cannot group reliably. " +
            "Task 9 converts the column and normalises the history");
    }
}
