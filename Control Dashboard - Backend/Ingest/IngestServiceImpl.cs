using DuckRun.Dashboard.Database;
using DuckRun.Protocol.V1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;

namespace DuckRun.Dashboard.Ingest;

internal sealed class IngestServiceImpl(
    IDbContextFactory<DashboardDbContext> contextFactory,
    ILogger<IngestServiceImpl> logger)
    : IngestService.IngestServiceBase
{
    private const string AcceptedProtocolVersion = "v1";

    public override async Task<HandshakeResponse> Handshake(HandshakeRequest request, ServerCallContext context)
    {
        var projectId = ResolveProject(context);

        await using var ctx = await contextFactory.CreateDbContextAsync(context.CancellationToken);

        var now = DateTime.UtcNow;
        await UpsertHeartbeatAsync(ctx, projectId, request.NodeId, request.Runtime, request.ClientVersion,
            startedAt: request.StartedAt?.ToDateTime() ?? now, isLeader: false, lastSeen: now,
            context.CancellationToken);

        foreach (var def in request.Jobs)
            await UpsertJobDefinitionAsync(ctx, projectId, def, now, context.CancellationToken);

        await ctx.SaveChangesAsync(context.CancellationToken);

        logger.LogInformation("Handshake from node {Node} of project {Project} with {Jobs} job(s).",
            request.NodeId, projectId, request.Jobs.Count);

        return new HandshakeResponse
        {
            AcceptedProtocolVersion = AcceptedProtocolVersion,
            ProjectId = projectId.ToString(),
        };
    }

    public override async Task<Ack> SendHeartbeat(HeartbeatRequest request, ServerCallContext context)
    {
        var projectId = ResolveProject(context);
        await using var ctx = await contextFactory.CreateDbContextAsync(context.CancellationToken);

        var seen = request.At?.ToDateTime() ?? DateTime.UtcNow;
        await UpsertHeartbeatAsync(ctx, projectId, request.NodeId, runtime: null, clientVersion: null,
            startedAt: null, isLeader: request.IsLeader, lastSeen: seen, context.CancellationToken);

        await ctx.SaveChangesAsync(context.CancellationToken);
        return new Ack { Ok = true };
    }

    public override async Task<Ack> SendRuns(RunsBatch request, ServerCallContext context)
    {
        var projectId = ResolveProject(context);
        await using var ctx = await contextFactory.CreateDbContextAsync(context.CancellationToken);

        var now = DateTime.UtcNow;
        foreach (var r in request.Runs)
        {
            if (!Guid.TryParse(r.Id, out var runId)) continue;

            var existing = await ctx.JobRuns.FindAsync([runId], context.CancellationToken);
            if (existing is null)
            {
                ctx.JobRuns.Add(new JobRun
                {
                    Id = runId,
                    ProjectId = projectId,
                    JobName = r.JobName,
                    NodeId = r.NodeId,
                    CreatedAt = r.CreatedAt?.ToDateTime() ?? now,
                    StartedAt = r.StartedAt?.ToDateTime(),
                    FinishedAt = r.FinishedAt?.ToDateTime(),
                    State = r.State,
                    TriggerSource = r.TriggerSource,
                    ErrorMessage = NullIfEmpty(r.ErrorMessage),
                    ErrorStackTrace = NullIfEmpty(r.ErrorStackTrace),
                    ReceivedAt = now,
                });
            }
            else
            {
                existing.JobName = r.JobName;
                existing.NodeId = r.NodeId;
                existing.StartedAt = r.StartedAt?.ToDateTime() ?? existing.StartedAt;
                existing.FinishedAt = r.FinishedAt?.ToDateTime() ?? existing.FinishedAt;
                existing.State = r.State;
                existing.TriggerSource = r.TriggerSource;
                existing.ErrorMessage = NullIfEmpty(r.ErrorMessage) ?? existing.ErrorMessage;
                existing.ErrorStackTrace = NullIfEmpty(r.ErrorStackTrace) ?? existing.ErrorStackTrace;
                existing.ReceivedAt = now;
            }
        }

        await ctx.SaveChangesAsync(context.CancellationToken);
        return new Ack { Ok = true };
    }

    public override async Task<Ack> SendLogs(LogsBatch request, ServerCallContext context)
    {
        var projectId = ResolveProject(context);
        await using var ctx = await contextFactory.CreateDbContextAsync(context.CancellationToken);

        foreach (var entry in request.Logs)
        {
            if (!Guid.TryParse(entry.RunId, out var runId)) continue;
            ctx.ConsoleLogs.Add(new ConsoleLog
            {
                ProjectId = projectId,
                RunId = runId,
                Timestamp = entry.Timestamp?.ToDateTime() ?? DateTime.UtcNow,
                Level = entry.Level,
                Message = entry.Message,
            });
        }

        await ctx.SaveChangesAsync(context.CancellationToken);
        return new Ack { Ok = true };
    }

    private static async Task UpsertHeartbeatAsync(DashboardDbContext ctx, Guid projectId, string nodeId,
        string? runtime, string? clientVersion, DateTime? startedAt, bool isLeader, DateTime lastSeen,
        CancellationToken ct)
    {
        var existing = await ctx.NodeHeartbeats
            .SingleOrDefaultAsync(h => h.ProjectId == projectId && h.NodeId == nodeId, ct);

        if (existing is null)
        {
            ctx.NodeHeartbeats.Add(new NodeHeartbeat
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                NodeId = nodeId,
                StartedAt = startedAt ?? lastSeen,
                LastSeen = lastSeen,
                IsLeader = isLeader,
                Runtime = runtime ?? "",
                ClientVersion = clientVersion ?? "",
            });
        }
        else
        {
            existing.LastSeen = lastSeen;
            existing.IsLeader = isLeader;
            if (!string.IsNullOrWhiteSpace(runtime)) existing.Runtime = runtime;
            if (!string.IsNullOrWhiteSpace(clientVersion)) existing.ClientVersion = clientVersion;
            if (startedAt is { } s) existing.StartedAt = s;
        }
    }

    private static async Task UpsertJobDefinitionAsync(DashboardDbContext ctx, Guid projectId,
        DuckRun.Protocol.V1.JobDefinition def, DateTime now, CancellationToken ct)
    {
        var existing = await ctx.JobDefinitions
            .SingleOrDefaultAsync(j => j.ProjectId == projectId && j.Name == def.Name, ct);

        if (existing is null)
        {
            ctx.JobDefinitions.Add(new Database.JobDefinition
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Name = def.Name,
                Cron = def.Cron,
                MaxConcurrency = def.MaxConcurrency,
                TimeoutSeconds = def.TimeoutSeconds,
                AllowManualTrigger = def.AllowManualTrigger,
                Enabled = def.Enabled,
                FirstSeen = now,
                LastSeen = now,
            });
        }
        else
        {
            existing.Cron = def.Cron;
            existing.MaxConcurrency = def.MaxConcurrency;
            existing.TimeoutSeconds = def.TimeoutSeconds;
            existing.AllowManualTrigger = def.AllowManualTrigger;
            existing.Enabled = def.Enabled;
            existing.LastSeen = now;
        }
    }

    private static Guid ResolveProject(ServerCallContext context)
    {
        if (context.UserState.TryGetValue(ApiKeyAuthInterceptor.ProjectIdKey, out var raw) && raw is Guid projectId)
            return projectId;
        throw new RpcException(new Status(StatusCode.Internal, "Project context missing — interceptor did not run."));
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrEmpty(s) ? null : s;
}
