using Azure.Core;
using Azure.Core.Pipeline;

namespace Locally.Aspire.Hosting;

// Points requests for Azure's services at Locally's endpoints for them.
internal sealed class LocallyEndpoints(IReadOnlyDictionary<string, Uri> endpoints) : HttpPipelineSynchronousPolicy
{
    public override void OnSendingRequest(HttpMessage message)
    {
        if (message.Request.Uri.Host is { } host && endpoints.TryGetValue(host, out var endpoint))
        {
            message.Request.Uri.Scheme = endpoint.Scheme;
            message.Request.Uri.Host = endpoint.Host;
            message.Request.Uri.Port = endpoint.Port;
        }
    }
}
