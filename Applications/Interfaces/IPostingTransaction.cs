namespace Applications.Interfaces;

/// <summary>
/// Runs a stock-affecting operation as one all-or-nothing unit of work.
/// </summary>
/// <remarks>
/// Before this existed, a single receipt or batch start called <c>SaveChangesAsync</c> two to four
/// times with no transaction around them. A failure between those calls left the database in a state
/// no screen could explain: stock added but the order not marked received, or ingredients deducted for
/// a batch that never started. Wrapping the whole operation removes that class of outcome entirely.
/// </remarks>
public interface IPostingTransaction
{
    /// <summary>Runs <paramref name="operation"/> in a transaction and returns its result.</summary>
    Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default);

    /// <summary>Runs <paramref name="operation"/> in a transaction.</summary>
    Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken = default);
}
