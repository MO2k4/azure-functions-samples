var builder = DistributedApplication.CreateBuilder(args);

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
