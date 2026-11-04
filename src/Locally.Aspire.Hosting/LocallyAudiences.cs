using Azure.Core;

namespace Locally.Aspire.Hosting;

// Asks for a token for Locally's audience where the caller asked for Azure's.
internal sealed class LocallyAudiences(TokenCredential credential, IReadOnlyDictionary<string, string> audiences) : TokenCredential
{
    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
        credential.GetToken(Translate(requestContext), cancellationToken);

    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
        credential.GetTokenAsync(Translate(requestContext), cancellationToken);

    private TokenRequestContext Translate(TokenRequestContext context) =>
        new(context.Scopes.Select(Translate).ToArray(), context.ParentRequestId, context.Claims, context.TenantId, context.IsCaeEnabled);

    // "https://management.azure.com/.default" -> "<Locally's ARM audience>/.default"
    internal string Translate(string scope)
    {
        var resource = scope.EndsWith("/.default", StringComparison.OrdinalIgnoreCase) ? scope[..^"/.default".Length] : scope;
        return audiences.TryGetValue(resource.TrimEnd('/'), out var audience)
            ? audience.TrimEnd('/') + "/.default"
            : scope;
    }
}
