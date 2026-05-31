using DuckRun.Core;

namespace DuckRun.Tests;

public class DuckRunJobAttributeTests
{

    [Fact]
    public void Ctor_StoresNameAndCron_WithDefaults()
    {
        var attr = new DuckRunJobAttribute("nightly", "0 0 * * *");
        Assert.Equal("nightly", attr.Name);
        Assert.Equal("0 0 * * *", attr.Cron);
        Assert.Equal(1, attr.MaxConcurrency);
        Assert.Equal(0, attr.TimeoutSeconds);
        Assert.True(attr.AllowManualTrigger);
        Assert.True(attr.Enabled);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ctor_BlankName_Throws(string? name) => Assert.Throws<ArgumentException>(() => new DuckRunJobAttribute(name!, "* * * * *"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ctor_BlankCron_Throws(string? cron) => Assert.Throws<ArgumentException>(() => new DuckRunJobAttribute("name", cron!));
}
