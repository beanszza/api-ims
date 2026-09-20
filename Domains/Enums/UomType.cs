namespace Domains.Enums;

/// <summary>
/// Physical dimension a unit of measure belongs to. Conversion is only ever legal within one
/// dimension: kilograms convert to grams, never to litres.
/// </summary>
public enum UomType
{
    /// <summary>Mass. Base unit: kilogram.</summary>
    [DbValue("Weight")]
    Weight = 1,

    /// <summary>Capacity. Base unit: litre.</summary>
    [DbValue("Volume")]
    Volume = 2,

    /// <summary>
    /// Discrete items. Base unit: piece.
    /// </summary>
    /// <remarks>
    /// Container-style units (box, pack, roll, bottle) are all Count with a factor of 1, because how
    /// many pieces are in a box is a property of a specific supplier's packaging, not of the unit
    /// itself. That belongs on <c>SupplierItem.PackSize</c> in Task 12.
    /// </remarks>
    [DbValue("Count")]
    Count = 3,

    /// <summary>Distance. Base unit: metre. Used by packaging materials sold by length.</summary>
    [DbValue("Length")]
    Length = 4
}
