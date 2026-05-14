namespace DuckRun.Core;

/// <summary>
/// Ambient accessor for the currently-running job's console. Useful when DI injection is awkward
/// (deeply-nested static utility code, legacy call paths). Prefer constructor injection of
/// <see cref="IDuckRunConsole"/> when possible.
/// </summary>
public static class DuckRunConsole
{
    private static readonly AsyncLocal<IDuckRunConsole?> _current = new();

    /// <summary>The console for the job currently executing on this async flow, or null outside a job run.</summary>
    public static IDuckRunConsole? Current => _current.Value;

    internal static void SetCurrent(IDuckRunConsole? console) => _current.Value = console;
}
