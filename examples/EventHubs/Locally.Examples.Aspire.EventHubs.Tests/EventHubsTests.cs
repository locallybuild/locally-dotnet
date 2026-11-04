using System.Net;
using System.Net.Http.Json;
using Aspire.Hosting;
using Aspire.Hosting.Testing;

public class EventHubsTests
{
    // Provisioning in Locally is quick, but leave room for a cold CI runner.
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(15);

    [Fact]
    public async Task SendsAndReceivesEvents()
    {
        using var cts = new CancellationTokenSource(Timeout);

        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Locally_Examples_Aspire_EventHubs_AppHost>(cts.Token);
        await using var app = await builder.BuildAsync(cts.Token);
        await app.StartAsync(cts.Token);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("api", cts.Token);

        using var http = app.CreateHttpClient("api");
        var message = $"hello from Locally {Guid.NewGuid():N}";

        var sent = await http.PostAsync("/events", new StringContent(message), cts.Token);
        Assert.Equal(HttpStatusCode.Accepted, sent.StatusCode);

        var events = await http.GetFromJsonAsync<string[]>("/events", cts.Token);
        Assert.Contains(message, events!);
    }
}
