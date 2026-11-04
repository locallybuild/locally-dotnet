#pragma warning disable ASPIREPIPELINES002 // IDeploymentStateManager is experimental.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Azure.Core;
using Azure.ResourceManager;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Locally.Aspire.Hosting;

// Aspire remembers what it provisioned - the resource group, and each
// deployment's outputs - and on the next run reuses it without asking Azure
// whether it's still there. Locally starts empty each time it's launched, so
// after a restart that memory describes resources that no longer exist. This
// forgets it when the resource group it names has gone, so Aspire provisions
// everything afresh.
internal static class StaleDeploymentState
{
    public static async Task ForgetIfGoneAsync(BeforeStartEvent @event, TokenCredential credential, string subscriptionId, CancellationToken cancellationToken)
    {
        var state = @event.Services.GetRequiredService<IDeploymentStateManager>();
        var azure = await state.AcquireSectionAsync("Azure", cancellationToken);
        if (azure.Data["ResourceGroup"]?.GetValue<string>() is not { Length: > 0 } resourceGroup)
        {
            return;
        }

        // State saved for a different subscription is forgotten too: AddLocally()
        // overrides the subscription, so it can't describe anything in this one.
        var provisionedIn = azure.Data["SubscriptionId"]?.GetValue<string>();
        if (string.Equals(provisionedIn, subscriptionId, StringComparison.OrdinalIgnoreCase))
        {
            var subscription = new ArmClient(credential, subscriptionId)
                .GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
            if ((await subscription.GetResourceGroups().ExistsAsync(resourceGroup, cancellationToken)).Value)
            {
                return;
            }
        }

        @event.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Locally")
            .LogInformation("Resource group {ResourceGroup} isn't in Locally any more, so its Azure resources will be provisioned again.", resourceGroup);
        await state.ClearAllStateAsync(cancellationToken);
    }
}
