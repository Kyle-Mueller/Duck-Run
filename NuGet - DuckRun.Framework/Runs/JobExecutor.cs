using System.Collections.Concurrent;
using System.Reflection;
using DuckRun.Core;
using DuckRun.Core.Logging;
using DuckRun.Core.Reporting;
using DuckRun.Core.Runs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DuckRun.Framework.Runs;

/// <summary>
/// net48-flavoured job executor. No DI scope: job instances are constructed via <see cref="DuckRunOptions.JobFactory"/>
/// and method parameters are limited to <c>CancellationToken</c> + <c>IDuckRunConsole</c> + defaulted values.
/// </summary>
internal sealed class JobExecutor(IJobRunStore runs, IConsoleStore consoleStore, IDashboardReporter reporter, Func<Type, object> jobFactory, ILogger? logger = null)
{

    private readonly ILogger _logger = logger ?? NullLogger.Instance;
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _live = new();

    public IReadOnlyCollection<Guid> InFlight => _live.Keys.ToArray();

    public void RequestCancel(Guid runId)
    {
        if (_live.TryGetValue(runId, out var cts))
        {
            try { cts.Cancel(); } catch (ObjectDisposedException) { }
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
        var console = new ScopedDuckRunConsole(consoleStore, reporter) { RunId = run.Id };
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
                try { target = jobFactory(job.DeclaringType); }
                catch (Exception ex)
                {
                    FailEarly(run, $"Could not construct {job.DeclaringType.FullName}: {ex.Message}", ex.ToString());
                    await runs.UpdateAsync(run, CancellationToken.None);
                    reporter.ReportRun(run);
                    return;
                }
            }

            object?[] args;
            try { args = BuildArguments(job.Method, console, linkedCts.Token); }
            catch (Exception ex)
            {
                FailEarly(run, ex.Message, ex.ToString());
                await runs.UpdateAsync(run, CancellationToken.None);
                reporter.ReportRun(run);
                return;
            }

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
            catch (TargetInvocationException tex) { AssignFailure(run, tex.InnerException ?? tex, timeoutCts); }
            catch (Exception ex) { AssignFailure(run, ex, timeoutCts); }
        }
        finally
        {
            run.FinishedAt = DateTimeOffset.UtcNow;
            await runs.UpdateAsync(run, CancellationToken.None);
            reporter.ReportRun(run);
            DuckRunConsole.SetCurrent(null);
            _live.TryRemove(run.Id, out _);
        }

        if (run.State == JobRunState.Failed) _logger.LogError("DuckRun.Framework job '{Job}' run {Run} failed: {Error}", job.Name, run.Id, run.ErrorMessage);
    }

    private static void FailEarly(JobRun run, string message, string stack)
    {
        run.State = JobRunState.Failed;
        run.ErrorMessage = message;
        run.ErrorStackTrace = stack;
        run.StartedAt = DateTimeOffset.UtcNow;
        run.FinishedAt = run.StartedAt;
    }

    private static void AssignFailure(JobRun run, Exception ex, CancellationTokenSource? timeoutCts)
    {
        if (ex is OperationCanceledException)
        {
            run.State = timeoutCts?.IsCancellationRequested == true ? JobRunState.TimedOut : JobRunState.Cancelled;
            return;
        }
        run.State = JobRunState.Failed;
        run.ErrorMessage = ex.Message;
        run.ErrorStackTrace = ex.ToString();
    }

    private static object?[] BuildArguments(MethodInfo method, IDuckRunConsole console, CancellationToken ct)
    {
        var parameters = method.GetParameters();
        if (parameters.Length == 0) return Array.Empty<object?>();

        var args = new object?[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            var p = parameters[i];
            if (p.ParameterType == typeof(CancellationToken)) { args[i] = ct; continue; }
            if (p.ParameterType == typeof(IDuckRunConsole)) { args[i] = console; continue; }
            if (p.HasDefaultValue) { args[i] = p.DefaultValue; continue; }
            throw new InvalidOperationException($"DuckRun.Framework can't resolve method parameter '{p.Name}' of type {p.ParameterType.Name} on " +
                                                $"{method.DeclaringType?.FullName}.{method.Name}. On net48 only CancellationToken and IDuckRunConsole " +
                                                $"are auto-injected. Use constructor injection on the job class via UseJobFactory(...) for other dependencies.");
        }
        return args;
    }
}
