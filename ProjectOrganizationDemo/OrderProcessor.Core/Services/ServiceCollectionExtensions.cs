using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrderProcessor.Core.Stores;
using OrderProcessor.Core.Validators;

namespace OrderProcessor.Core.Services;

public static class ServiceCollectionExtensions
{
    // Registers the cross-cutting services every consumer (Http app, Queue app, tests) needs.
    // Function-app-specific things (IOrderStore impl selection, options binding) stay in Program.cs.
    public static IServiceCollection AddOrderServices(this IServiceCollection services)
    {
        services.TryAddSingleton<OrderValidator>();

        services.TryAddKeyedSingleton<IOrderStore, SqlOrderStore>(OrderStoreKeys.Sql);
        services.TryAddKeyedSingleton<IOrderStore, CosmosOrderStore>(OrderStoreKeys.Cosmos);

        return services;
    }
}
