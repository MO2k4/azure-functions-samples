using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Client.Entities;
using Microsoft.DurableTask.Entities;

namespace DurableEntitiesDemo;

// A client can signal an entity (fire-and-forget) and read its state, but it cannot
// call one for a return value. Only orchestrations get request/response.
public static class SignalCounterClient
{
    [Function(nameof(SignalCounterClient))]
    public static async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "counters/{key}/add")]
            HttpRequestData req,
        string key,
        [DurableClient] DurableTaskClient client)
    {
        var id = new EntityInstanceId(nameof(Counter), key);

        // Returns the instant the "Add" message is durably enqueued, not when it runs.
        await client.Entities.SignalEntityAsync(id, "Add", 5);

        return req.CreateResponse(System.Net.HttpStatusCode.Accepted);
    }
}

// Reading state without dispatching an operation: committed (possibly stale) state,
// null when the entity has never been created. The read does not create it.
public static class ReadCounterClient
{
    [Function(nameof(ReadCounterClient))]
    public static async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "counters/{key}")]
            HttpRequestData req,
        string key,
        [DurableClient] DurableTaskClient client)
    {
        var id = new EntityInstanceId(nameof(Counter), key);

        EntityMetadata<int>? entity = await client.Entities.GetEntityAsync<int>(id);
        int value = entity?.State ?? 0;

        HttpResponseData response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { key, value });
        return response;
    }
}

// Sharding for throughput: a hot single id serializes every write. Spread writes across
// N sub-entities, then fan out and sum on read (eventually consistent aggregate).
public static class ShardedCounterClient
{
    private const int ShardCount = 16;

    [Function(nameof(ShardedCounterAddClient))]
    public static async Task<HttpResponseData> ShardedCounterAddClient(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "sharded/add")]
            HttpRequestData req,
        [DurableClient] DurableTaskClient client)
    {
        // Write: pick a shard, signal only that one.
        int shard = Random.Shared.Next(ShardCount);
        var shardId = new EntityInstanceId(nameof(Counter), $"global-{shard}");
        await client.Entities.SignalEntityAsync(shardId, "Add", 1);

        return req.CreateResponse(System.Net.HttpStatusCode.Accepted);
    }

    [Function(nameof(ShardedCounterTotalClient))]
    public static async Task<HttpResponseData> ShardedCounterTotalClient(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "sharded/total")]
            HttpRequestData req,
        [DurableClient] DurableTaskClient client)
    {
        // Read: fan out across all shards and sum (eventually consistent aggregate).
        int total = 0;
        for (int i = 0; i < ShardCount; i++)
        {
            var id = new EntityInstanceId(nameof(Counter), $"global-{i}");
            EntityMetadata<int>? m = await client.Entities.GetEntityAsync<int>(id);
            total += m?.State ?? 0;
        }

        HttpResponseData response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { total });
        return response;
    }
}

// Cross-entity list/query: walk every entity of a type instead of addressing one by id.
public static class ListCartsClient
{
    [Function(nameof(ListCartsClient))]
    public static async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "carts")]
            HttpRequestData req,
        [DurableClient] DurableTaskClient client)
    {
        // EntityQuery filters by instance-id prefix (the "@name@key" form) and asks for state.
        var query = new EntityQuery
        {
            InstanceIdStartsWith = $"@{nameof(ShoppingCart).ToLowerInvariant()}@",
            IncludeState = true,
        };

        var carts = new List<object>();
        AsyncPageable<EntityMetadata<List<CartLine>>> results =
            client.Entities.GetAllEntitiesAsync<List<CartLine>>(query);

        await foreach (EntityMetadata<List<CartLine>> cart in results)
        {
            decimal total = cart.State.Sum(l => l.UnitPrice * l.Quantity);
            carts.Add(new { id = cart.Id.ToString(), lineCount = cart.State.Count, total });
        }

        HttpResponseData response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        await response.WriteAsJsonAsync(carts);
        return response;
    }
}
