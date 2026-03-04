namespace HttpTriggerDemo.Tests;

[Collection(FunctionAppContainerFixture.Name)]
public class ExceptionHandlingMiddlewareTests(FunctionAppContainerFixture fixture)
{
    [Fact]
    public async Task UnhandledException_Returns500()
    {
        var client = fixture.CreateClient();

        var response = await client.GetAsync("/api/error");

        Assert.Equal(System.Net.HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task UnhandledException_ResponseBodyIsEmpty()
    {
        var client = fixture.CreateClient();

        var response = await client.GetAsync("/api/error");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Empty(body);
    }
}
