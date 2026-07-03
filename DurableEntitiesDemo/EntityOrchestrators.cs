using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Entities;

namespace DurableEntitiesDemo;

// The orchestrator is the only place you get request/response with an entity: call to
// read a value, signal to change it fire-and-forget.
public static class CounterOrchestrator
{
    [Function(nameof(CounterOrchestrator))]
    public static async Task<int> Run(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var id = new EntityInstanceId(nameof(Counter), "myCounter");

        int current = await context.Entities.CallEntityAsync<int>(id, "Get");   // two-way
        if (current < 10)
            await context.Entities.SignalEntityAsync(id, "Add", 1);             // one-way

        return current;
    }
}

public record TransferRequest(string From, string To, decimal Amount);

// Multi-entity critical section: lock both accounts so no other caller observes a
// mid-transfer state. The lock is released when the returned scope is disposed.
public static class TransferOrchestrator
{
    [Function(nameof(TransferOrchestrator))]
    public static async Task<bool> Run(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        TransferRequest request = context.GetInput<TransferRequest>()!;
        var from = new EntityInstanceId(nameof(BankAccount), request.From);
        var to = new EntityInstanceId(nameof(BankAccount), request.To);

        // LockEntitiesAsync returns an IAsyncDisposable; the section ends on dispose.
        // Inside the lock you may only call the locked entities, never signal them.
        await using (await context.Entities.LockEntitiesAsync(from, to))
        {
            bool withdrew = await context.Entities.CallEntityAsync<bool>(
                from, nameof(BankAccount.Withdraw), request.Amount);
            if (!withdrew)
                return false;   // insufficient funds; no compensation needed, nothing moved

            await context.Entities.CallEntityAsync(
                to, nameof(BankAccount.Deposit), request.Amount);
            return true;
        }
    }
}
