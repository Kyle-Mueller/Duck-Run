namespace DuckRun.Core.Reporting;

/// <summary>
/// Parsed form of a DuckRun DSN. Format: <c>{scheme}://{publicKey}@{host}[:{port}]/{projectId}</c>.
/// </summary>
internal sealed record Dsn(string Scheme, string Host, int Port, string PublicKey, Guid ProjectId)
{
    public string EndpointUrl => $"{Scheme}://{Host}:{Port}";

    public static Dsn Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new ArgumentException("DSN is empty.", nameof(raw));

        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
            throw new FormatException($"DSN is not a valid URI: '{raw}'.");

        if (uri.Scheme != "http" && uri.Scheme != "https")
            throw new FormatException($"DSN scheme must be http or https; got '{uri.Scheme}'.");

        if (string.IsNullOrWhiteSpace(uri.UserInfo))
            throw new FormatException("DSN must include the public key as the user-info part ('{key}@host').");

        var path = uri.AbsolutePath.Trim('/');
        if (string.IsNullOrWhiteSpace(path))
            throw new FormatException("DSN must include the project id as the path component.");

        if (!Guid.TryParse(path, out var projectId))
            throw new FormatException($"DSN project id '{path}' is not a valid GUID.");

        return new Dsn(
            Scheme: uri.Scheme,
            Host: uri.Host,
            Port: uri.IsDefaultPort ? (uri.Scheme == "https" ? 443 : 80) : uri.Port,
            PublicKey: uri.UserInfo,
            ProjectId: projectId);
    }
}
