using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Locally.Aspire.Hosting;

// Checks that a provisioned Storage account's blobs or queues can be reached,
// signing in with the given credential rather than the one Aspire's own
// health checks use.
internal sealed class StorageHealthCheck(IResourceWithConnectionString resource, TokenCredential credential) : IHealthCheck
{
    // The resources Aspire health-checks from the AppHost, when they're
    // provisioned rather than emulated.
    public static bool Checks(IResourceWithConnectionString resource) => resource switch
    {
        AzureBlobStorageResource blobs => !blobs.Parent.IsEmulator(),
        AzureBlobStorageContainerResource container => !container.Parent.Parent.IsEmulator(),
        AzureQueueStorageResource queues => !queues.Parent.IsEmulator(),
        AzureQueueStorageQueueResource queue => !queue.Parent.Parent.IsEmulator(),
        _ => false,
    };

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken)
    {
        try
        {
            switch (resource)
            {
                case AzureBlobStorageResource blobs:
                    await FirstPage(new BlobServiceClient(await EndpointOf(blobs, cancellationToken), credential)
                        .GetBlobContainersAsync(cancellationToken: cancellationToken), cancellationToken);
                    break;
                case AzureBlobStorageContainerResource container:
                    await new BlobServiceClient(await EndpointOf(container.Parent, cancellationToken), credential)
                        .GetBlobContainerClient(container.BlobContainerName).GetPropertiesAsync(cancellationToken: cancellationToken);
                    break;
                case AzureQueueStorageResource queues:
                    await FirstPage(new QueueServiceClient(await EndpointOf(queues, cancellationToken), credential)
                        .GetQueuesAsync(cancellationToken: cancellationToken), cancellationToken);
                    break;
                case AzureQueueStorageQueueResource queue:
                    await new QueueServiceClient(await EndpointOf(queue.Parent, cancellationToken), credential)
                        .GetQueueClient(queue.QueueName).GetPropertiesAsync(cancellationToken);
                    break;
            }
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(exception: ex);
        }
    }

    // A provisioned blob or queue service's connection string is its endpoint,
    // or "Endpoint=<endpoint>;..." - either way, the endpoint.
    private static async Task<Uri> EndpointOf(IResourceWithConnectionString service, CancellationToken cancellationToken)
    {
        var connectionString = await service.ConnectionStringExpression.GetValueAsync(cancellationToken) ?? "";
        var endpoint = connectionString.Split(';')
            .Select(part => part.Split('=', 2))
            .FirstOrDefault(kv => kv.Length == 2 && kv[0].Equals("Endpoint", StringComparison.OrdinalIgnoreCase))?[1];
        return new Uri(endpoint ?? connectionString);
    }

    private static async Task FirstPage<T>(global::Azure.AsyncPageable<T> pageable, CancellationToken cancellationToken) where T : notnull
    {
        await using var pages = pageable.AsPages(pageSizeHint: 1).GetAsyncEnumerator(cancellationToken);
        await pages.MoveNextAsync();
    }
}
