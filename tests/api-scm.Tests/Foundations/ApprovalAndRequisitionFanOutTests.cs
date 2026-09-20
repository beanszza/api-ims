using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Tests.Infrastructure;
using Applications.Services;
using Domains.Entities;
using Domains.Enums;
using Domains.Identity;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace api_scm.Tests.Foundations;

[Collection(ScmDatabaseCollection.Name)]
public sealed class ApprovalAndRequisitionFanOutTests(ScmDatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    [Fact]
    public async Task Creating_approval_request_records_pending_status_and_audit_trail()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        var requester = new CurrentUser("REQ-001", "Procurement Lead John", "john@r3b2p.com", ["Procurement"], true);
        var user = new FakeCurrentUserService(requester);
        var service = ServiceFactory.Approvals(context, user);

        var request = new CreateApprovalRequest
        {
            EntityType = "PurchaseOrder",
            EntityId = 101,
            DocumentNumber = "PO-2026-0042",
            Amount = 150000m,
            Reason = "High-value raw ingredients order exceeding ₱50,000 threshold"
        };

        var result = await service.CreateApprovalRequestAsync(request);

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Status.Should().Be("Pending");
        result.Data.RequestedBy.Should().Be("Procurement Lead John");
        result.Data.Amount.Should().Be(150000m);

        await using var verify = CreateContext();
        var saved = await verify.ApprovalRequests.SingleAsync(a => a.DocumentNumber == "PO-2026-0042");
        saved.Status.Should().Be(ApprovalStatus.Pending);
        saved.Reason.Should().Contain("₱50,000");
    }

    [Fact]
    public async Task Acting_on_approval_records_approver_attribution_and_timestamp()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        var app = new ApprovalRequest
        {
            EntityType = "PurchaseOrder",
            EntityId = 202,
            DocumentNumber = "PO-2026-0099",
            Amount = 85000m,
            Status = ApprovalStatus.Pending,
            Reason = "Manager sign-off required",
            RequestedBy = "Procurement Staff",
            RequestedAt = DateTime.UtcNow.AddHours(-2)
        };
        context.ApprovalRequests.Add(app);
        await context.SaveChangesAsync();

        var approver = new CurrentUser("MGR-001", "Operations Director Ramon", "ramon@r3b2p.com", ["Director"], true);
        var user = new FakeCurrentUserService(approver);
        var service = ServiceFactory.Approvals(context, user);

        var actResult = await service.ActOnApprovalAsync(app.ApprovalRequestId, new ActOnApprovalRequest
        {
            Approve = true,
            Comments = "Approved budget allocation."
        });

        actResult.Success.Should().BeTrue();
        actResult.Data!.Status.Should().Be("Approved");
        actResult.Data.ApproverName.Should().Be("Operations Director Ramon");
        actResult.Data.ActionAt.Should().NotBeNull();
        actResult.Data.Comments.Should().Be("Approved budget allocation.");
    }

    [Fact]
    public async Task Creating_PR_calculates_estimated_totals_and_assigns_REQ_number()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        // Catalog: Supplier A sells Ube at 80 PHP, Supplier B sells Sugar at 50 PHP
        context.SupplierItems.AddRange(
            new SupplierItem { SupplierId = world.SupplierAId, ItemId = world.UbeItemId, UnitPrice = 80m, PurchaseUomId = world.KgUomId, IsPreferred = true },
            new SupplierItem { SupplierId = world.SupplierBId, ItemId = world.SugarItemId, UnitPrice = 50m, PurchaseUomId = world.KgUomId, IsPreferred = true }
        );
        await context.SaveChangesAsync();

        var user = new FakeCurrentUserService(new CurrentUser("USER-1", "Inventory Clerk Alex", "alex@r3b2p.com", ["Inventory"], true));
        var prService = ServiceFactory.PurchaseRequisitions(context, user);

        var request = new CreatePurchaseRequisitionRequest
        {
            Department = "Kitchen Production",
            RequiredDate = DateTime.UtcNow.AddDays(5),
            Purpose = "Monthly raw materials replenishment for Ube Jam production",
            Items =
            [
                new CreatePurchaseRequisitionItemRequest { ItemId = world.UbeItemId, RequestedQuantity = 200m, EstimatedUnitPrice = 80m },
                new CreatePurchaseRequisitionItemRequest { ItemId = world.SugarItemId, RequestedQuantity = 100m, EstimatedUnitPrice = 50m }
            ]
        };

        var result = await prService.CreatePurchaseRequisitionAsync(request);

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.PrNumber.Should().StartWith("REQ-");
        result.Data.Status.Should().Be("Draft");
        result.Data.EstimatedTotalAmount.Should().Be(21000m, "(200*80) + (100*50) = 16000 + 5000 = 21000");
        result.Data.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task PR_fan_out_creates_multi_vendor_POs_grouped_by_preferred_supplier()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        // Catalog: Supplier A is preferred for Ube; Supplier B is preferred for Sugar
        context.SupplierItems.AddRange(
            new SupplierItem { SupplierId = world.SupplierAId, ItemId = world.UbeItemId, UnitPrice = 90m, PurchaseUomId = world.KgUomId, IsPreferred = true },
            new SupplierItem { SupplierId = world.SupplierBId, ItemId = world.SugarItemId, UnitPrice = 55m, PurchaseUomId = world.KgUomId, IsPreferred = true }
        );
        await context.SaveChangesAsync();

        var user = new FakeCurrentUserService(new CurrentUser("MGR-001", "Procurement Manager Dave", "dave@r3b2p.com", ["Procurement"], true));
        var prService = ServiceFactory.PurchaseRequisitions(context, user);

        // 1. Create PR
        var prCreate = await prService.CreatePurchaseRequisitionAsync(new CreatePurchaseRequisitionRequest
        {
            Department = "Kitchen Production",
            RequiredDate = DateTime.UtcNow.AddDays(7),
            Purpose = "Batch Production Q3",
            Items =
            [
                new CreatePurchaseRequisitionItemRequest { ItemId = world.UbeItemId, RequestedQuantity = 150m, EstimatedUnitPrice = 90m },
                new CreatePurchaseRequisitionItemRequest { ItemId = world.SugarItemId, RequestedQuantity = 80m, EstimatedUnitPrice = 55m }
            ]
        });
        prCreate.Success.Should().BeTrue();
        var prId = prCreate.Data!.PrId;

        // 2. Approve PR
        var approveResult = await prService.UpdateStatusAsync(prId, new UpdatePurchaseRequisitionStatusRequest
        {
            Status = "Approved",
            Comments = "Budget verified and approved."
        });
        approveResult.Success.Should().BeTrue();

        // 3. Fan-out to multi-supplier POs
        var fanOutResult = await prService.FanOutToPurchaseOrdersAsync(prId);

        fanOutResult.Success.Should().BeTrue();
        fanOutResult.Data.Should().NotBeNull();
        fanOutResult.Data!.PurchaseOrdersCreatedCount.Should().Be(2, "One PO for Supplier A (Ube) and One PO for Supplier B (Sugar)");

        var pos = fanOutResult.Data.GeneratedPurchaseOrders;
        pos.Should().ContainSingle(p => p.SupplierId == world.SupplierAId && p.Items.Any(i => i.ItemId == world.UbeItemId));
        pos.Should().ContainSingle(p => p.SupplierId == world.SupplierBId && p.Items.Any(i => i.ItemId == world.SugarItemId));

        // 4. Verify PR is now marked ConvertedToPo
        await using var verify = CreateContext();
        var reloadedPr = await verify.PurchaseRequisitions.FindAsync(prId);
        reloadedPr!.Status.Should().Be(PurchaseRequisitionStatus.ConvertedToPo);
        reloadedPr.GeneratedPoNumbers.Should().NotBeNullOrEmpty();
    }
}
