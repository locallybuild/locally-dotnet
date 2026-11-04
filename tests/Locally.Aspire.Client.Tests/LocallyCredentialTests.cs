using Azure.Identity;

namespace Locally.Aspire.Client.Tests;

[Collection("environment")]
public class LocallyCredentialTests
{
    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData(null, false)]
    [InlineData("false", false)]
    public void HonoursAzureDisableInstanceDiscovery(string? value, bool disabled)
    {
        var previous = Environment.GetEnvironmentVariable("AZURE_DISABLE_INSTANCE_DISCOVERY");
        Environment.SetEnvironmentVariable("AZURE_DISABLE_INSTANCE_DISCOVERY", value);
        try
        {
            Assert.Equal(disabled, new LocallyCredential().DisablesInstanceDiscovery);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AZURE_DISABLE_INSTANCE_DISCOVERY", previous);
        }
    }

    [Fact]
    public void KeepsInstanceDiscoveryOffWhenAskedTo()
    {
        var credential = new LocallyCredential(new DefaultAzureCredentialOptions { DisableInstanceDiscovery = true });

        Assert.True(credential.DisablesInstanceDiscovery);
    }
}
