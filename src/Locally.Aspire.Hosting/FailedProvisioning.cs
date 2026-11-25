using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Locally.Aspire.Hosting;

// When Aspire fails to provision an Azure resource it marks it "Failed to
// Provision", which isn't one of the states Aspire's own waits stop on
// (FailedToStart, Exited, Finished, RuntimeUnhealthy). So anything that
// WaitFor()s the resource - and a test waiting on that - waits forever rather
// than failing. A resource Aspire marks as failed is marked FailedToStart too,
// which those waits do stop on.
internal static class FailedProvisioning
{
    // Watches every resource until the AppHost stops.
    public static void Start(IServiceProvider services)
    {
        var notifications = services.GetRequiredService<ResourceNotificationService>();
        var stopping = services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
        _ = Task.Run(() => WatchAsync(notifications, stopping), stopping);
    }

    private static async Task WatchAsync(ResourceNotificationService notifications, CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var change in notifications.WatchAsync(stoppingToken))
            {
                if (change.Resource is IAzureResource && HasFailed(change.Snapshot))
                {
                    await notifications.PublishUpdateAsync(change.Resource, change.ResourceId, state => state with
                    {
                        State = new(KnownResourceStates.FailedToStart, KnownResourceStateStyles.Error),
                    });
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    // Aspire's failure states for provisioning ("Failed to Provision", "Failed
    // to Provision Roles") are localised, so they're recognised by their error
    // style rather than their text.
    internal static bool HasFailed(CustomResourceSnapshot snapshot) =>
        snapshot.State is { } state
        && state.Style == KnownResourceStateStyles.Error
        && state.Text != KnownResourceStates.FailedToStart;
}
