using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Tests.Infrastructure;
using Domains.Entities;
using Domains.Enums;
using Domains.Identity;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace api_scm.Tests.Foundations;

[Collection(ScmDatabaseCollection.Name)]
public sealed class GoodsReceiptAndQualityInspectionTests(ScmDatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    [Fact]
    public async Task GoodsReceipt_creates_quarantine_lot_and_ledger_entry()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        var user = new FakeCurrentUserService(new CurrentUser("RCV-001", "Warehouse Clerk Noel", "noel@r3b2p.com", ["Warehouse"], true));
        var poService = ServiceFactory.PurchaseOrders(context, user);
        var grnService = ServiceFactory.GoodsReceipts(context, user);

        // 1. Create a PO for 100 kg Ube
        var poCreate = await poService.CreatePurchaseOrderAsync(new CreatePurchaseOrderRequest
        {
            SupplierId = world.SupplierAId,
            ExpectedArrivalDate = DateTime.UtcNow.AddDays(3),
            PaymentType = "Cash",
            Items =
            [
                new CreatePurchaseOrderItemRequest { ItemId = world.UbeItemId, PoItemQuantity = 100m, UnitPrice = 85m }
            ]
        });
        poCreate.Success.Should().BeTrue();
        var po = poCreate.Data!;

        // 2. Receive goods via GRN
        var grnResult = await grnService.ReceiveGoodsAsync(new CreateGoodsReceiptRequest
        {
            PoId = po.PoId,
            DeliveryNoteNumber = "DN-88492",
            Carrier = "FastTruck Logistics",
            Notes = "Received 100kg batch in crates",
            Items =
            [
                new CreateGoodsReceiptItemRequest
                {
                    PoItemId = po.Items[0].PoItemId,
                    DeliveredQuantity = 100m,
                    SupplierLotCode = "SUPP-LOT-992",
                    ManufactureDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-2)),
                    ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(6))
                }
            ]
        });

        grnResult.Success.Should().BeTrue();
        grnResult.Data.Should().NotBeNull();
        grnResult.Data!.GrnNumber.Should().StartWith("GRN-");
        grnResult.Data.Status.Should().Be("Received");
        grnResult.Data.Items.Should().HaveCount(1);
        grnResult.Data.Items[0].LotId.Should().NotBeNull();

        // 3. Verify Lot is in Quarantine and NOT yet Available
        await using var verify = CreateContext();
        var lot = await verify.InventoryLots.FindAsync(grnResult.Data.Items[0].LotId!.Value);
        lot.Should().NotBeNull();
        lot!.Status.Should().Be(LotStatus.Quarantine);
        lot.QuantityRemaining.Should().Be(100m);
        lot.SupplierLotNo.Should().Be("SUPP-LOT-992");

        // Verify on-hand available stock cache is still 0 (since it's in quarantine)
        var inv = await verify.Inventories.FirstOrDefaultAsync(i => i.ItemId == world.UbeItemId && i.LocationId == lot.LocationId);
        (inv?.CurrentStock ?? 0m).Should().Be(0m, "Quarantine stock does not contribute to available on-hand stock");

        // Verify PO received quantity updated
        var poItem = await verify.PurchaseOrderItems.FindAsync(po.Items[0].PoItemId);
        poItem!.ReceivedQuantity.Should().Be(100m);
    }

    [Fact]
    public async Task IncomingQC_full_pass_releases_quarantine_lot_to_available()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        var user = new FakeCurrentUserService(new CurrentUser("QC-001", "QA Officer Karen", "karen@r3b2p.com", ["Quality"], true));
        var poService = ServiceFactory.PurchaseOrders(context, user);
        var grnService = ServiceFactory.GoodsReceipts(context, user);
        var qcService = ServiceFactory.QualityInspections(context, user);

        // 1. Create PO and Receive GRN
        var poCreate = await poService.CreatePurchaseOrderAsync(new CreatePurchaseOrderRequest
        {
            SupplierId = world.SupplierAId,
            ExpectedArrivalDate = DateTime.UtcNow.AddDays(2),
            PaymentType = "Terms 30",
            Items = [new CreatePurchaseOrderItemRequest { ItemId = world.UbeItemId, PoItemQuantity = 50m, UnitPrice = 80m }]
        });
        poCreate.Success.Should().BeTrue();
        var po = poCreate.Data!;

        var grnResult = await grnService.ReceiveGoodsAsync(new CreateGoodsReceiptRequest
        {
            PoId = po.PoId,
            DeliveryNoteNumber = "DN-1029",
            Items =
            [
                new CreateGoodsReceiptItemRequest
                {
                    PoItemId = po.Items[0].PoItemId,
                    DeliveredQuantity = 50m,
                    SupplierLotCode = "LOT-AAA"
                }
            ]
        });
        grnResult.Success.Should().BeTrue();
        var grn = grnResult.Data!;
        var lotId = grn.Items[0].LotId!.Value;

        // 2. QA inspects: 50 delivered, 50 accepted
        var qcResult = await qcService.InspectIncomingGoodsAsync(new CreateQualityInspectionRequest
        {
            ReferenceType = "GRN",
            ReferenceId = grn.GrnId,
            OverallNotes = "Visual, brix, and sensory tests passed within spec.",
            Items =
            [
                new CreateQualityInspectionItemRequest
                {
                    ItemId = world.UbeItemId,
                    LotId = lotId,
                    DeliveredQuantity = 50m,
                    AcceptedQuantity = 50m,
                    RejectedQuantity = 0m,
                    Notes = "High quality batch"
                }
            ]
        });

        qcResult.Success.Should().BeTrue();
        qcResult.Data!.Status.Should().Be("Passed");
        qcResult.Data.InspectionNumber.Should().StartWith("QC-IN-");

        // 3. Verify lot is now Available and stock cache updated
        await using var verify = CreateContext();
        var lot = await verify.InventoryLots.FindAsync(lotId);
        lot!.Status.Should().Be(LotStatus.Available);

        var inv = await verify.Inventories.FirstAsync(i => i.ItemId == world.UbeItemId && i.LocationId == lot.LocationId);
        inv.CurrentStock.Should().Be(50m, "Stock is now available for production");

        // Verify PO completed
        var reloadedPo = await verify.PurchaseOrders.FindAsync(po.PoId);
        reloadedPo!.Status.Should().Be(PurchaseOrderStatus.Completed);
    }

    [Fact]
    public async Task IncomingQC_partial_accept_reject_split_creates_available_and_rejected_lots_and_auto_ncr()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        var user = new FakeCurrentUserService(new CurrentUser("QC-001", "QA Lead Karen", "karen@r3b2p.com", ["Quality"], true));
        var poService = ServiceFactory.PurchaseOrders(context, user);
        var grnService = ServiceFactory.GoodsReceipts(context, user);
        var qcService = ServiceFactory.QualityInspections(context, user);

        // 1. Create PO for 90 kg and Receive GRN
        var poCreate = await poService.CreatePurchaseOrderAsync(new CreatePurchaseOrderRequest
        {
            SupplierId = world.SupplierAId,
            ExpectedArrivalDate = DateTime.UtcNow.AddDays(1),
            PaymentType = "Cash",
            Items = [new CreatePurchaseOrderItemRequest { ItemId = world.UbeItemId, PoItemQuantity = 90m, UnitPrice = 80m }]
        });
        poCreate.Success.Should().BeTrue();
        var po = poCreate.Data!;

        var grnResult = await grnService.ReceiveGoodsAsync(new CreateGoodsReceiptRequest
        {
            PoId = po.PoId,
            DeliveryNoteNumber = "DN-PARTIAL-SPLIT",
            Items =
            [
                new CreateGoodsReceiptItemRequest
                {
                    PoItemId = po.Items[0].PoItemId,
                    DeliveredQuantity = 90m,
                    SupplierLotCode = "LOT-SPLIT-90"
                }
            ]
        });
        grnResult.Success.Should().BeTrue();
        var grn = grnResult.Data!;
        var originalLotId = grn.Items[0].LotId!.Value;

        // 2. QA inspects with 80/10 split: 80 Accepted, 10 Rejected due to mold/spoilage
        var qcResult = await qcService.InspectIncomingGoodsAsync(new CreateQualityInspectionRequest
        {
            ReferenceType = "GRN",
            ReferenceId = grn.GrnId,
            OverallNotes = "10 kg rejected due to surface rot and high moisture",
            Items =
            [
                new CreateQualityInspectionItemRequest
                {
                    ItemId = world.UbeItemId,
                    LotId = originalLotId,
                    DeliveredQuantity = 90m,
                    AcceptedQuantity = 80m,
                    RejectedQuantity = 10m,
                    DefectReason = "Surface mold and bruising exceeding 5% tolerance",
                    Notes = "Crates #4 and #7 discarded"
                }
            ]
        });

        qcResult.Success.Should().BeTrue();
        qcResult.Data!.Status.Should().Be("Failed", "Contains rejected items");

        // 3. Verify original lot has 80 kg Available, and new rejected lot has 10 kg Rejected
        await using var verify = CreateContext();
        var origLot = await verify.InventoryLots.FindAsync(originalLotId);
        origLot!.Status.Should().Be(LotStatus.Available);
        origLot.QuantityRemaining.Should().Be(80m);

        var rejLot = await verify.InventoryLots.FirstOrDefaultAsync(l => l.LotCode == $"{origLot.LotCode}-REJ");
        rejLot.Should().NotBeNull();
        rejLot!.Status.Should().Be(LotStatus.Rejected);
        rejLot.QuantityRemaining.Should().Be(10m);

        // Verify available inventory cache is exactly 80
        var inv = await verify.Inventories.FirstAsync(i => i.ItemId == world.UbeItemId && i.LocationId == origLot.LocationId);
        inv.CurrentStock.Should().Be(80m);

        // 4. Verify Non-Conformance Report (NCR) was automatically filed
        var ncr = await verify.NonConformanceReports.FirstOrDefaultAsync(n => n.LotId == originalLotId);
        ncr.Should().NotBeNull();
        ncr!.NcrNumber.Should().StartWith("NCR-");
        ncr.DefectiveQuantity.Should().Be(10m);
        ncr.DefectType.Should().Contain("Surface mold");
        ncr.Status.Should().Be(NcrStatus.Open);
    }

    [Fact]
    public async Task ReturnToVendor_dispatches_rejected_lot_and_updates_status()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        var user = new FakeCurrentUserService(new CurrentUser("PROC-001", "Procurement Specialist Dan", "dan@r3b2p.com", ["Procurement"], true));
        var ncrService = ServiceFactory.Ncrs(context, user);

        // Pre-create a rejected lot
        var rejectedLot = new InventoryLot
        {
            LotCode = "LOT-REJ-TEST-001",
            ItemId = world.UbeItemId,
            LocationId = world.MainWarehouseId,
            SourceType = LotSourceType.Purchased,
            SupplierId = world.SupplierAId,
            ReceivedDate = DateTime.UtcNow.AddDays(-3),
            QuantityReceived = 15m,
            QuantityRemaining = 15m,
            UomId = world.KgUomId,
            UnitCost = 80m,
            Status = LotStatus.Rejected
        };
        context.InventoryLots.Add(rejectedLot);
        await context.SaveChangesAsync();

        // 1. Create RTV
        var rtvCreate = await ncrService.CreateRtvAsync(new CreateRtvRequest
        {
            SupplierId = world.SupplierAId,
            ItemId = world.UbeItemId,
            LotId = rejectedLot.LotId,
            ReturnedQuantity = 15m,
            Reason = "Return of defective 15kg ube tubers from inspection QC-IN-2026-0001"
        });

        rtvCreate.Success.Should().BeTrue();
        var rtv = rtvCreate.Data!;
        rtv.Status.Should().Be("Pending Dispatch");

        // 2. Dispatch RTV with Supplier Credit Note
        var dispatchResult = await ncrService.DispatchRtvAsync(rtv.RtvId, new DispatchRtvRequest
        {
            CreditNoteNumber = "CN-AGRI-2026-883"
        });

        dispatchResult.Success.Should().BeTrue();
        dispatchResult.Data!.Status.Should().Be("Credit Note Received");
        dispatchResult.Data.CreditNoteNumber.Should().Be("CN-AGRI-2026-883");

        // 3. Verify lot is marked Returned
        await using var verify = CreateContext();
        var reloadedLot = await verify.InventoryLots.FindAsync(rejectedLot.LotId);
        reloadedLot!.Status.Should().Be(LotStatus.Returned);
        reloadedLot.QuantityRemaining.Should().Be(0m);
    }
}
