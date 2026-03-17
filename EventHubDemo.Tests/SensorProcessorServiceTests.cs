using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace EventHubDemo.Tests;

public class SensorProcessorServiceTests
{
    private readonly ICosmosRepository _repository = Substitute.For<ICosmosRepository>();
    private readonly SensorProcessorService _service;

    public SensorProcessorServiceTests()
    {
        _service = new SensorProcessorService(NullLogger<SensorProcessorService>.Instance, _repository);
    }

    [Fact]
    public async Task ProcessAsync_WithValidReading_SavesToRepository()
    {
        var reading = new SensorReading("device-01", 22.5, 60.0, DateTimeOffset.UtcNow);

        await _service.ProcessAsync(reading);

        await _repository.Received(1).SaveAsync(Arg.Is<SensorReading>(r =>
            r.DeviceId == "device-01" &&
            r.Temperature == 22.5 &&
            r.Humidity == 60.0));
    }

    [Theory]
    [InlineData(-60.0)]   // below -50°C threshold
    [InlineData(160.0)]   // above 150°C threshold
    public async Task ProcessAsync_WithOutOfRangeTemperature_DoesNotSave(double temperature)
    {
        var reading = new SensorReading("device-01", temperature, 60.0, DateTimeOffset.UtcNow);

        await _service.ProcessAsync(reading);

        await _repository.DidNotReceive().SaveAsync(Arg.Any<SensorReading>());
    }

    [Theory]
    [InlineData(-1.0)]    // below 0%
    [InlineData(101.0)]   // above 100%
    public async Task ProcessAsync_WithOutOfRangeHumidity_DoesNotSave(double humidity)
    {
        var reading = new SensorReading("device-01", 22.5, humidity, DateTimeOffset.UtcNow);

        await _service.ProcessAsync(reading);

        await _repository.DidNotReceive().SaveAsync(Arg.Any<SensorReading>());
    }
}
