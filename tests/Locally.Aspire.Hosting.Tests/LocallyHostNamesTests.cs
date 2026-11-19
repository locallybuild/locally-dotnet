#pragma warning disable ASPIREPIPELINES001 // PipelineConfigurationAnnotation is experimental.

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Aspire.Hosting.Pipelines;

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

    // A site looks up the sites it references, so it's provisioned after them.
    [Fact]
    public void AResourceIsOrderedAfterTheComputeResourcesItReferences()
    {
        var builder = DistributedApplication.CreateBuilder(["--operation", "publish", "--output-path", Path.GetTempPath()]);
        builder.AddAzureAppServiceEnvironment("env");
        var storage = builder.AddAzureStorage("storage");
        var api = builder.AddProject<TestProject>("api", o => o.ExcludeLaunchProfile = true).WithReference(storage.AddQueue("queue"));
        var web = builder.AddProject<TestProject>("web", o => o.ExcludeLaunchProfile = true).WithReference(api);
        var model = new DistributedApplicationModel(builder.Resources);
        var compute = model.GetComputeResources().ToList();

        Assert.Equal([api.Resource], LocallyHostNames.ReferencedComputeResources(web.Resource, compute));
        Assert.Empty(LocallyHostNames.ReferencedComputeResources(api.Resource, compute));

        int Count(IResource r) => r.Annotations.OfType<PipelineConfigurationAnnotation>().Count();
        var (webBefore, apiBefore) = (Count(web.Resource), Count(api.Resource));

        LocallyHostNames.Apply(builder, model);

        Assert.Equal(webBefore + 1, Count(web.Resource));
        Assert.Equal(apiBefore, Count(api.Resource));
    }

    // Two sites that reference each other can't both go first.
    [Fact]
    public void ResourcesThatReferenceEachOtherAreNotOrdered()
    {
        var builder = DistributedApplication.CreateBuilder(["--operation", "publish", "--output-path", Path.GetTempPath()]);
        builder.AddAzureAppServiceEnvironment("env");
        var api = builder.AddProject<TestProject>("api", o => o.ExcludeLaunchProfile = true);
        var web = builder.AddProject<TestProject>("web", o => o.ExcludeLaunchProfile = true).WithReference(api);
        api.WithReference(web);
        var compute = new DistributedApplicationModel(builder.Resources).GetComputeResources().ToList();

        Assert.Empty(LocallyHostNames.ReferencedComputeResources(web.Resource, compute));
        Assert.Empty(LocallyHostNames.ReferencedComputeResources(api.Resource, compute));
    }

    private sealed class TestProject : IProjectMetadata
    {
        public string ProjectPath => "TestProject.csproj";
    }
}
