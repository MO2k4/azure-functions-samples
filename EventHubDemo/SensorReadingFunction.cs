using Azure.Messaging.EventHubs;
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
        logger.LogInformation("Processing batch of {Count} events", events.Length);

        foreach (var eventData in events)
        {
            var reading = JsonSerializer.Deserialize<SensorReading>(eventData.Body.Span);
            if (reading is null)
            {
                logger.LogWarning("Received null or unparseable event — skipping");
                continue;
            }

            await processor.ProcessAsync(reading);
        }
    }
}
