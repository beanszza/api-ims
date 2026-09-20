using Applications.Interfaces;
using Infrastructures.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Applications.Services;

/// <summary>
/// Wraps an operation in a database transaction with a single commit point.
/// </summary>
public sealed class PostingTransaction : IPostingTransaction
{
    private readonly ScmDbContext _context;

    public PostingTransaction(ScmDbContext context) => _context = context;

    public async Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken = default)
        => await ExecuteAsync<object?>(async () =>
        {
            await operation();
            return null;
        }, cancellationToken);

    public async Task<T> ExecuteAsync<T>(
        Func<Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        // The in-memory provider used for local development has no transaction support. Running the
        // operation directly keeps development working; correctness there is not the point.
        if (!_context.Database.IsRelational())
        {
            return await RunAndSaveAsync(operation, cancellationToken);
        }

        // Already inside someone else's transaction: join it rather than nesting, so the outermost
        // caller keeps control of the commit.
        if (_context.Database.CurrentTransaction is not null)
        {
            return await operation();
        }

        // The context is configured with EnableRetryOnFailure. A retrying execution strategy refuses
        // user-initiated transactions unless the whole transaction is inside the strategy's delegate,
        // so the BeginTransaction has to live in here rather than around it.
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            var result = await RunAndSaveAsync(operation, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return result;

            // No explicit rollback: disposing an uncommitted transaction rolls it back, and that path
            // is reached by any exception escaping the operation.
        });
    }

    private async Task<T> RunAndSaveAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        var result = await operation();

        // Flush anything the operation staged but did not save itself. Existing services still call
        // SaveChangesAsync internally; this makes the final state consistent either way.
        await _context.SaveChangesAsync(cancellationToken);
        return result;
    }
}
