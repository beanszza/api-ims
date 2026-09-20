using Applications.Interfaces;
using Domains.Entities;
using Domains.Enums;
using Domains.Exceptions;
using Infrastructures.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Applications.Services;

/// <summary>
/// Factor-based unit conversion, scoped per request.
/// </summary>
/// <remarks>
/// Units are reference data that changes rarely, so they are cached for the lifetime of the scope to
/// keep a recipe with a dozen ingredients from issuing a dozen round trips.
/// </remarks>
public sealed class UomConversionService : IUomConversionService
{
    /// <summary>Matches the scale of the quantity columns, numeric(18,3).</summary>
    private const int StockScale = 3;

    private readonly ScmDbContext _context;
    private readonly Dictionary<int, UnitOfMeasure> _cache = [];

    public UomConversionService(ScmDbContext context) => _context = context;

    public async Task<decimal> ConvertAsync(decimal quantity, int fromUomId, int toUomId)
    {
        if (fromUomId == toUomId)
        {
            return quantity;
        }

        var from = await LoadAsync(fromUomId);
        var to = await LoadAsync(toUomId);

        if (from.UomType != to.UomType)
        {
            throw UomConversionException.CrossDimension(
                Describe(from), EnumDbValue.ToDbValue(from.UomType),
                Describe(to), EnumDbValue.ToDbValue(to.UomType));
        }

        if (from.ConversionFactor <= 0)
        {
            throw UomConversionException.InvalidFactor(Describe(from), from.ConversionFactor);
        }

        if (to.ConversionFactor <= 0)
        {
            throw UomConversionException.InvalidFactor(Describe(to), to.ConversionFactor);
        }

        // Via the dimension's base unit: grams -> kilograms is 500 * 0.001 / 1 = 0.5.
        return quantity * from.ConversionFactor / to.ConversionFactor;
    }

    public async Task<decimal> ConvertForStockAsync(decimal quantity, int fromUomId, int toUomId)
        => Math.Round(await ConvertAsync(quantity, fromUomId, toUomId), StockScale, MidpointRounding.AwayFromZero);

    public async Task<bool> CanConvertAsync(int fromUomId, int toUomId)
    {
        if (fromUomId == toUomId)
        {
            return true;
        }

        var from = await FindAsync(fromUomId);
        var to = await FindAsync(toUomId);

        return from is not null
            && to is not null
            && from.UomType == to.UomType
            && from.ConversionFactor > 0
            && to.ConversionFactor > 0;
    }

    public async Task<decimal> ConvertToItemStockUomAsync(decimal quantity, int fromUomId, int itemId)
    {
        var stockUomId = await _context.Items
            .Where(i => i.ItemId == itemId)
            .Select(i => i.StockUomId)
            .FirstOrDefaultAsync();

        if (stockUomId == 0)
        {
            throw new InvalidOperationException(
                $"Item {itemId} has no stocking unit of measure, so quantities cannot be converted for it.");
        }

        return await ConvertForStockAsync(quantity, fromUomId, stockUomId);
    }

    private async Task<UnitOfMeasure> LoadAsync(int uomId)
        => await FindAsync(uomId) ?? throw UomConversionException.UnknownUnit(uomId);

    private async Task<UnitOfMeasure?> FindAsync(int uomId)
    {
        if (_cache.TryGetValue(uomId, out var cached))
        {
            return cached;
        }

        var uom = await _context.UnitOfMeasures
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UomId == uomId);

        if (uom is not null)
        {
            _cache[uomId] = uom;
        }

        return uom;
    }

    private static string Describe(UnitOfMeasure uom)
        => string.IsNullOrWhiteSpace(uom.Abbreviation) ? uom.Name : uom.Abbreviation;
}
