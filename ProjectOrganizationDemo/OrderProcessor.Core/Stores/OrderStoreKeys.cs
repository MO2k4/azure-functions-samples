namespace OrderProcessor.Core.Stores;

// Typed constants instead of stringly-typed [FromKeyedServices("sql")] callsites.
// A typo in the key throws InvalidOperationException at resolve time on .NET 9+.
public static class OrderStoreKeys
{
    public const string Sql = "sql";
    public const string Cosmos = "cosmos";
}
