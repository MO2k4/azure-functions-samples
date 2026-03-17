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
            logger.LogWarning(
                "Reading from {DeviceId} has out-of-range temperature {Temperature}°C — skipping",
                reading.DeviceId, reading.Temperature);
            return;
        }

        if (reading.Humidity < MinHumidity || reading.Humidity > MaxHumidity)
        {
            logger.LogWarning(
                "Reading from {DeviceId} has out-of-range humidity {Humidity}% — skipping",
                reading.DeviceId, reading.Humidity);
            return;
        }

        await repository.SaveAsync(reading);

        logger.LogInformation(
            "Saved reading from {DeviceId}: {Temperature}°C, {Humidity}%",
            reading.DeviceId, reading.Temperature, reading.Humidity);
    }
}
