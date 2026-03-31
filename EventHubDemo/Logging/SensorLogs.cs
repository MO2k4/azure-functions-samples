using Microsoft.Extensions.Logging;

namespace EventHubDemo.Logging;

public static partial class SensorLogs
{
    [LoggerMessage(EventId = 2001, Level = LogLevel.Information,
        Message = "Processing batch of {BatchSize} events")]
    public static partial void BatchReceived(ILogger logger, int batchSize);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Warning,
        Message = "Received null or unparseable event, skipping")]
    public static partial void InvalidEventSkipped(ILogger logger);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Warning,
        Message = "Temperature {Temperature} out of range, skipping")]
    public static partial void TemperatureOutOfRange(ILogger logger, double temperature);

    [LoggerMessage(EventId = 2004, Level = LogLevel.Warning,
        Message = "Humidity {Humidity} out of range, skipping")]
    public static partial void HumidityOutOfRange(ILogger logger, double humidity);

    [LoggerMessage(EventId = 2005, Level = LogLevel.Information,
        Message = "Reading saved: {Temperature}C, {Humidity}%")]
    public static partial void ReadingSaved(ILogger logger, double temperature, double humidity);
}
