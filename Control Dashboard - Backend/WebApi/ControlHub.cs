using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DuckRun.Dashboard.WebApi;

/// <summary>
/// SignalR hub for the React SPA. Browsers join a group per project they're viewing,
/// and the dashboard pushes "RunUpdated" / "LogAppended" events as ingest writes happen.
/// </summary>
[Authorize]
internal sealed class ControlHub : Hub
{
    public Task SubscribeProject(string projectId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(projectId));

    public Task UnsubscribeProject(string projectId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupFor(projectId));

    public static string GroupFor(string projectId) => $"project:{projectId}";
    public static string GroupFor(Guid projectId) => $"project:{projectId}";
}
