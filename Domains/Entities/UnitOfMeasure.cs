using Domains.Enums;

namespace Domains.Entities;

/// <summary>
/// A unit quantities can be expressed in, plus the information needed to convert between units.
/// </summary>
/// <remarks>
/// Before this existed as a convertible unit, <c>RecipeIngredient.UomId</c> and <c>Item.UomId</c> were
/// both recorded and then ignored: an ingredient written as 10 000 g was subtracted from a balance
/// held in kilograms unit for unit, a thousand-fold error the model had no way to notice.
/// </remarks>
public class UnitOfMeasure
{
    public int UomId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Abbreviation { get; set; } = string.Empty;

    /// <summary>Stable machine-friendly key, unique across units. Safe to reference from code and seeds.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Dimension this unit measures. Conversion across dimensions is refused.</summary>
    public UomType UomType { get; set; } = UomType.Count;

    /// <summary>
    /// How many of the dimension's base unit make up one of this unit.
    /// </summary>
    /// <remarks>
    /// Bases are kilogram, litre, piece and metre, each with a factor of 1. So gram is 0.001,
    /// millilitre is 0.001, and a 50 kg sack is 50. Converting is
    /// <c>target = source * sourceFactor / targetFactor</c>.
    /// </remarks>
    public decimal ConversionFactor { get; set; } = 1m;

    /// <summary>True when this unit is the base for its dimension.</summary>
    public bool IsBaseUnit { get; set; }

    /// <summary>
    /// The base unit of this unit's dimension. Null on base units themselves.
    /// </summary>
    public int? BaseUomId { get; set; }

    public UnitOfMeasure? BaseUom { get; set; }

    public ICollection<Item> Items { get; set; } = new List<Item>();
    public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = new List<RecipeIngredient>();
}
