using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Entities;

namespace DurableEntitiesDemo;

// Two of these are locked together in a critical section for the atomic transfer.
public class BankAccount : TaskEntity<decimal>
{
    public void Deposit(decimal amount) => this.State += amount;

    public bool Withdraw(decimal amount)
    {
        if (this.State < amount)
            return false;

        this.State -= amount;
        return true;
    }

    public Task<decimal> GetBalance() => Task.FromResult(this.State);

    [Function(nameof(BankAccount))]
    public static Task Run([EntityTrigger] TaskEntityDispatcher dispatcher)
        => dispatcher.DispatchAsync<BankAccount>();
}
