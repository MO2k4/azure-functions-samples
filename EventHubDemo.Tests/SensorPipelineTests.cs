using Azure.Messaging.EventHubs;
using System.Text.Json;

namespace EventHubDemo.Tests;

[Collection(SensorPipelineFixture.Name)]
public class SensorPipelineTests(SensorPipelineFixture fixture)
{
    [Fact]
    public async Task PublishedEvent_WithValidReading_AppearsInCosmosDb()
    {
        var reading = new SensorReading(
            DeviceId: $"device-{Guid.NewGuid():N}",
            Temperature: 23.4,
            Humidity: 58.0,
            Timestamp: DateTimeOffset.UtcNow);

        var json = JsonSerializer.SerializeToUtf8Bytes(reading);
        var batch = await fixture.ProducerClient.CreateBatchAsync();
        batch.TryAdd(new EventData(json));
        await fixture.ProducerClient.SendAsync(batch);

        // Poll Cosmos DB until the document appears (max 30s)
        var container = fixture.CosmosClient.GetContainer("SensorData", "readings");
        var deadline = DateTime.UtcNow.AddSeconds(30);
        List<dynamic> results = [];

        while (DateTime.UtcNow < deadline)
        {
            var query = container.GetItemQueryIterator<dynamic>(
                $"SELECT * FROM c WHERE c.deviceId = '{reading.DeviceId}'");

            results.Clear();
            while (query.HasMoreResults)
            {
                var page = await query.ReadNextAsync();
                results.AddRange(page);
            }

            if (results.Count > 0)
                break;

            await Task.Delay(500);
        }

        Assert.Single(results);
        Assert.Equal(23.4, (double)results[0].temperature, precision: 1);
        Assert.Equal(58.0, (double)results[0].humidity, precision: 1);
    }

    [Fact]
    public async Task PublishedEvent_WithOutOfRangeTemperature_DoesNotAppearInCosmosDb()
    {
        var reading = new SensorReading(
            DeviceId: $"device-{Guid.NewGuid():N}",
            Temperature: 999.0,   // out of range — service should discard
            Humidity: 58.0,
            Timestamp: DateTimeOffset.UtcNow);

        var json = JsonSerializer.SerializeToUtf8Bytes(reading);
        var batch = await fixture.ProducerClient.CreateBatchAsync();
        batch.TryAdd(new EventData(json));
        await fixture.ProducerClient.SendAsync(batch);

        // Wait a reasonable time then assert nothing was written
        await Task.Delay(5000);

        var container = fixture.CosmosClient.GetContainer("SensorData", "readings");
        var query = container.GetItemQueryIterator<dynamic>(
            $"SELECT * FROM c WHERE c.deviceId = '{reading.DeviceId}'");

        var results = new List<dynamic>();
        while (query.HasMoreResults)
        {
            var page = await query.ReadNextAsync();
            results.AddRange(page);
        }

        Assert.Empty(results);
    }
}
