namespace DuckRun.Core.Internal.Discovery;

internal sealed class JobRegistry : IJobRegistry
{
    private readonly Dictionary<string, JobDescriptor> _byName;
    private readonly IReadOnlyList<JobDescriptor> _all;

    public JobRegistry(IEnumerable<JobDescriptor> descriptors)
    {
        _byName = new Dictionary<string, JobDescriptor>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in descriptors)
        {
            if (!_byName.TryAdd(d.Name, d)) throw new InvalidOperationException($"Duplicate DuckRun job name '{d.Name}'. Already registered for " +
                                                                                $"{_byName[d.Name].DeclaringType.FullName}.{_byName[d.Name].Method.Name}, " +
                                                                                $"now also seen on {d.DeclaringType.FullName}.{d.Method.Name}.");
        }
        _all = [.. _byName.Values];
    }

    public IReadOnlyList<JobDescriptor> All => _all;
    public JobDescriptor? FindByName(string name) => _byName.TryGetValue(name, out var d) ? d : null;
}
