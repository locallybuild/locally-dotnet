using System.Net;
using System.Net.Http.Json;
using Aspire.Hosting;
using Aspire.Hosting.Testing;

public class AppConfigurationTests
{
    // Provisioning in Locally is quick, but leave room for a cold CI runner.
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(15);

    [Fact]
    public async Task StoresSettingsAndRefreshesThem()
    {
        using var cts = new CancellationTokenSource(Timeout);

        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Locally_Examples_Aspire_AppConfiguration_AppHost>(cts.Token);
        await using var app = await builder.BuildAsync(cts.Token);
        await app.StartAsync(cts.Token);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("api", cts.Token);

        using var http = app.CreateHttpClient("api");
        var key = $"setting-{Guid.NewGuid():N}";

        var stored = await http.PutAsync($"/settings/{key}", new StringContent("hello from Locally"), cts.Token);
        Assert.Equal(HttpStatusCode.NoContent, stored.StatusCode);

        // The app's configuration is refreshed in the background as requests come in, so this can take a few requests.
        while (true)
        {
            var response = await http.GetAsync($"/settings/{key}", cts.Token);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                Assert.Equal("hello from Locally", await response.Content.ReadFromJsonAsync<string>(cts.Token));
                break;
            }

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            await Task.Delay(TimeSpan.FromSeconds(1), cts.Token);
        }
    }
}
