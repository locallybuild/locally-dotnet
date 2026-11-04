using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Azure.Provisioning;
using Azure.Provisioning.AppService;
using Azure.Provisioning.Expressions;
using Azure.Provisioning.RedisEnterprise;
using AzureRedis = Azure.Provisioning.Redis.RedisResource;

namespace Locally.Aspire.Hosting;

/// <summary>
/// Aspire writes a few values into the Bicep it publishes that assume Azure's
/// fixed host names and ports. These overrides read the real values from the
/// resources instead, so the same Bicep deploys to Locally and to Azure; on
/// Azure they produce exactly the values Aspire would have written.
/// </summary>
internal static class LocallyHostNames
{
    /// <summary>
    /// Attaches the overrides to every resource in <paramref name="model"/>
    /// they apply to. Called once, before the infrastructure is generated.
    /// </summary>
    public static void Apply(IDistributedApplicationBuilder builder, DistributedApplicationModel model)
    {
        foreach (var cache in model.Resources.OfType<AzureManagedRedisResource>())
        {
            builder.CreateResourceBuilder(cache).ConfigureInfrastructure(UseManagedRedisDatabasePort);
        }

#pragma warning disable CS0618 // Azure Cache for Redis is obsolete in Aspire, but still served.
        foreach (var cache in model.Resources.OfType<AzureRedisCacheResource>())
#pragma warning restore CS0618
        {
            builder.CreateResourceBuilder(cache).ConfigureInfrastructure(UseRedisSslPort);
        }

        var environments = model.Resources.OfType<AzureAppServiceEnvironmentResource>().ToList();
        if (environments.Count == 0)
        {
            // A website customisation with no App Service environment is an
            // error to Aspire, so the website override is only added with one.
            return;
        }

        foreach (var environment in environments)
        {
            builder.CreateResourceBuilder(environment).ConfigureInfrastructure(UseDashboardHostName);
        }

        // The resources App Service publishes as websites: projects and
        // containers, as Aspire's own App Service support enumerates them.
        var compute = model.GetComputeResources().ToList();
        var computeNames = compute.Select(r => r.Name).ToList();
        foreach (var resource in compute)
        {
            var others = computeNames.Where(n => n != resource.Name).ToArray();
            resource.Annotations.Add(new AzureAppServiceWebsiteCustomizationAnnotation(
                (infra, site) => ResolveReferencesByHostName(infra, site, others)));
        }
    }

    // Azure Managed Redis always serves its database on port 10000, so Aspire
    // writes that port into the connection string. Locally serves each database
    // on a port of its own, so use the database's deployed port. (Its Port
    // property is the 10000 Aspire sets, inlined as a literal, so the deployed
    // value is referenced instead.)
    private static void UseManagedRedisDatabasePort(AzureResourceInfrastructure infra)
    {
        var cluster = infra.GetProvisionableResources().OfType<RedisEnterpriseCluster>().SingleOrDefault();
        var database = infra.GetProvisionableResources().OfType<RedisEnterpriseDatabase>().SingleOrDefault();
        if (cluster is null || database is null || Output(infra, "connectionString") is not { } output)
        {
            return;
        }
        var port = new MemberExpression(new MemberExpression(new IdentifierExpression(database.BicepIdentifier), "properties"), "port");
        output.Value = BicepFunction.Interpolate($"{cluster.HostName}:{port},ssl=true");
    }

    // Aspire builds an Azure Cache for Redis connection string as
    // "<host>,ssl=true", relying on the client's default TLS port of 6380.
    // Locally serves each cache on a port of its own, so name its sslPort.
    private static void UseRedisSslPort(AzureResourceInfrastructure infra)
    {
        var redis = infra.GetProvisionableResources().OfType<AzureRedis>().SingleOrDefault();
        if (redis is null || Output(infra, "connectionString") is not { } output)
        {
            return;
        }
        output.Value = BicepFunction.Interpolate($"{redis.HostName}:{redis.SslPort},ssl=true");
    }

    // The dashboard's URL is built as "https://<name>.azurewebsites.net".
    // Locally gives web apps host names of their own, so use the dashboard
    // site's defaultHostName. Apps send telemetry to the dashboard at this URL.
    private static void UseDashboardHostName(AzureResourceInfrastructure infra)
    {
        if (Output(infra, "AZURE_APP_SERVICE_DASHBOARD_URI") is not { } output)
        {
            return; // no dashboard (WithDashboard(false))
        }
        var dashboard = infra.GetProvisionableResources().OfType<WebSite>().SingleOrDefault();
        if (dashboard is not null)
        {
            output.Value = BicepFunction.Interpolate($"https://{dashboard.DefaultHostName}");
        }
    }

    // Service discovery settings (services__api__https__0, API_HTTPS and so on)
    // are built as "https://<name>.azurewebsites.net" too. Point each one at
    // the referenced site's defaultHostName instead, looked up as an existing
    // site by the name Aspire gives it: "<resource>-<uniqueString(resource group)>".
    internal static void ResolveReferencesByHostName(AzureResourceInfrastructure infra, WebSite site, IEnumerable<string> references)
    {
        foreach (var reference in references)
        {
            var prefixes = new[] { $"services__{reference}__", $"{reference.ToUpperInvariant()}_HTTP" };
            var settings = site.SiteConfig.AppSettings
                .Select(s => s.Value)
                .Where(pair => pair is not null && prefixes.Any(p => pair.Name.Value?.StartsWith(p, StringComparison.OrdinalIgnoreCase) == true))
                .ToList();
            if (settings.Count == 0)
            {
                continue;
            }

            var referenced = WebSite.FromExisting($"{Infrastructure.NormalizeBicepIdentifier(reference)}_site");
            referenced.Name = BicepFunction.Take(
                BicepFunction.Interpolate($"{reference}-{BicepFunction.GetUniqueString(BicepFunction.GetResourceGroup().Id)}"), 60);
            infra.Add(referenced);
            foreach (var pair in settings)
            {
                pair!.Value = BicepFunction.Interpolate($"https://{referenced.DefaultHostName}");
            }
        }
    }

    private static ProvisioningOutput? Output(AzureResourceInfrastructure infra, string name) =>
        infra.GetProvisionableResources().OfType<ProvisioningOutput>().SingleOrDefault(o => o.BicepIdentifier == name);
}
