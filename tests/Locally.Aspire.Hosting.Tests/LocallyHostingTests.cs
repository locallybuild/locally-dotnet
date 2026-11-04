using Aspire.Hosting;
using Azure.Core;
using Azure.Identity;

namespace Locally.Aspire.Hosting.Tests;

public class LocallyHostingTests
{
    private const string Subscription = "307d8f52-9719-460e-9f85-aa408e28ee55";
    private const string ResourceGroupId = $"/subscriptions/{Subscription}/resourceGroups/rg-aspire-apphost-db4037bd";

    // Outside `locally run`, AddLocally() only attaches the host-name
    // overrides (they apply when publishing too): it points nothing at
    // Locally, and calling it again adds nothing.
    [Fact]
    public void OutsideLocallyRunOnlyAddsTheHostNameOverrides()
    {
        var builder = DistributedApplication.CreateBuilder();
        var before = builder.Services.Count;

        builder.AddLocally();
        var afterFirst = builder.Services.Count;
        builder.AddLocally();

        Assert.False(builder.IsLocallyRun());
        Assert.Equal(before + 1, afterFirst);
        Assert.Equal(afterFirst, builder.Services.Count);
        Assert.Null(builder.Configuration["Azure:SubscriptionId"]);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    [InlineData("TRUE")]
    public void IsLocallyRunUnderLocallyRun(string value)
    {
        var builder = DistributedApplication.CreateBuilder([$"LOCALLY_ENVIRONMENT={value}"]);

        Assert.True(builder.IsLocallyRun());
    }

    [Fact]
    public void RefusesToStartWithoutLocallyRunsEnvironment()
    {
        var builder = DistributedApplication.CreateBuilder(["LOCALLY_ENVIRONMENT=true"]);

        var error = Assert.Throws<InvalidOperationException>(() => builder.AddLocally());

        Assert.Contains("locally run", error.Message);
    }

    [Theory]
    [InlineData("https://management.azure.com/.default", "https://127.0.0.1:5680/.default")]
    [InlineData("https://management.azure.com//.default", "https://127.0.0.1:5680/.default")]
    [InlineData("https://management.core.windows.net/.default", "https://127.0.0.1:5680/.default")]
    [InlineData("https://graph.windows.net/.default", "https://127.0.0.1:5679/.default")]
    [InlineData("https://storage.azure.com/.default", "https://storage.azure.com/.default")]
    [InlineData("https://MANAGEMENT.azure.com/.DEFAULT", "https://127.0.0.1:5680/.default")]
    public void AsksForLocallysAudiences(string scope, string expected)
    {
        var audiences = new LocallyAudiences(new ClientSecretCredential("t", "c", "s"), new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["https://management.azure.com"] = "https://127.0.0.1:5680",
            ["https://management.core.windows.net"] = "https://127.0.0.1:5680",
            ["https://graph.windows.net"] = "https://127.0.0.1:5679/",
        });

        Assert.Equal(expected, audiences.Translate(scope));
    }

    [Fact]
    public void LinksPortalPagesToTheLocallyDashboard()
    {
        var deployment = $"{ResourceGroupId}/providers/Microsoft.Resources/deployments/storage";
        var portal = $"https://portal.azure.com/#view/HubsExtension/DeploymentDetailsBlade/~/overview/id/{Uri.EscapeDataString(deployment)}";

        var link = DashboardLinks.Rewrite(portal, new Uri("https://localhost:5678"));

        Assert.Equal($"https://localhost:5678/subscriptions/{Subscription}/resources/view?resourceID={Uri.EscapeDataString(deployment)}", link);
    }

    [Theory]
    [InlineData("https://portal.azure.com/#home")]
    [InlineData("https://localhost:7152/")]
    public void LeavesOtherLinksAlone(string url)
    {
        Assert.Equal(url, DashboardLinks.Rewrite(url, new Uri("https://localhost:5678")));
    }
}
