using Aspire.Hosting.Azure;
using AspireDemo.AppHost;
using Azure.Provisioning;
using Azure.Provisioning.Primitives;
using Azure.Provisioning.AppContainers;
using Azure.Provisioning.OperationalInsights;
using Azure.Provisioning.Roles;
using Microsoft.Extensions.DependencyInjection;

var builder = DistributedApplication.CreateBuilder(args);

// W24: billing tags (cost-center, env) on every Azure resource, declared once in C#
// instead of edited into each generated .module.bicep. The resolver participates in
// Azure.Provisioning's Bicep generation, so `azd infra gen` re-emits the tags on
// every regeneration; hand-edits to infra/ would be lost on the next one.
builder.Services.Configure<AzureProvisioningOptions>(options =>
    options.ProvisioningBuildOptions.InfrastructureResolvers.Insert(0, new AzureTagResolver()));

// Host storage (W22 baseline): the connection Functions needs for its own bookkeeping
// (lease blobs, timer state, queue scaling controller), injected as AzureWebJobsStorage.
var hostStorage = builder.AddAzureStorage("host-storage").RunAsEmulator();

// Application storage, separate from the runtime's bookkeeping. Same Azurite emulator
// API, a distinct resource so receipts don't commingle with host-storage. AddBlobs is
// the service-level handle; WithReference(receipts) below auto-wires the BlobOutput.
var appStorage = builder.AddAzureStorage("app-storage").RunAsEmulator();
var receipts = appStorage.AddBlobs("receipts");

// Service Bus as a resource. RunAsEmulator() pulls the emulator container plus a SQL
// backing container, generates the SA password, and accepts the EULA for both. The
// "orders" queue is pre-provisioned from this declaration via the generated Config.json.
// In publish mode RunAsEmulator() is a no-op, so the same line provisions a real namespace.
var messaging = builder.AddAzureServiceBus("messaging").RunAsEmulator();
messaging.AddServiceBusQueue("orders");

// Redis as a local container. At publish this deploys containerized Redis on ACA; for a
// managed cache the current API is AddAzureManagedRedis (AddAzureRedis is obsolete in 13.x).
var cache = builder.AddRedis("cache");

// W24 Part 3: the publish target. AddAzureContainerAppEnvironment declares the Azure Container
// Apps environment that every project and containerized resource above is published into. It is
// inert locally (RunAsEmulator still drives the dev inner loop); it only materialises at
// publish/deploy. Required since Aspire 9.4, which removed the implicit azd-owned ACA environment:
// without this line `aspire publish` / `azd` have no compute environment to target.
builder.AddAzureContainerAppEnvironment("aca-env")
    // The environment module's resources (managed env, Log Analytics, its identity)
    // bind Tags to the module-level `tags` parameter, an expression the resolver
    // can't add entries to. Rebinding Tags to a literal covers all three; the
    // parameter stays declared but unused (azd never passes it).
    .ConfigureInfrastructure(infra =>
    {
        var resources = infra.GetProvisionableResources().ToList();
        var taggable = resources.OfType<ContainerAppManagedEnvironment>().Cast<ProvisionableResource>()
            .Concat(resources.OfType<OperationalInsightsWorkspace>())
            .Concat(resources.OfType<UserAssignedIdentity>());
        foreach (var resource in taggable)
        {
            resource.GetType().GetProperty("Tags")?.SetValue(resource, new BicepDictionary<string>
            {
                ["cost-center"] = "GAZE",
                ["owner"] = "AZE",
                ["environment"] = "learning",
                ["project"] = "aspire-demo",
            });
        }
    });

builder.AddAzureFunctionsProject<Projects.OrderProcessor_Http>("orders-http")
    .WithHostStorage(hostStorage);

builder.AddAzureFunctionsProject<Projects.OrderProcessor_Queue>("orders-queue")
    .WithHostStorage(hostStorage);

// The Service Bus consumer. Three references, three resolution paths:
//   WithReference(messaging, "messaging") -> auto-wires [ServiceBusTrigger(Connection = "messaging")]
//   WithReference(receipts, "receipts")   -> auto-wires [BlobOutput(Connection = "receipts")]
//   WithReference(cache)                  -> serves AddRedisClient("cache") / IConnectionMultiplexer
builder.AddAzureFunctionsProject<Projects.OrderProcessor_ServiceBus>("orders-sb")
    .WithHostStorage(hostStorage)
    .WithReference(messaging, "messaging")
    .WithReference(receipts, "receipts")
    .WithReference(cache);

builder.Build().Run();
