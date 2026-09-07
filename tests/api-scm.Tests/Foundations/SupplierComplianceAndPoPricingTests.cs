using System;
using System.Linq;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Tests.Infrastructure;
using Applications.Interfaces;
using Applications.Services;
using Domains.Entities;
using Domains.Enums;
using Domains.Identity;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace api_scm.Tests.Foundations;

[Collection(ScmDatabaseCollection.Name)]
public sealed class SupplierComplianceAndPoPricingTests(ScmDatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private sealed class StaticCurrentUserService(CurrentUser user) : ICurrentUserService
    {
        public CurrentUser Current => user;
        public CurrentUser RequireAuthenticated() => user;
    }

    [Fact]
    public async Task Registering_supplier_document_saves_LTO_and_permits_with_expiry()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        var currentUser = new StaticCurrentUserService(new CurrentUser("QA-001", "QA Officer Elena", "qa@r3b2p.com", ["QA"], true));
        var service = new SupplierDocumentService(context, currentUser, NullLogger<SupplierDocumentService>.Instance);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var request = new CreateSupplierDocumentRequest
        {
            SupplierId = world.SupplierAId,
            DocumentType = "FDA LTO",
            DocumentNumber = "LTO-300000123456",
            Title = "FDA License to Operate - Food Manufacturer",
            IssueDate = today.AddYears(-1),
            ExpiryDate = today.AddYears(1),
            Notes = "Valid FDA LTO registered."
        };

        var result = await service.CreateDocumentAsync(request);

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.DocumentNumber.Should().Be("LTO-300000123456");
        result.Data.DocumentType.Should().Be("FDA LTO");
        result.Data.IsExpired.Should().BeFalse();
        result.Data.IsVerified.Should().BeFalse();

        await using var verify = CreateContext();
        var saved = await verify.SupplierDocuments.SingleAsync(d => d.DocumentNumber == "LTO-300000123456");
        saved.DocumentType.Should().Be(SupplierDocumentType.FdaLto);
    }

    [Fact]
    public async Task Verifying_supplier_document_sets_verification_attribution_and_date()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        var currentUser = new StaticCurrentUserService(new CurrentUser("COMP-002", "Compliance Auditor Maria", "audit@r3b2p.com", ["Compliance"], true));
        var service = new SupplierDocumentService(context, currentUser, NullLogger<SupplierDocumentService>.Instance);

        var doc = new SupplierDocument
        {
            SupplierId = world.SupplierAId,
            DocumentType = SupplierDocumentType.SanitaryPermit,
            DocumentNumber = "SAN-2026-QC-001",
            Title = "City Health Sanitary Permit",
            IssueDate = DateOnly.FromDateTime(DateTime.Today),
            ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(6)),
            IsVerified = false
        };
        context.SupplierDocuments.Add(doc);
        await context.SaveChangesAsync();

        var verifyResult = await service.VerifyDocumentAsync(new VerifySupplierDocumentRequest
        {
            DocumentId = doc.DocumentId,
            IsVerified = true,
            Notes = "Inspected physical original."
        });

        verifyResult.Success.Should().BeTrue();
        verifyResult.Data!.IsVerified.Should().BeTrue();
        verifyResult.Data.VerifiedBy.Should().Be("Compliance Auditor Maria");
        verifyResult.Data.VerifiedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Supplier_compliance_summary_correctly_computes_LTO_and_Sanitary_permit_validity()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        var currentUser = new StaticCurrentUserService(CurrentUser.Background);
        var service = new SupplierDocumentService(context, currentUser, NullLogger<SupplierDocumentService>.Instance);

        var today = DateOnly.FromDateTime(DateTime.Today);

        // Add valid LTO and expired Sanitary Permit
        context.SupplierDocuments.AddRange(
            new SupplierDocument
            {
                SupplierId = world.SupplierAId,
                DocumentType = SupplierDocumentType.FdaLto,
                DocumentNumber = "LTO-VALID",
                Title = "FDA LTO",
                IssueDate = today.AddYears(-1),
                ExpiryDate = today.AddYears(1),
                IsVerified = true
            },
            new SupplierDocument
            {
                SupplierId = world.SupplierAId,
                DocumentType = SupplierDocumentType.SanitaryPermit,
                DocumentNumber = "SAN-EXPIRED",
                Title = "Sanitary Permit",
                IssueDate = today.AddYears(-2),
                ExpiryDate = today.AddDays(-10), // Expired 10 days ago
                IsVerified = true
            }
        );
        await context.SaveChangesAsync();

        var summary = await service.GetComplianceSummaryAsync(world.SupplierAId);

        summary.Success.Should().BeTrue();
        summary.Data!.HasValidFdaLto.Should().BeTrue();
        summary.Data.HasValidSanitaryPermit.Should().BeFalse("Sanitary permit is expired");
        summary.Data.IsFullyCompliant.Should().BeFalse("requires both LTO and Sanitary permit");
        summary.Data.ExpiredDocumentsCount.Should().Be(1);
    }

    [Fact]
    public async Task Creating_PO_pulls_unit_price_from_supplier_catalog_and_calculates_total_amount()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        // Setup catalog entry: Supplier A sells Ube at 85.50 PHP per kg
        context.SupplierItems.Add(new SupplierItem
        {
            SupplierId = world.SupplierAId,
            ItemId = world.UbeItemId,
            UnitPrice = 85.50m,
            Currency = "PHP",
            PurchaseUomId = world.KgUomId,
            PackSize = 1m,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var poService = ServiceFactory.PurchaseOrders(context);

        var request = new CreatePurchaseOrderRequest
        {
            SupplierId = world.SupplierAId,
            ExpectedArrivalDate = DateTime.UtcNow.AddDays(3),
            PaymentType = "Cash",
            Items =
            [
                new CreatePurchaseOrderItemRequest
                {
                    ItemId = world.UbeItemId,
                    PoItemQuantity = 100m,
                    UnitPrice = 0 // Should pull from SupplierItem catalog (85.50)
                }
            ]
        };

        var result = await poService.CreatePurchaseOrderAsync(request);

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.TotalAmount.Should().Be(8550.00m, "100 kg * 85.50 PHP");
        result.Data.Items.Single().UnitPrice.Should().Be(85.50m);
        result.Data.Items.Single().LineTotal.Should().Be(8550.00m);
    }
}
