using DurableFunctionsDemo;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Testing;

namespace DurableFunctionsDemo.Tests;

// Integration tests register the production orchestrator and activity logic with an
// in-memory host and run the real engine: real replay, real serialization, real
// activity execution. No Azure Storage, no emulator, no sidecar. This is the layer
// that catches determinism and round-trip bugs a mocked-context test cannot.
public class OrderWorkflowIntegrationTests
{
    private static Task<DurableTaskTestHost> StartHostAsync() =>
        DurableTaskTestHost.StartAsync(tasks =>
        {
            // Register with the typed-input overload so the host deserializes the
            // scheduled input to OrderRequest; the orchestrator then reads it back
            // through context.GetInput<OrderRequest>() exactly as it does in production.
            tasks.AddOrchestratorFunc<OrderRequest, string>(
                nameof(OrderOrchestrator),
                (ctx, _) => OrderOrchestrator.RunOrchestrator(ctx));

            // Register the real activity logic by name so the orchestrator's
            // CallActivityAsync calls execute against production code.
            tasks.AddActivityFunc<OrderRequest, bool>(
                nameof(ValidateOrderActivity), (_, order) => ValidateOrderActivity.Run(order));
            tasks.AddActivityFunc<OrderRequest, string>(
                nameof(CreateOrderActivity), (_, order) => CreateOrderActivity.Run(order));
            tasks.AddActivityFunc<string>(
                nameof(SendConfirmationActivity), (_, _) => { /* side-effect only */ });
        });

    [Fact]
    public async Task Valid_order_completes_with_order_id()
    {
        await using DurableTaskTestHost host = await StartHostAsync();

        string instanceId = await host.Client.ScheduleNewOrchestrationInstanceAsync(
            nameof(OrderOrchestrator), new OrderRequest("cust-1", "sku-9", 2));

        OrchestrationMetadata result = await host.Client.WaitForInstanceCompletionAsync(
            instanceId, getInputsAndOutputs: true);

        Assert.Equal(OrchestrationRuntimeStatus.Completed, result.RuntimeStatus);
        Assert.StartsWith("ORD-cust-1-sku-9", result.ReadOutputAs<string>());
    }

    [Fact]
    public async Task Invalid_order_completes_with_validation_failure()
    {
        await using DurableTaskTestHost host = await StartHostAsync();

        string instanceId = await host.Client.ScheduleNewOrchestrationInstanceAsync(
            nameof(OrderOrchestrator), new OrderRequest("cust-1", "", 0));

        OrchestrationMetadata result = await host.Client.WaitForInstanceCompletionAsync(
            instanceId, getInputsAndOutputs: true);

        Assert.Equal(OrchestrationRuntimeStatus.Completed, result.RuntimeStatus);
        Assert.Equal("Order validation failed", result.ReadOutputAs<string>());
    }
}
