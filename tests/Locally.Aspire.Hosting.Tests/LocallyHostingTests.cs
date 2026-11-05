#pragma warning disable ASPIREPIPELINES003 // ContainerBuildOptionsCallbackContext and ContainerTargetPlatform are experimental.

using System.Runtime.InteropServices;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Aspire.Hosting.Publishing;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

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

    // `locally run aspire deploy` publishes and then provisions, so Aspire is
    // pointed at Locally then too - not only under `aspire run`.
    [Fact]
    public void UnderLocallyRunAspireDeployPointsAspireAtLocally()
    {
        var builder = DistributedApplication.CreateBuilder(["--operation", "publish", .. LocallyRunsEnvironment]);

        builder.AddLocally();

        Assert.True(builder.ExecutionContext.IsPublishMode);
        Assert.False(builder.IsLocallyRun());
        Assert.Equal(Subscription, builder.Configuration["Azure:SubscriptionId"]);
        Assert.Contains(builder.Services, d => d.ServiceType == typeof(ITokenCredentialProvider));
    }

    [Fact]
    public async Task BuildsContainerImagesForThisMachine()
    {
        var builder = DistributedApplication.CreateBuilder();
        var container = builder.AddContainer("web", "nginx").Resource;
        var parameter = builder.AddParameter("secret", secret: true).Resource;

        LocallyExtensions.BuildImagesForThisMachine(new DistributedApplicationModel(builder.Resources));

        Assert.Empty(parameter.Annotations.OfType<ContainerBuildOptionsCallbackAnnotation>());
        var annotation = Assert.Single(container.Annotations.OfType<ContainerBuildOptionsCallbackAnnotation>());
        using var services = new ServiceCollection().BuildServiceProvider();
        var context = new ContainerBuildOptionsCallbackContext(container, services, NullLogger.Instance, CancellationToken.None, builder.ExecutionContext);
        await annotation.Callback(context);
        Assert.Equal(LocallyExtensions.HostPlatform(RuntimeInformation.OSArchitecture), context.TargetPlatform);
    }

    [Theory]
    [InlineData(Architecture.Arm64, ContainerTargetPlatform.LinuxArm64)]
    [InlineData(Architecture.X64, ContainerTargetPlatform.LinuxAmd64)]
    public void HostPlatformMatchesThisMachinesArchitecture(Architecture architecture, ContainerTargetPlatform expected)
    {
        Assert.Equal(expected, LocallyExtensions.HostPlatform(architecture));
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

    private static readonly string[] LocallyRunsEnvironment =
    [
        "LOCALLY_ENVIRONMENT=true",
        $"AZURE_SUBSCRIPTION_ID={Subscription}",
        "AZURE_TENANT_ID=00000000-0000-0000-0000-000000000001",
        "AZURE_CLIENT_ID=00000000-0000-0000-0000-000000000002",
        "AZURE_CLIENT_SECRET=secret",
        "AZURE_LOCATION=westeurope",
        "AZURE_AUTHORITY_HOST=https://127.0.0.1:5679",
        "AZURE_RESOURCE_MANAGER_ENDPOINT=https://127.0.0.1:5680",
        "AZURE_RESOURCE_MANAGER_AUDIENCE=https://127.0.0.1:5680",
        "AZURE_MICROSOFT_GRAPH_AUDIENCE=https://127.0.0.1:5679",
    ];
}
