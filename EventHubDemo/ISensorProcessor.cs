namespace EventHubDemo;

public interface ISensorProcessor
{
    Task ProcessAsync(SensorReading reading);
}
