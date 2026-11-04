using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Locally.Aspire.Hosting;

// Rewrites the Azure portal links Aspire publishes for each resource it
// provisions - a deployment's, or a resource's - as links to the same resource
// in the Locally Dashboard's Resource View.
internal static class DashboardLinks
{
    private const string Portal = "https://portal.azure.com";

    // Watches every resource until the AppHost stops, rewriting its portal links
    // as soon as Aspire publishes them.
    public static void Start(IServiceProvider services, Uri dashboard)
    {
        var notifications = services.GetRequiredService<ResourceNotificationService>();
        var stopping = services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
        _ = Task.Run(() => WatchAsync(notifications, dashboard, stopping), stopping);
    }

    private static async Task WatchAsync(ResourceNotificationService notifications, Uri dashboard, CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var change in notifications.WatchAsync(stoppingToken))
            {
                if (change.Snapshot.Urls.Any(u => u.Url.StartsWith(Portal, StringComparison.OrdinalIgnoreCase)))
                {
                    await notifications.PublishUpdateAsync(change.Resource, change.ResourceId, state => state with
                    {
                        Urls = [.. state.Urls.Select(u => u with { Url = Rewrite(u.Url, dashboard) })],
                    });
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    // A portal link names the resource's ID (URL-encoded): link to that ID's page
    // in the Locally Dashboard, or leave the link alone if it has none.
    internal static string Rewrite(string url, Uri dashboard)
    {
        if (!url.StartsWith(Portal, StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }
        var decoded = Uri.UnescapeDataString(url);
        var at = decoded.IndexOf("/subscriptions/", StringComparison.OrdinalIgnoreCase);
        if (at < 0)
        {
            return url;
        }
        var resourceId = decoded[at..];
        var subscription = resourceId.Split('/')[2];
        return new Uri(dashboard, $"/subscriptions/{subscription}/resources/view?resourceID={Uri.EscapeDataString(resourceId)}").ToString();
    }
}
