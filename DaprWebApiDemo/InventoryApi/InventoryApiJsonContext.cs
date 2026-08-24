using System.Text.Json;
using System.Text.Json.Serialization;
using InventoryApi.Stock;

namespace InventoryApi;

/// <summary>
/// Source-generated metadata for the stock contracts. Same
/// <c>JsonSerializerDefaults.Web</c> shape as orders-api's context, which is what keeps the two
/// halves of the invocation agreeing on property names without sharing a project.
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(StockCheckRequest))]
[JsonSerializable(typeof(StockCheckResponse))]
[JsonSerializable(typeof(StockLine))]
[JsonSerializable(typeof(StockShortfall))]
[JsonSerializable(typeof(StockLevel))]
[JsonSerializable(typeof(ErrorResponse))]
public sealed partial class InventoryApiJsonContext : JsonSerializerContext;
