namespace Applications.Interfaces;

/// <summary>
/// Single authority on which document status changes are legal. Every service that mutates a status
/// goes through this, so an invalid lifecycle jump fails the same way everywhere.
/// </summary>
public interface IStatusTransitionGuard
{
    /// <summary>True when moving from <paramref name="from"/> to <paramref name="to"/> is allowed.</summary>
    bool CanTransition<TEnum>(TEnum from, TEnum to) where TEnum : struct, Enum;

    /// <summary>
    /// Throws <see cref="Domains.Exceptions.InvalidStatusTransitionException"/> unless the move is allowed.
    /// </summary>
    void EnsureCanTransition<TEnum>(TEnum from, TEnum to) where TEnum : struct, Enum;

    /// <summary>States reachable from <paramref name="from"/>. Empty means a final state.</summary>
    IReadOnlyCollection<TEnum> AllowedFrom<TEnum>(TEnum from) where TEnum : struct, Enum;

    /// <summary>True when no further transition is possible.</summary>
    bool IsFinal<TEnum>(TEnum status) where TEnum : struct, Enum;
}
