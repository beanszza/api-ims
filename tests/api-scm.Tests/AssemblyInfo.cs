// The suite shares one PostgreSQL database and truncates it between tests, so tests must never run
// in parallel. Relying on [Collection] inheritance from DatabaseTestBase is not enough: xUnit does
// not reliably serialise derived classes through a base-class collection attribute, which showed up
// as one test class truncating another's data mid-run.
[assembly: CollectionBehavior(DisableTestParallelization = true, MaxParallelThreads = 1)]
