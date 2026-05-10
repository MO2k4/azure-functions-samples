using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Settlement.Core.Services;

public static class ServiceCollectionExtensions
{
    // Registers the cross-cutting workload services. Each host (Function App,
    // App Service, Container App) binds SettlementOptions and selects the
    // ISettlementGateway implementation in its own Program.cs, since the
    // composition root is host-specific.
    public static IServiceCollection AddSettlementCore(this IServiceCollection services)
    {
        services.TryAddSingleton<IPaymentSettler, PaymentSettler>();
        return services;
    }
}
