using EventHubDemo.Logging;
using Microsoft.Extensions.Logging;

namespace EventHubDemo;

public class SensorProcessorService(
    ILogger<SensorProcessorService> logger,
    ICosmosRepository repository) : ISensorProcessor
{
    private const double MinTemperature = -50.0;
    private const double MaxTemperature = 150.0;
    private const double MinHumidity = 0.0;
    private const double MaxHumidity = 100.0;

    public async Task ProcessAsync(SensorReading reading)
    {
        if (reading.Temperature < MinTemperature || reading.Temperature > MaxTemperature)
        {
            SensorLogs.TemperatureOutOfRange(logger, reading.Temperature);
            return;
        }

        if (reading.Humidity < MinHumidity || reading.Humidity > MaxHumidity)
        {
            SensorLogs.HumidityOutOfRange(logger, reading.Humidity);
            return;
        }

        await repository.SaveAsync(reading);

        SensorLogs.ReadingSaved(logger, reading.Temperature, reading.Humidity);
    }
}
