using Azure.Data.Tables;
using Testcontainers.Azurite;

namespace HttpTriggerDemo.Tests;

public class TableStorageOrderRepositoryTests : IAsyncLifetime
{
    private readonly AzuriteContainer _azurite = new AzuriteBuilder().Build();

    public async Task InitializeAsync() => await _azurite.StartAsync();

    [Fact]
    public async Task SaveAsync_WithValidOrder_PersistsToTableStorage()
    {
        var client = new TableClient(_azurite.GetConnectionString(), "orders");
        await client.CreateIfNotExistsAsync();

        var repository = new TableStorageOrderRepository(client);
        var order = new Order("ORD-TEST01", "WIDGET-42", 3);

        await repository.SaveAsync(order);

        var entity = await client.GetEntityAsync<TableEntity>(
            order.ProductId, order.OrderId);
        Assert.Equal(3, entity.Value["Quantity"]);
    }

    public async Task DisposeAsync() => await _azurite.DisposeAsync();
}
