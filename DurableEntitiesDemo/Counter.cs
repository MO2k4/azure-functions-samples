using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Entities;

namespace DurableEntitiesDemo;

// The canonical class-based entity: derive from TaskEntity<TState>, operations are
// public methods, and one [Function]/[EntityTrigger] method dispatches to them.
// Only this.State is serialized; nothing else on the class survives between operations.
public class Counter : TaskEntity<int>
{
    public void Add(int amount) => this.State += amount;

    public Task Reset()
    {
        this.State = 0;
        return Task.CompletedTask;
    }

    public Task<int> Get() => Task.FromResult(this.State);

    // Never name this RunAsync: ITaskEntity already defines an instance RunAsync, and the
    // collision throws an ambiguous-match error at dispatch time. Run (or any other name) is fine.
    [Function(nameof(Counter))]
    public static Task Run([EntityTrigger] TaskEntityDispatcher dispatcher)
        => dispatcher.DispatchAsync<Counter>();
}
