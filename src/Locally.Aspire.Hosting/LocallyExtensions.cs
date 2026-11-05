#pragma warning disable ASPIREPIPELINES003 // ContainerBuildOptionsCallbackAnnotation and ContainerTargetPlatform are experimental.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Aspire.Hosting.Publishing;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Identity;
using Locally.Aspire.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Runtime.InteropServices;

namespace Aspire.Hosting;

/// <summary>
/// Runs an AppHost's Azure resources in Locally.
/// </summary>
public static class LocallyExtensions
{
    private static int s_endpointsRedirected;

    /// <summary>
    /// Whether the AppHost is running under <c>locally run</c>: <c>aspire run</c>
    /// with Locally's environment, rather than publishing or running against Azure.
    /// </summary>
    public static bool IsLocallyRun(this IDistributedApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.ExecutionContext.IsRunMode && IsLocallyEnvironment(builder.Configuration);
    }

    /// <summary>
    /// Under <c>locally run</c>, provisions the AppHost's Azure resources in
    /// Locally rather than Azure: Aspire creates them there, grants you access to
    /// them, and hands the projects their connection strings, exactly as it would
    /// in an Azure subscription. This applies to both <c>aspire run</c> and
    /// <c>aspire deploy</c>. Otherwise it does nothing, so the AppHost publishes,
    /// runs and deploys against Azure, as before.
    /// </summary>
    /// <remarks>
    /// A resource set to <c>RunAsEmulator()</c> or <c>RunAsContainer()</c> still
    /// runs locally - Aspire never provisions those - so leave those calls out for
    /// the resources Locally should provide.
    /// </remarks>
    public static IDistributedApplicationBuilder AddLocally(this IDistributedApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // The host-name overrides apply whether or not this is `locally run`,
        // since they change the Bicep Aspire publishes. BeforeStartEvent fires
        // when publishing as well as running, once the app model is complete.
        if (builder.Services.All(d => d.ServiceType != typeof(HostNamesMarker)))
        {
            builder.Services.AddSingleton<HostNamesMarker>();
            builder.Eventing.Subscribe<BeforeStartEvent>((@event, _) =>
            {
                LocallyHostNames.Apply(builder, @event.Model);
                return Task.CompletedTask;
            });
        }

        // Under `locally run`, point Aspire at Locally whether it's running the
        // AppHost (`aspire run`) or deploying it (`aspire deploy`): both
        // provision through the same Azure SDK clients and credential.
        if (!IsLocallyEnvironment(builder.Configuration))
        {
            return builder;
        }
        var config = builder.Configuration;

        // Aspire's provisioner always talks to Azure Resource Manager at
        // management.azure.com, with no setting to change that. Every Azure SDK
        // client in this process starts from ClientOptions.Default, though, so a
        // policy added there sends those requests to Locally's endpoint instead.
        // ClientOptions.Default is process-wide, so it's only added once.
        if (Interlocked.Exchange(ref s_endpointsRedirected, 1) == 0)
        {
            var endpoints = new Dictionary<string, Uri>(StringComparer.OrdinalIgnoreCase)
            {
                ["management.azure.com"] = new Uri(Required(config, "AZURE_RESOURCE_MANAGER_ENDPOINT")),
            };
            ClientOptions.Default.AddPolicy(new LocallyEndpoints(endpoints), HttpPipelinePosition.PerRetry);
        }

        // Sign in to Locally as the identity `locally run` provides. Locally's
        // sign-in endpoint isn't one of Azure's, so instance discovery - which
        // would ask login.microsoftonline.com about it - is turned off.
        var signIn = new ClientSecretCredential(
            Required(config, "AZURE_TENANT_ID"), Required(config, "AZURE_CLIENT_ID"), Required(config, "AZURE_CLIENT_SECRET"),
            new ClientSecretCredentialOptions
            {
                AuthorityHost = new Uri(Required(config, "AZURE_AUTHORITY_HOST")),
                DisableInstanceDiscovery = true,
            });

        // Aspire asks for tokens for Azure's own audiences too, and Locally
        // accepts a token only for the audience it advertises for each service,
        // so the scopes are translated to Locally's.
        var resourceManager = Required(config, "AZURE_RESOURCE_MANAGER_AUDIENCE");
        var credential = new LocallyAudiences(signIn, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["https://management.azure.com"] = resourceManager,
            ["https://management.core.windows.net"] = resourceManager,
            // Aspire reads who you are from a token for the legacy Azure AD
            // Graph, which it asks for but doesn't call.
            ["https://graph.windows.net"] = Required(config, "AZURE_MICROSOFT_GRAPH_AUDIENCE"),
        });
        builder.Services.AddSingleton<ITokenCredentialProvider>(new LocallyCredentialProvider(credential));

