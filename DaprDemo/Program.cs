using Dapr.Client;
using DaprDemo.Orders;

var builder = WebApplication.CreateBuilder(args);

// The app never links a Dapr library to reach Redis, Cosmos DB, or Service Bus. It talks to
// the daprd sidecar over loopback, and the sidecar talks to the backing service. AddDaprClient
// reads DAPR_HTTP_PORT / DAPR_GRPC_PORT from the environment (dapr run sets them), so nothing
// here hardcodes 3500 or 50001.
builder.Services.AddDaprClient();

// Service invocation runs over an ordinary HttpClient. DaprClient.InvokeMethodAsync and its
// gRPC siblings have carried [Obsolete] since Dapr .NET SDK 1.17: "Recommended guidance is to
// use a native HTTP or gRPC client for service invocation." CreateInvokeHttpClient is the
// supported replacement. It sets BaseAddress to http://<appId> and installs the handler that
// routes the call through the sidecar.
//
// One HttpClient per target app ID is the documented pattern (an app ID containing an
// uppercase letter only works when it is passed here), so the client is registered under the
// app ID as its DI key.
builder.Services.AddKeyedSingleton<HttpClient>(
    OrderEndpoints.ShippingAppId,
    (_, key) => DaprClient.CreateInvokeHttpClient(appId: (string)key!));

var app = builder.Build();

// Everything published through Dapr is wrapped in a CloudEvent envelope, with the payload
// nested under "data". UseCloudEvents unwraps it so the subscriber binds the order event
// itself rather than the envelope.
app.UseCloudEvents();

// Serves GET /dapr/subscribe. That is how the sidecar discovers the .WithTopic() endpoint
// below at startup. Programmatic subscriptions are read once, at startup only.
app.MapSubscribeHandler();

app.MapOrderEndpoints();

await app.RunAsync();
