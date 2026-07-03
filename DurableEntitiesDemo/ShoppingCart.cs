using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Entities;

namespace DurableEntitiesDemo;

// Entity state DTO as a positional record (C# 14). Round-trips through System.Text.Json
// as entity state; see ShoppingCartSerializationTests for the proof.
public record CartLine(string Sku, int Quantity, decimal UnitPrice);

// Same shape as Counter, but the state grew from an int to a List<CartLine>. The
// operation-per-method model and the single trigger did not change.
public class ShoppingCart : TaskEntity<List<CartLine>>
{
    protected override List<CartLine> InitializeState(TaskEntityOperation entityOperation)
        => new();

    public void AddItem(CartLine line) => this.State.Add(line);

    public void RemoveItem(string sku) => this.State.RemoveAll(l => l.Sku == sku);

    public Task<decimal> GetTotal() =>
        Task.FromResult(this.State.Sum(l => l.UnitPrice * l.Quantity));

    [Function(nameof(ShoppingCart))]
    public static Task Run([EntityTrigger] TaskEntityDispatcher dispatcher)
        => dispatcher.DispatchAsync<ShoppingCart>();
}
