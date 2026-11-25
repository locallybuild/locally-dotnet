using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace Locally.Aspire.Hosting.Tests;

public class FailedProvisioningTests
{
    // How long a wait that should end gets to end, and a wait that shouldn't
    // gets to show it doesn't.
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    // Without the watch, a resource waiting on an Azure resource Aspire failed
    // to provision waits forever - which is why the watch exists.
    [Fact]
    public async Task WithoutTheWatchAWaitOnAFailedResourceNeverEnds()
    {
        var (app, cache, api) = Build();
        await using var _ = app;
        var notifications = app.Services.GetRequiredService<ResourceNotificationService>();

        using var cts = new CancellationTokenSource(Patience);
        var waiting = notifications.WaitForDependenciesAsync(api, cts.Token);
        await PublishFailedToProvisionAsync(notifications, cache);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
    }

    [Fact]
    public async Task AWaitOnAFailedResourceFails()
    {
        var (app, cache, api) = Build();
        await using var _ = app;
        var notifications = app.Services.GetRequiredService<ResourceNotificationService>();
        FailedProvisioning.Start(app.Services);

        using var cts = new CancellationTokenSource(Patience);
        var waiting = notifications.WaitForDependenciesAsync(api, cts.Token);
        await PublishFailedToProvisionAsync(notifications, cache);

        await Assert.ThrowsAsync<DistributedApplicationException>(() => waiting);
    }

    public static TheoryData<string, string, bool> States => new()
    {
        { "Failed to Provision", KnownResourceStateStyles.Error, true },
        { "Failed to Provision Roles", KnownResourceStateStyles.Error, true },
        { "Échec de l'approvisionnement", KnownResourceStateStyles.Error, true },
        { KnownResourceStates.FailedToStart, KnownResourceStateStyles.Error, false },
        { "Provisioning", KnownResourceStateStyles.Info, false },
        { KnownResourceStates.Running, KnownResourceStateStyles.Success, false },
    };

    [Theory]
    [MemberData(nameof(States))]
    public void RecognisesAFailureByItsStyle(string text, string style, bool failed)
    {
        var snapshot = new CustomResourceSnapshot
        {
            ResourceType = "Azure",
            Properties = [],
            State = new(text, style),
        };

        Assert.Equal(failed, FailedProvisioning.HasFailed(snapshot));
    }

    [Fact]
    public void AResourceWithNoStateHasNotFailed()
    {
        var snapshot = new CustomResourceSnapshot { ResourceType = "Azure", Properties = [] };

        Assert.False(FailedProvisioning.HasFailed(snapshot));
    }

    // An Azure resource, and a resource which waits for it the way an example's
    // "api" does (stopping if it becomes unavailable, as it does under test).
    private static (DistributedApplication App, IResource Cache, IResource Api) Build()
    {
        var builder = DistributedApplication.CreateBuilder();
        var cache = builder.AddAzureManagedRedis("cache");
        var api = builder.AddResource(new Waiter("api"))
            .WaitFor(cache, WaitBehavior.StopOnResourceUnavailable);
        return (builder.Build(), cache.Resource, api.Resource);
    }

    // As Aspire does when the deployment fails.
    private static Task PublishFailedToProvisionAsync(ResourceNotificationService notifications, IResource resource) =>
        notifications.PublishUpdateAsync(resource, state => state with
        {
            State = new("Failed to Provision", KnownResourceStateStyles.Error),
        });

    private sealed class Waiter(string name) : Resource(name), IResourceWithWaitSupport;
}
