using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;

namespace Locally.Aspire.Hosting.Tests;

public class LocallyHostNamesTests
{
    [Fact]
    public void NoWebsiteOverrideWithoutAnAppServiceEnvironment()
    {
        var builder = DistributedApplication.CreateBuilder();
        var api = builder.AddContainer("api", "example/api");

        LocallyHostNames.Apply(builder, new DistributedApplicationModel(builder.Resources));

        Assert.False(api.Resource.HasAnnotationOfType<AzureAppServiceWebsiteCustomizationAnnotation>());
    }

    // App Service environments are part of the model only when publishing.
    [Fact]
    public void EveryComputeResourceGetsTheWebsiteOverrideWithAnEnvironment()
    {
        var builder = DistributedApplication.CreateBuilder(["--operation", "publish", "--output-path", Path.GetTempPath()]);
        builder.AddAzureAppServiceEnvironment("env");
        Assert.True(builder.ExecutionContext.IsPublishMode);
        Assert.Contains(builder.Resources, r => r is AzureAppServiceEnvironmentResource);
        var api = builder.AddContainer("api", "example/api");
        var web = builder.AddContainer("web", "example/web");

        LocallyHostNames.Apply(builder, new DistributedApplicationModel(builder.Resources));

        Assert.Single(api.Resource.Annotations.OfType<AzureAppServiceWebsiteCustomizationAnnotation>());
        Assert.Single(web.Resource.Annotations.OfType<AzureAppServiceWebsiteCustomizationAnnotation>());
    }
}
