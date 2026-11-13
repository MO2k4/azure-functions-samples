using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.Services.AddSingleton(_ =>
    new BlobContainerClient(builder.Configuration["AzureWebJobsStorage"], "invoices"));

builder.Build().Run();
