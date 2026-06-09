using DuckRun.Core.Reporting;

namespace DuckRun.Tests;

public class DsnTests
{

    [Fact]
    public void Parse_FullHttpsDsn_PopulatesAllParts()
    {
        var id = Guid.NewGuid();
        var dsn = Dsn.Parse($"https://pubkey123@dashboard.example.com:8091/{id}");
        Assert.Equal("https", dsn.Scheme);
        Assert.Equal("dashboard.example.com", dsn.Host);
        Assert.Equal(8091, dsn.Port);
        Assert.Equal("pubkey123", dsn.PublicKey);
        Assert.Equal(id, dsn.ProjectId);
        Assert.Equal("https://dashboard.example.com:8091", dsn.EndpointUrl);
    }

    [Fact]
    public void Parse_NoExplicitPort_DefaultsByScheme()
    {
        var id = Guid.NewGuid();
        Assert.Equal(443, Dsn.Parse($"https://k@host/{id}").Port);
        Assert.Equal(80, Dsn.Parse($"http://k@host/{id}").Port);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_EmptyOrWhitespace_Throws(string raw) => Assert.Throws<ArgumentException>(() => Dsn.Parse(raw));

    [Fact]
    public void Parse_NonHttpScheme_Throws() => Assert.Throws<FormatException>(() => Dsn.Parse($"ftp://k@host/{Guid.NewGuid()}"));

    [Fact]
    public void Parse_MissingPublicKey_Throws() => Assert.Throws<FormatException>(() => Dsn.Parse($"https://host/{Guid.NewGuid()}"));

    [Fact]
    public void Parse_MissingProjectId_Throws() => Assert.Throws<FormatException>(() => Dsn.Parse("https://k@host"));

    [Fact]
    public void Parse_NonGuidProjectId_Throws() => Assert.Throws<FormatException>(() => Dsn.Parse("https://k@host/not-a-guid"));
}
