using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Locally.Aspire.Client.Tests;

public class IsLocallyRunTests
{
    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("TRUE", true)]
    [InlineData("false", false)]
    [InlineData("1", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ReadsLocallyEnvironment(string? value, bool expected)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new());
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["LOCALLY_ENVIRONMENT"] = value });

        Assert.Equal(expected, builder.IsLocallyRun());
        Assert.Equal(expected, builder.Configuration.IsLocallyRun());
    }
}
