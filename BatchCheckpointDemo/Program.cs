using Azure.Storage.Blobs;
using BatchCheckpointDemo;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.Services.AddSingleton(sp =>
{
    var connection = sp.GetRequiredService<IConfiguration>().GetValue<string>("AzureWebJobsStorage")
        ?? throw new InvalidOperationException("AzureWebJobsStorage is required");
    return new BlobContainerClient(connection, "batch-cursors");
});

builder.Services.AddSingleton<IBatchSource, FakeBatchSource>();

builder.Build().Run();
