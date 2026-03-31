using Azure.Messaging.EventHubs;
using EventHubDemo.Logging;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EventHubDemo;

public class SensorReadingFunction(
    ILogger<SensorReadingFunction> logger,
    ISensorProcessor processor)
{
    [Function(nameof(SensorReadingFunction))]
    public async Task Run(
        [EventHubTrigger("sensor-readings", Connection = "EventHubConnection")]
        EventData[] events)
    {
        using var batchScope = logger.BeginScope(new Dictionary<string, object>
        {
            ["BatchSize"] = events.Length
        });

        SensorLogs.BatchReceived(logger, events.Length);

        foreach (var eventData in events)
        {
            var reading = JsonSerializer.Deserialize<SensorReading>(eventData.Body.Span);
            if (reading is null)
            {
                SensorLogs.InvalidEventSkipped(logger);
                continue;
            }

            using var eventScope = logger.BeginScope(new Dictionary<string, object>
            {
                ["DeviceId"] = reading.DeviceId
            });

            await processor.ProcessAsync(reading);
        }
    }
}
