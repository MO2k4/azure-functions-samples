using Azure.Data.Tables;

namespace HttpTriggerDemo;

public class TableStorageOrderRepository(TableClient tableClient) : IOrderRepository
{
    public async Task SaveAsync(Order order)
    {
        var entity = new TableEntity(order.ProductId, order.OrderId)
        {
            ["Quantity"] = order.Quantity
        };
        await tableClient.AddEntityAsync(entity);
    }
}
