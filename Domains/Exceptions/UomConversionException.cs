namespace Domains.Exceptions;

/// <summary>
/// Thrown when a quantity cannot be converted between two units.
/// </summary>
/// <remarks>
/// Deliberately an exception rather than a silent fallback. The bug this whole task exists to fix was
/// caused by quietly treating one unit as another, so an impossible conversion must stop the
/// operation instead of producing a plausible-looking wrong number.
/// </remarks>
public sealed class UomConversionException : Exception
{
    private UomConversionException(string message) : base(message)
    {
    }

    public static UomConversionException CrossDimension(
        string fromUnit, string fromType, string toUnit, string toType)
        => new($"Cannot convert {fromUnit} ({fromType}) to {toUnit} ({toType}): " +
               "the units measure different things.");

    public static UomConversionException UnknownUnit(int uomId)
        => new($"Unit of measure {uomId} was not found.");

    public static UomConversionException InvalidFactor(string unit, decimal factor)
        => new($"Unit {unit} has a conversion factor of {factor}. Factors must be greater than zero.");
}