        // Aspire checks a Storage account's health from the AppHost too, but signs
        // those checks in with a credential of its own rather than the one above -
        // a managed identity, since `locally run` sets AZURE_CLIENT_ID. They're
        // ordinary health check registrations, so they're replaced with checks
        // that sign in as above. An emulated account keeps its own.
        builder.Services.PostConfigure<HealthCheckServiceOptions>(options =>
        {
            foreach (var registration in options.Registrations)
            {
                var resource = builder.Resources.OfType<IResourceWithConnectionString>()
                    .FirstOrDefault(r => registration.Name == $"{r.Name}_check");
                if (resource is not null && StorageHealthCheck.Checks(resource))
                {
                    registration.Factory = _ => new StorageHealthCheck(resource, credential);
                }
            }
        });

        // The links are rewritten as Aspire provisions each resource. A hosted
        // service would only start once every resource had been provisioned,
        // so the watch starts from BeforeStartEvent, which fires before any are.
        var dashboard = new Uri(config["LOCALLY_DASHBOARD_URL"] ?? "https://localhost:5678");
        builder.Eventing.Subscribe<BeforeStartEvent>((@event, _) =>
        {
            DashboardLinks.Start(@event.Services, dashboard);
            return Task.CompletedTask;
        });

        // App Service builds each project's image for linux/amd64, which is what
        // Azure runs - but Locally runs it on this machine, where an image for
        // another architecture runs under emulation (and .NET hangs under QEMU).
        // So under `locally run` images are built for this machine instead.
        builder.Eventing.Subscribe<BeforeStartEvent>((@event, _) =>
        {
            BuildImagesForThisMachine(@event.Model);
            return Task.CompletedTask;
        });

        // Provision into the subscription, tenant and region `locally run`
        // selected, whatever Aspire remembers choosing on an earlier run: a
        // source added last outranks the user secrets it remembers them in.
        // Resource group creation is allowed because, after Locally restarts,
        // Aspire may need to recreate a resource group it remembers naming.
        var subscriptionId = Required(config, "AZURE_SUBSCRIPTION_ID");
        config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Azure:SubscriptionId"] = subscriptionId,
            ["Azure:TenantId"] = Required(config, "AZURE_TENANT_ID"),
            ["Azure:Location"] = Required(config, "AZURE_LOCATION"),
            ["Azure:AllowResourceGroupCreation"] = "true",
        });
        builder.Eventing.Subscribe<BeforeStartEvent>((@event, cancellationToken) =>
            StaleDeploymentState.ForgetIfGoneAsync(@event, credential, subscriptionId, cancellationToken));

        return builder;
    }

    internal static void BuildImagesForThisMachine(DistributedApplicationModel model)
    {
        var platform = HostPlatform(RuntimeInformation.OSArchitecture);
        foreach (var resource in model.Resources.Where(r => r is ProjectResource or ContainerResource))
        {
            resource.Annotations.Add(new ContainerBuildOptionsCallbackAnnotation(context => context.TargetPlatform = platform));
        }
    }

    internal static ContainerTargetPlatform HostPlatform(Architecture architecture) =>
        architecture == Architecture.Arm64 ? ContainerTargetPlatform.LinuxArm64 : ContainerTargetPlatform.LinuxAmd64;

    private static bool IsLocallyEnvironment(IConfiguration config) =>
        string.Equals(config["LOCALLY_ENVIRONMENT"], "true", StringComparison.OrdinalIgnoreCase);

    // `locally run` sets all of these; one missing means the AppHost wasn't
    // started by it, whatever LOCALLY_ENVIRONMENT says.
    private static string Required(IConfiguration config, string name) =>
        config[name] is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"{name} isn't set. Start the AppHost with `locally run aspire run`, which sets it.");

    private sealed class LocallyCredentialProvider(TokenCredential credential) : ITokenCredentialProvider
    {
        public TokenCredential TokenCredential => credential;
    }

    // Registered once per builder, so calling AddLocally() twice attaches the
    // host-name overrides once.
    private sealed class HostNamesMarker;
}
