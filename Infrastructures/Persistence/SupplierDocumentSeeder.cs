using System;
using System.Linq;
using Domains.Entities;
using Domains.Enums;
using Microsoft.Extensions.Logging;

namespace Infrastructures.Persistence;

public static class SupplierDocumentSeeder
{
    public static void Seed(ScmDbContext db, ILogger logger)
    {
        if (db.SupplierDocuments.Any())
            return;

        var suppliers = db.Suppliers.ToList();
        if (!suppliers.Any())
            return;

        logger.LogInformation("→ Seeding Supplier Compliance Documents (FDA LTO, Sanitary Permits)...");

        var today = DateOnly.FromDateTime(DateTime.Today);
        var docs = new List<SupplierDocument>();

        foreach (var supplier in suppliers)
        {
            var sName = supplier.CompanyName.ToUpper();
            var shortCode = sName[..Math.Min(3, sName.Length)];

            // 1. FDA License to Operate (LTO)
            docs.Add(new SupplierDocument
            {
                SupplierId = supplier.SupplierId,
                DocumentType = SupplierDocumentType.FdaLto,
                DocumentNumber = $"LTO-300000{supplier.SupplierId:D4}89",
                Title = "FDA License to Operate - Food Trader / Wholesaler",
                IssueDate = today.AddYears(-1),
                ExpiryDate = today.AddYears(2), // Valid for 2 more years
                IsVerified = true,
                VerifiedBy = "QA Officer",
                VerifiedAt = DateTime.UtcNow.AddMonths(-6),
                Notes = "Verified against FDA Philippines Verification Portal.",
                CreatedAt = DateTime.UtcNow
            });

            // 2. Sanitary Permit
            docs.Add(new SupplierDocument
            {
                SupplierId = supplier.SupplierId,
                DocumentType = SupplierDocumentType.SanitaryPermit,
                DocumentNumber = $"SAN-{today.Year}-{shortCode}-{supplier.SupplierId:D3}",
                Title = "City Health Office Sanitary Permit to Operate",
                IssueDate = new DateOnly(today.Year, 1, 15),
                ExpiryDate = new DateOnly(today.Year, 12, 31),
                IsVerified = true,
                VerifiedBy = "Compliance Officer",
                VerifiedAt = DateTime.UtcNow.AddMonths(-3),
                Notes = "Annual City Health inspection passed Grade A.",
                CreatedAt = DateTime.UtcNow
            });

            // 3. Certificate of Analysis (COA) for specific food vendors
            if (sName.Contains("AGRI") || sName.Contains("FARM") || sName.Contains("SUGAR") || sName.Contains("DAIRY"))
            {
                docs.Add(new SupplierDocument
                {
                    SupplierId = supplier.SupplierId,
                    DocumentType = SupplierDocumentType.Coa,
                    DocumentNumber = $"COA-QC-{today.Year}-{supplier.SupplierId:D4}",
                    Title = "Batch Certificate of Analysis & Microbiological Clearance",
                    IssueDate = today.AddMonths(-2),
                    ExpiryDate = today.AddMonths(10),
                    IsVerified = true,
                    VerifiedBy = "QA Specialist",
                    VerifiedAt = DateTime.UtcNow.AddMonths(-2),
                    Notes = "Heavy metals, moisture, microbial plate counts all within PNS food standards.",
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        db.SupplierDocuments.AddRange(docs);
        db.SaveChanges();
        logger.LogInformation("✓ Seeded {Count} supplier compliance documents.", docs.Count);
    }
}
