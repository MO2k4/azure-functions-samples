using Azure.Messaging.EventHubs;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using System.Text.Json;

namespace EventHubDemo.Tests;

public class SensorReadingFunctionTests
{
    private readonly ISensorProcessor _processor = Substitute.For<ISensorProcessor>();
    private readonly SensorReadingFunction _function;

    public SensorReadingFunctionTests()
    {
        _function = new SensorReadingFunction(NullLogger<SensorReadingFunction>.Instance, _processor);
    }

    [Fact]
    public async Task Run_WithSingleEvent_CallsProcessorOnce()
    {
        var reading = new SensorReading("device-01", 22.5, 60.0, DateTimeOffset.UtcNow);
        var events = new[] { CreateEventData(reading) };

        await _function.Run(events);

        await _processor.Received(1).ProcessAsync(Arg.Is<SensorReading>(r =>
            r.DeviceId == "device-01" &&
            r.Temperature == 22.5 &&
            r.Humidity == 60.0));
    }

    [Fact]
    public async Task Run_WithBatchOfThreeEvents_CallsProcessorThreeTimes()
    {
        var events = new[]
        {
            CreateEventData(new SensorReading("device-01", 22.5, 60.0, DateTimeOffset.UtcNow)),
            CreateEventData(new SensorReading("device-02", 25.0, 55.0, DateTimeOffset.UtcNow)),
            CreateEventData(new SensorReading("device-03", 18.3, 72.0, DateTimeOffset.UtcNow)),
        };

        await _function.Run(events);

        await _processor.Received(3).ProcessAsync(Arg.Any<SensorReading>());
    }

    private static EventData CreateEventData(SensorReading reading)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(reading);
        return new EventData(json);
    }
}
