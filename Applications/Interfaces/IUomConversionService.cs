namespace Applications.Interfaces;

/// <summary>
/// Converts quantities between units of measure. The only sanctioned way to move a number from one
/// unit into another.
/// </summary>
public interface IUomConversionService
{
    /// <summary>
    /// Converts <paramref name="quantity"/> from one unit to another.
    /// </summary>
    /// <exception cref="Domains.Exceptions.UomConversionException">
    /// Either unit is unknown, or they measure different dimensions.
    /// </exception>
    Task<decimal> ConvertAsync(decimal quantity, int fromUomId, int toUomId);

    /// <summary>
    /// Converts and rounds to the 3 decimal places stock quantities are stored with, so the caller
    /// cannot accidentally persist more precision than the column holds.
    /// </summary>
    Task<decimal> ConvertForStockAsync(decimal quantity, int fromUomId, int toUomId);

    /// <summary>
    /// True when a conversion between the two units is possible. Use this for validation, where an
    /// exception would be the wrong shape of answer.
    /// </summary>
    Task<bool> CanConvertAsync(int fromUomId, int toUomId);

    /// <summary>
    /// Converts an ingredient quantity into the stocking unit of the item it draws from.
    /// </summary>
    /// <remarks>
    /// The specific case behind defect 5: a recipe expressed in grams drawing on an item held in
    /// kilograms.
    /// </remarks>
    Task<decimal> ConvertToItemStockUomAsync(decimal quantity, int fromUomId, int itemId);
}
