using DuckRun.Core;
using DuckRun.Core.Runs;

namespace DuckRun.Tests;

public class InMemoryJobRunStoreTests
{

    private static JobRun Run(string job, JobRunState state = JobRunState.Pending) => new() { JobName = job, State = state };

    [Fact]
    public async Task AddThenGet_ReturnsSameRun()
    {
        var store = new InMemoryJobRunStore(maxPerJob: 10);
        var run = Run("job-a");
        await store.AddAsync(run, default);
        Assert.Same(run, await store.GetAsync(run.Id, default));
    }

    [Fact]
    public async Task Get_UnknownId_ReturnsNull() => Assert.Null(await new InMemoryJobRunStore(10).GetAsync(Guid.NewGuid(), default));

    [Fact]
    public async Task RingBuffer_EvictsOldestBeyondCap()
    {
        var store = new InMemoryJobRunStore(maxPerJob: 2);
        var first = Run("job-a");
        var second = Run("job-a");
        var third = Run("job-a");
        await store.AddAsync(first, default);
        await store.AddAsync(second, default);
        await store.AddAsync(third, default);

        Assert.Null(await store.GetAsync(first.Id, default));
        Assert.NotNull(await store.GetAsync(second.Id, default));
        Assert.NotNull(await store.GetAsync(third.Id, default));
    }

    [Fact]
    public async Task GetRecentForJob_ReturnsNewestFirst()
    {
        var store = new InMemoryJobRunStore(maxPerJob: 10);
        var older = Run("job-a");
        var newer = Run("job-a");
        await store.AddAsync(older, default);
        await store.AddAsync(newer, default);

        var recent = await store.GetRecentForJobAsync("job-a", 10, default);
        Guid[] expected = [newer.Id, older.Id];
        Guid[] actual = [.. recent.Select(r => r.Id)];
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task CountInFlight_CountsOnlyRunning()
    {
        var store = new InMemoryJobRunStore(maxPerJob: 10);
        await store.AddAsync(Run("job-a", JobRunState.Running), default);
        await store.AddAsync(Run("job-a", JobRunState.Running), default);
        await store.AddAsync(Run("job-a", JobRunState.Succeeded), default);

        Assert.Equal(2, await store.CountInFlightAsync("job-a", default));
    }
}
