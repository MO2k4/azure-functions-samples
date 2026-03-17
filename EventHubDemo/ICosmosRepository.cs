namespace EventHubDemo;

public interface ICosmosRepository
{
    Task SaveAsync(SensorReading reading);
}
