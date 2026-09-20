using System;
using System.Collections.Generic;
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
public sealed class SupplierScorecardTests(ScmDatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    [Fact]
    public async Task Scorecard_calculates_real_transaction_metrics_correctly()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        var user = new FakeCurrentUserService(new CurrentUser("ADMIN-001", "SCM Admin", "admin@r3b2p.com", ["Admin"], true));
        var poService = ServiceFactory.PurchaseOrders(context, user);
        var grnService = ServiceFactory.GoodsReceipts(context, user);
        var qcService = ServiceFactory.QualityInspections(context, user);
        var scorecardService = ServiceFactory.SupplierScorecards(context);

        // 1. Add valid FDA LTO and Sanitary Permit documents
        context.SupplierDocuments.AddRange(
            new SupplierDocument
            {
                SupplierId = world.SupplierAId,
                DocumentType = SupplierDocumentType.FdaLto,
                DocumentNumber = "FDA-LTO-2026-9999",
                ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(10)),
                IsVerified = true,
                VerifiedBy = "Compliance Lead"
            },
            new SupplierDocument
            {
                SupplierId = world.SupplierAId,
                DocumentType = SupplierDocumentType.SanitaryPermit,
                DocumentNumber = "SAN-2026-8888",
                ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(8)),
                IsVerified = true,
                VerifiedBy = "Compliance Lead"
            }
        );
        await context.SaveChangesAsync();

        // 2. Create PO 1 (100 kg, expected in 2 days)
        var po1Create = await poService.CreatePurchaseOrderAsync(new CreatePurchaseOrderRequest
        {
            SupplierId = world.SupplierAId,
            ExpectedArrivalDate = DateTime.UtcNow.AddDays(2),
            PaymentType = "Cash",
            Items = [new CreatePurchaseOrderItemRequest { ItemId = world.UbeItemId, PoItemQuantity = 100m, UnitPrice = 80m }]
        });
        var po1 = po1Create.Data!;

        // Receive GRN 1 on time
        var grn1Result = await grnService.ReceiveGoodsAsync(new CreateGoodsReceiptRequest
        {
            PoId = po1.PoId,
            DeliveryNoteNumber = "DN-001",
            Items = [new CreateGoodsReceiptItemRequest { PoItemId = po1.Items[0].PoItemId, DeliveredQuantity = 100m }]
        });
        var grn1 = grn1Result.Data!;

        // Inspect GRN 1: 100 delivered, 100 accepted (100% QA)
        await qcService.InspectIncomingGoodsAsync(new CreateQualityInspectionRequest
        {
            ReferenceType = "GRN",
            ReferenceId = grn1.GrnId,
            Items =
            [
                new CreateQualityInspectionItemRequest
                {
                    ItemId = world.UbeItemId,
                    LotId = grn1.Items[0].LotId!.Value,
                    DeliveredQuantity = 100m,
                    AcceptedQuantity = 100m,
                    RejectedQuantity = 0m
                }
            ]
        });

        // 3. Generate Scorecard
        var scorecardResult = await scorecardService.GetSupplierScorecardAsync(world.SupplierAId);

        scorecardResult.Success.Should().BeTrue();
        var sc = scorecardResult.Data!;

        sc.TotalPurchaseOrders.Should().Be(1);
        sc.TotalGoodsReceipts.Should().Be(1);
        sc.OnTimeDeliveryRate.Should().Be(100m);
        sc.QualityAcceptanceRate.Should().Be(100m);
        sc.DefectRate.Should().Be(0m);
        sc.IsFdaCompliant.Should().BeTrue();
        sc.IsSanitaryCompliant.Should().BeTrue();
        sc.IsFullyCompliant.Should().BeTrue();
        sc.CompositeScore.Should().Be(100m);
        sc.PerformanceGrade.Should().Contain("Grade A");
    }
}
