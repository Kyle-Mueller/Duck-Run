using System.Collections.Concurrent;
using System.Reflection;
using DuckRun.Core.Logging;
using DuckRun.Core.Reporting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DuckRun.Core.Runs;

internal sealed class JobExecutor(IServiceScopeFactory scopes, IJobRunStore runs, IDashboardReporter reporter, ILogger<JobExecutor> logger)
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _live = new();

    public IReadOnlyCollection<Guid> InFlight => _live.Keys.ToArray();

    public void RequestCancel(Guid runId)
    {
        if (_live.TryGetValue(runId, out var cts))
        {
            try { cts.Cancel(); }
            catch (ObjectDisposedException) { /* already finished */ }
        }
    }

    public async Task<JobRun> CreateAndStoreRunAsync(JobDescriptor job, string triggerSource, CancellationToken ct)
    {
        var run = new JobRun
        {
            JobName = job.Name,
            State = JobRunState.Pending,
            TriggerSource = triggerSource,
        };
        await runs.AddAsync(run, ct);
        reporter.ReportRun(run);
        return run;
    }

    public async Task<JobRun> ExecuteAsync(JobDescriptor job, string triggerSource, CancellationToken outerCt)
    {
        var run = await CreateAndStoreRunAsync(job, triggerSource, CancellationToken.None);
        await ContinueRunAsync(run, job, outerCt);
        return run;
    }

    public async Task ContinueRunAsync(JobRun run, JobDescriptor job, CancellationToken outerCt)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;

        var console = (ScopedDuckRunConsole)sp.GetRequiredService<IDuckRunConsole>();
        console.RunId = run.Id;
        DuckRunConsole.SetCurrent(console);

        using var timeoutCts = job.Timeout is { } t ? new CancellationTokenSource(t) : null;
        using var linkedCts = timeoutCts is null ? CancellationTokenSource.CreateLinkedTokenSource(outerCt)
                                                 : CancellationTokenSource.CreateLinkedTokenSource(outerCt, timeoutCts.Token);

        _live[run.Id] = linkedCts;

        try
        {
            object? target = null;
            if (!job.Method.IsStatic)
            {
                try { target = ActivatorUtilities.CreateInstance(sp, job.DeclaringType); }
                catch (Exception ex)
                {
                    run.State = JobRunState.Failed;
                    run.ErrorMessage = $"Could not construct {job.DeclaringType.FullName}: {ex.Message}";
                    run.ErrorStackTrace = ex.ToString();
                    run.StartedAt = DateTimeOffset.UtcNow;
                    run.FinishedAt = run.StartedAt;
                    await runs.UpdateAsync(run, CancellationToken.None);
                    reporter.ReportRun(run);
                    return;
                }
            }

            var args = BuildArguments(sp, job.Method, linkedCts.Token);

            run.StartedAt = DateTimeOffset.UtcNow;
            run.State = JobRunState.Running;
            await runs.UpdateAsync(run, CancellationToken.None);
            reporter.ReportRun(run);

            try
            {
                var result = job.Method.Invoke(target, args);

                if (result is Task task) await task.ConfigureAwait(false);
                else if (result is ValueTask valueTask) await valueTask.ConfigureAwait(false);

                run.State = JobRunState.Succeeded;
            }
            catch (TargetInvocationException tex)
            {
                var inner = tex.InnerException ?? tex;
                AssignFailure(run, inner, timeoutCts);
            }
            catch (Exception ex)
            {
                AssignFailure(run, ex, timeoutCts);
            }
        }
        finally
        {
            run.FinishedAt = DateTimeOffset.UtcNow;
            await runs.UpdateAsync(run, CancellationToken.None);
            reporter.ReportRun(run);
            DuckRunConsole.SetCurrent(null);
            _live.TryRemove(run.Id, out _);
        }

        if (run.State == JobRunState.Failed) logger.LogError("DuckRun job '{Job}' run {Run} failed: {Error}", job.Name, run.Id, run.ErrorMessage);
    }

    private static void AssignFailure(JobRun run, Exception ex, CancellationTokenSource? timeoutCts)
    {
        if (ex is OperationCanceledException)
        {
            run.State = timeoutCts?.IsCancellationRequested == true ? JobRunState.TimedOut
                                                                    : JobRunState.Cancelled;
            return;
        }
        run.State = JobRunState.Failed;
        run.ErrorMessage = ex.Message;
        run.ErrorStackTrace = ex.ToString();
    }

    private static object?[] BuildArguments(IServiceProvider sp, MethodInfo method, CancellationToken ct)
    {
        var parameters = method.GetParameters();
        if (parameters.Length == 0) return Array.Empty<object?>();

        var args = new object?[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            var p = parameters[i];
            if (p.ParameterType == typeof(CancellationToken))
            {
                args[i] = ct;
                continue;
            }

            var resolved = sp.GetService(p.ParameterType);
            if (resolved is null && !p.HasDefaultValue)
            {
                throw new InvalidOperationException($"Cannot resolve parameter '{p.Name}' of type {p.ParameterType.Name} for " +
                                                    $"{method.DeclaringType?.FullName}.{method.Name}. Either register the service in DI " +
                                                    $"or give the parameter a default value.");
            }
            args[i] = resolved ?? p.DefaultValue;
        }
        return args;
    }
}
