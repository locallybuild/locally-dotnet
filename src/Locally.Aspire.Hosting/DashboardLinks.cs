using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Hosting;

namespace Locally.Aspire.Hosting;

// Rewrites the Azure portal links Aspire publishes for each resource it
// provisions - a deployment's, or a resource's - as links to the same resource
// in the Locally Dashboard's Resource View.
internal sealed class DashboardLinks(ResourceNotificationService notifications, Uri dashboard) : BackgroundService
{
    private const string Portal = "https://portal.azure.com";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
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
