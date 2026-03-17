namespace EventHubDemo;

public record SensorReading(
    string DeviceId,
    double Temperature,
    double Humidity,
    DateTimeOffset Timestamp);
