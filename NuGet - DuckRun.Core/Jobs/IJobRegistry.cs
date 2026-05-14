namespace DuckRun.Core.Jobs;

internal interface IJobRegistry
{
    IReadOnlyList<JobDescriptor> All { get; }
    JobDescriptor? FindByName(string name);
}
