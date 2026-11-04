using Azure.Core;
using Azure.Identity;

namespace Locally.Aspire.Client;

/// <summary>
/// A <see cref="DefaultAzureCredential"/> that honours
/// <c>AZURE_DISABLE_INSTANCE_DISCOVERY</c>, as Azure.Identity for .NET does not.
/// </summary>
/// <remarks>
/// <para>
/// <c>locally run</c> signs a process in to Locally's sign-in endpoint rather than
/// Microsoft Entra ID's. Before its first token, a credential asks
/// login.microsoftonline.com whether that endpoint is one of Microsoft's own -
/// instance discovery - unless it's turned off. <c>locally run</c> sets
/// <c>AZURE_DISABLE_INSTANCE_DISCOVERY=true</c> to turn it off, which this
/// credential reads.
/// </para>
/// <para>
/// <c>UseLocallyCredential()</c> gives every Azure client one of these. Pass one
/// yourself to a client it can't reach, such as one you construct.
/// </para>
/// </remarks>
public sealed class LocallyCredential : TokenCredential
{
    private readonly DefaultAzureCredential _credential;

    /// <summary>Creates the credential.</summary>
    public LocallyCredential()
        : this(new DefaultAzureCredentialOptions())
    {
    }

    /// <summary>Creates the credential with the given options.</summary>
    public LocallyCredential(DefaultAzureCredentialOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        DisablesInstanceDiscovery = options.DisableInstanceDiscovery |= InstanceDiscoveryDisabledByEnvironment;
        _credential = new DefaultAzureCredential(options);
    }

    internal bool DisablesInstanceDiscovery { get; }

    /// <inheritdoc />
    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
        _credential.GetToken(requestContext, cancellationToken);

    /// <inheritdoc />
    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
        _credential.GetTokenAsync(requestContext, cancellationToken);

    private static bool InstanceDiscoveryDisabledByEnvironment => string.Equals(
        Environment.GetEnvironmentVariable("AZURE_DISABLE_INSTANCE_DISCOVERY"), "true", StringComparison.OrdinalIgnoreCase);
}
