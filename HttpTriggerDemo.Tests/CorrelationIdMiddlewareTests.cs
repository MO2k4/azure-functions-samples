namespace HttpTriggerDemo.Tests;

[Collection(FunctionAppContainerFixture.Name)]
public class CorrelationIdMiddlewareTests(FunctionAppContainerFixture fixture)
{
    private const string Header = "X-Correlation-Id";

    [Fact]
    public async Task CorrelationId_WhenMissing_IsGeneratedInResponse()
    {
        var client = fixture.CreateClient();

        var response = await client.GetAsync("/api/hello");

        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.Contains(Header),
            $"Expected {Header} header in response");
        var value = response.Headers.GetValues(Header).Single();
        Assert.True(Guid.TryParse(value, out _),
            $"Expected a GUID but got: {value}");
    }

    [Fact]
    public async Task CorrelationId_WhenProvided_IsEchoedBack()
    {
        var client = fixture.CreateClient();
        var id = Guid.NewGuid().ToString();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/hello");
        request.Headers.Add(Header, id);

        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var echoedId = response.Headers.GetValues(Header).Single();
        Assert.Equal(id, echoedId);
    }
}
