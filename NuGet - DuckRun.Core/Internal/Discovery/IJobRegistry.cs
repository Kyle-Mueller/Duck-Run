namespace DuckRun.Core.Internal.Discovery;

internal interface IJobRegistry
{
    IReadOnlyList<JobDescriptor> All { get; }
    JobDescriptor? FindByName(string name);
}
