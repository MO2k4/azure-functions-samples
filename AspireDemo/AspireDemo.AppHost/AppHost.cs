var builder = DistributedApplication.CreateBuilder(args);

var hostStorage = builder.AddAzureStorage("host-storage").RunAsEmulator();

builder.AddAzureFunctionsProject<Projects.OrderProcessor_Http>("orders-http")
    .WithHostStorage(hostStorage);

builder.AddAzureFunctionsProject<Projects.OrderProcessor_Queue>("orders-queue")
    .WithHostStorage(hostStorage);

builder.Build().Run();
