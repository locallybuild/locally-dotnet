using System.Net;
using Azure.Core;
using Locally.Aspire.Client;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Signs an app's Azure clients in to Locally.
/// </summary>
public static class LocallyClientExtensions
{
    /// <summary>
    /// Signs the app's Azure clients in to Locally, under <c>locally run</c> and
    /// once Locally hosts the app, with a <see cref="LocallyCredential"/>.
    /// Anywhere else, they sign in exactly as they would without it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every client built by Microsoft.Extensions.Azure - which includes Aspire's
    /// Azure client integrations - uses it, unless it's given a credential of its
    /// own (for an Aspire client, <c>settings.Credential</c>). So to use a real
    /// Azure resource alongside Locally's, give its client the credential to sign
    /// in to Azure with.
    /// </para>
    /// <para>
    /// Redis clients registered by Aspire sign in with it too when they connect
    /// to a cache in Locally - a <c>*.locally</c> host, over TLS, with no
    /// password. Aspire's own <c>WithAzureAuthentication()</c> only signs in to
    /// Azure's host names.
    /// </para>
    /// <para>
    /// Aspire's PostgreSQL, Cosmos DB and App Configuration clients create a
    /// credential of their own, so give each this one: <c>settings.Credential = new LocallyCredential()</c>.
    /// </para>
    /// </remarks>
    public static IHostApplicationBuilder UseLocallyCredential(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UseLocallyCredential(new LocallyCredential());
    }

    /// <summary>
    /// Whether the app is running under <c>locally run</c>, rather than against Azure.
    /// </summary>
    /// <remarks>
    /// An app deployed into Locally isn't started by <c>locally run</c>, so this
    /// returns <c>false</c> there.
    /// </remarks>
    public static bool IsLocallyRun(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.Configuration.IsLocallyRun();
    }

    /// <summary>
    /// Whether the app is running under <c>locally run</c>, rather than against
    /// Azure. For code that runs once the app's built, such as middleware or a
    /// health check.
    /// </summary>
    /// <remarks>
    /// An app deployed into Locally isn't started by <c>locally run</c>, so this
    /// returns <c>false</c> there.
    /// </remarks>
    public static bool IsLocallyRun(this IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return string.Equals(configuration["LOCALLY_ENVIRONMENT"], "true", StringComparison.OrdinalIgnoreCase);
    }

    internal static IHostApplicationBuilder UseLocallyCredential(this IHostApplicationBuilder builder, TokenCredential credential)
    {
        // The credential Microsoft.Extensions.Azure gives a client that isn't
        // given one of its own. Outside `locally run` it's the DefaultAzureCredential
        // that would have been used anyway.
        builder.Services.AddAzureClients(clients => clients.UseCredential(credential));

        // Aspire's Redis client reads its connection into ConfigurationOptions,
        // which anything can post-configure.
        builder.Services.PostConfigureAll<ConfigurationOptions>(options =>
        {
            if (options.Ssl && string.IsNullOrEmpty(options.Password) && options.EndPoints.All(IsInLocally))
            {
                // Options are configured synchronously, so this blocks on the
                // first token. It runs once, when the options are first resolved,
                // and the client refreshes the token itself from then on.
                options.ConfigureForAzureWithTokenCredentialAsync(credential).GetAwaiter().GetResult();
            }
        });

        return builder;
    }

    private static bool IsInLocally(EndPoint endpoint) =>
        endpoint is DnsEndPoint { Host: var host } && host.EndsWith(".locally", StringComparison.OrdinalIgnoreCase);
}
