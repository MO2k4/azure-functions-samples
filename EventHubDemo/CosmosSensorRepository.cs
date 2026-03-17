using Microsoft.Azure.Cosmos;

namespace EventHubDemo;

public class CosmosSensorRepository(CosmosClient cosmosClient) : ICosmosRepository
{
    private const string DatabaseId = "SensorData";
    private const string ContainerId = "readings";

    public async Task SaveAsync(SensorReading reading)
    {
        var container = cosmosClient.GetContainer(DatabaseId, ContainerId);

        var document = new
        {
            id = Guid.NewGuid().ToString(),
            reading.DeviceId,
            reading.Temperature,
            reading.Humidity,
            reading.Timestamp,
        };

        await container.CreateItemAsync(document, new PartitionKey(reading.DeviceId));
    }
}
