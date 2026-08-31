using Domains.Entities;
using Domains.Enums;
using Infrastructures.Persistence;
using Microsoft.EntityFrameworkCore;

namespace api_scm.Tests.Infrastructure;

/// <summary>
/// Ids of the baseline fixture, so tests read as domain language rather than magic numbers.
/// </summary>
public sealed class SeededWorld
{
    public int KgUomId { get; init; }
    public int GramUomId { get; init; }
    public int PieceUomId { get; init; }

    public int RawMaterialCategoryId { get; init; }
    public int ToolsAndSuppliesCategoryId { get; init; }
    public int FinishedGoodCategoryId { get; init; }

    public int UbeItemId { get; init; }
    public int SugarItemId { get; init; }
    public int Jar500ItemId { get; init; }
    public int LidItemId { get; init; }
    public int UbeHalaya500ItemId { get; init; }

    public int SupplierAId { get; init; }
    public int SupplierBId { get; init; }

    public int MainWarehouseId { get; init; }
    public int ProductionLocationId { get; init; }
    public int WipLocationId { get; init; }
    public int QuarantineLocationId { get; init; }
    public int FinishedGoodsLocationId { get; init; }
    public int DisposalLocationId { get; init; }
    public int BranchManilaId { get; init; }

    public int UbeHalaya500ProductId { get; init; }
    public int UbeHalaya500RecipeId { get; init; }
}

/// <summary>
/// Builds the common Ube Halaya / Ube Jam fixture used across the suite.
/// </summary>
public static class TestDataSeeder
{
    /// <summary>Ube required by one batch of the seeded recipe, in kg.</summary>
    public const int UbePerBatchKg = 10;

    /// <summary>Sugar required by one batch of the seeded recipe, in kg.</summary>
    public const int SugarPerBatchKg = 5;

    /// <summary>Jars produced by one batch of the seeded recipe.</summary>
    public const int JarsPerBatch = 20;

    public static async Task<SeededWorld> SeedBaselineAsync(ScmDbContext context)
    {
        // Use the production seeder so tests exercise the same dimensions and conversion factors the
        // application runs with, rather than a parallel set that could drift out of agreement.
        UnitOfMeasureSeeder.Seed(context);

        var unitsByCode = (await context.UnitOfMeasures.ToListAsync())
            .ToDictionary(u => u.Code, u => u);
        var kg = unitsByCode[UnitOfMeasureSeeder.Codes.Kilogram];
        var gram = unitsByCode[UnitOfMeasureSeeder.Codes.Gram];
        var piece = unitsByCode[UnitOfMeasureSeeder.Codes.Piece];

        // "Raw Materials" plural, matching what Program.cs seeds and what ItemService validates against.
        var rawMaterial = new Category { CategoryName = "Raw Materials", Description = "Ingredients" };
        var toolsAndSupplies = new Category { CategoryName = "Tools and Supplies", Description = "Packaging and tools" };
        var finishedGood = new Category { CategoryName = "Finished Good", Description = "Finished Goods" };
        context.Categories.AddRange(rawMaterial, toolsAndSupplies, finishedGood);

        await context.SaveChangesAsync();

        var ube = NewItem("Ube", kg.UomId, rawMaterial.CategoryId, minStock: 20);
        var sugar = NewItem("Sugar", kg.UomId, rawMaterial.CategoryId, minStock: 10);
        var jar = NewItem("Jar 500g", piece.UomId, toolsAndSupplies.CategoryId, minStock: 100);
        var lid = NewItem("Lid", piece.UomId, toolsAndSupplies.CategoryId, minStock: 100);
        var halayaItem = NewItem("Ube Halaya 500g", piece.UomId, finishedGood.CategoryId, minStock: 24);
        context.Items.AddRange(ube, sugar, jar, lid, halayaItem);

        var supplierA = NewSupplier("Supplier A Farms", "a@example.test");
        var supplierB = NewSupplier("Supplier B Trading", "b@example.test");
        context.Suppliers.AddRange(supplierA, supplierB);

        var mainWarehouse = NewLocation("Main Warehouse", LocationType.Warehouse);
        var production = NewLocation("Production Floor", LocationType.Production);
        var wip = NewLocation("Work In Progress", LocationType.Wip);
        var quarantine = NewLocation("Quarantine Hold", LocationType.Quarantine);
        var finishedGoods = NewLocation("Finished Goods", LocationType.FinishedGoods);
        var disposal = NewLocation("Disposal", LocationType.Disposal);
        // A branch is an ordinary location, not a system role: there can be many of them.
        var branchManila = NewLocation("Branch Manila", LocationType.Branch, isSystem: false);
        context.Locations.AddRange(
            mainWarehouse, production, wip, quarantine, finishedGoods, disposal, branchManila);

        await context.SaveChangesAsync();

        var halayaProduct = new FinishedProduct
        {
            ItemId = halayaItem.ItemId,
            SellingPrice = 180m,
            Sku = "FG-UH-500",
            Variant = "500g"
        };
        context.FinishedProducts.Add(halayaProduct);
        await context.SaveChangesAsync();

        var recipe = new Recipe
        {
            ProductId = halayaProduct.ProductId,
            RecipeName = "Ube Halaya 500g",
            OutputQuantity = JarsPerBatch,
            Notes = "Baseline test recipe",
            IsActive = true
        };
        context.Recipes.Add(recipe);
        await context.SaveChangesAsync();

        context.RecipeIngredients.AddRange(
            new RecipeIngredient
            {
                RecipeId = recipe.RecipeId,
                ItemId = ube.ItemId,
                UomId = kg.UomId,
                StandardQuantity = UbePerBatchKg
            },
            new RecipeIngredient
            {
                RecipeId = recipe.RecipeId,
                ItemId = sugar.ItemId,
                UomId = kg.UomId,
                StandardQuantity = SugarPerBatchKg
            });

        await context.SaveChangesAsync();

        return new SeededWorld
        {
            KgUomId = kg.UomId,
            GramUomId = gram.UomId,
            PieceUomId = piece.UomId,
            RawMaterialCategoryId = rawMaterial.CategoryId,
            ToolsAndSuppliesCategoryId = toolsAndSupplies.CategoryId,
            FinishedGoodCategoryId = finishedGood.CategoryId,
            UbeItemId = ube.ItemId,
            SugarItemId = sugar.ItemId,
            Jar500ItemId = jar.ItemId,
            LidItemId = lid.ItemId,
            UbeHalaya500ItemId = halayaItem.ItemId,
            SupplierAId = supplierA.SupplierId,
            SupplierBId = supplierB.SupplierId,
            MainWarehouseId = mainWarehouse.LocationId,
            ProductionLocationId = production.LocationId,
            WipLocationId = wip.LocationId,
            QuarantineLocationId = quarantine.LocationId,
            FinishedGoodsLocationId = finishedGoods.LocationId,
            DisposalLocationId = disposal.LocationId,
            BranchManilaId = branchManila.LocationId,
            UbeHalaya500ProductId = halayaProduct.ProductId,
            UbeHalaya500RecipeId = recipe.RecipeId
        };
    }

