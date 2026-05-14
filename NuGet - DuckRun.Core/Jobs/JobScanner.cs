using System.Reflection;
using Cronos;

namespace DuckRun.Core.Jobs;

internal static class JobScanner
{
    public static IJobRegistry Build(IEnumerable<Assembly> assembliesToScan, IEnumerable<JobDescriptor> explicitJobs)
    {
        var descriptors = new List<JobDescriptor>(explicitJobs);

        foreach (var assembly in assembliesToScan)
        {
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t is not null).Cast<Type>().ToArray(); }

            foreach (var type in types)
            {
                if (type.IsAbstract || type.IsInterface) continue;

                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                foreach (var method in methods)
                {
                    var attr = method.GetCustomAttribute<DuckRunJobAttribute>();
                    if (attr is null) continue;

                    ValidateCron(attr.Cron, type, method);

                    descriptors.Add(new JobDescriptor
                    {
                        Name = attr.Name,
                        Cron = attr.Cron,
                        DeclaringType = type,
                        Method = method,
                        MaxConcurrency = attr.MaxConcurrency <= 0 ? int.MaxValue : attr.MaxConcurrency,
                        Timeout = attr.TimeoutSeconds > 0 ? TimeSpan.FromSeconds(attr.TimeoutSeconds) : null,
                        AllowManualTrigger = attr.AllowManualTrigger,
                        Enabled = attr.Enabled,
                    });
                }
            }
        }

        return new JobRegistry(descriptors);
    }

    private static void ValidateCron(string expression, Type type, MethodInfo method)
    {
        try
        {
            var fieldCount = expression.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
            var format = fieldCount == 6 ? CronFormat.IncludeSeconds : CronFormat.Standard;
            CronExpression.Parse(expression, format);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Invalid cron expression '{expression}' on {type.FullName}.{method.Name}: {ex.Message}", ex);
        }
    }
}