    /// <summary>Adds an on-hand balance for an item at a location.</summary>
    public static async Task GiveStockAsync(ScmDbContext context, int itemId, int locationId, int quantity)
    {
        context.Inventories.Add(new Inventory
        {
            ItemId = itemId,
            LocationId = locationId,
            CurrentStock = quantity
        });
        await context.SaveChangesAsync();
    }

    public static async Task<PurchaseOrder> AddPurchaseOrderAsync(
        ScmDbContext context,
        int supplierId,
        int itemId,
        int quantity,
        PurchaseOrderStatus status = PurchaseOrderStatus.Pending)
    {
        var order = new PurchaseOrder
        {
            SupplierId = supplierId,
            OrderDate = DateTime.UtcNow,
            ExpectedArrivalDate = DateTime.UtcNow.AddDays(3),
            Status = status,
            PaymentType = "Cash",
            ProofImageUrl = string.Empty,
            TotalAmount = 0m,
            PurchaseOrderItems =
            [
                new PurchaseOrderItem
                {
                    ItemId = itemId,
                    SupplierId = supplierId,
                    PoItemQuantity = quantity,
                    ReceivedQuantity = 0
                }
            ]
        };

        context.PurchaseOrders.Add(order);
        await context.SaveChangesAsync();
        return order;
    }

    private static Item NewItem(string name, int uomId, int categoryId, int minStock) => new()
    {
        ItemName = name,
        UomId = uomId,
        // Stock is held in the same unit the item is displayed in, matching what item creation does.
        StockUomId = uomId,
        CategoryId = categoryId,
        MinStockLevel = minStock,
        MaxStockLevel = minStock * 10,
        IsActive = true
    };

    private static Supplier NewSupplier(string company, string email) => new()
    {
        CompanyName = company,
        ContactPerson = "Contact Person",
        Email = email,
        Phone = "0900-000-0000",
        Address = "Test Address",
        IsActive = true
    };

    /// <summary>
    /// Creates a location that plays a system role, so <c>ILocationResolver</c> can find it the same way
    /// it does at runtime.
    /// </summary>
    private static Location NewLocation(string name, LocationType type, bool isSystem = true) => new()
    {
        LocationName = name,
        LocationType = type,
        Address = "Test Address",
        IsActive = true,
        Status = "Active",
        IsSystemLocation = isSystem
    };
}
