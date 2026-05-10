# HttpClientFactoryDemo

Companion sample for the W20 article *When you've outgrown Azure Functions* (Wednesday tip: `IHttpClientFactory` and typed clients in the isolated worker).

A function calls a downstream HTTP API through a typed client registered with `IHttpClientFactory`. Handlers are pooled, sockets recycle on rotation, and the standard resilience handler adds timeouts, retries, and a circuit breaker without any custom code.

## What the sample shows

- `Program.cs` — `AddHttpClient<IPaymentsApi, PaymentsApi>(...)` registers the typed client; `AddStandardResilienceHandler()` adds the canonical Microsoft resilience pipeline.
- `PaymentsApi` — the typed client, owns the `HttpClient` lifetime through the factory.
- `AuthorizePaymentFunction` — HTTP-triggered function (ASP.NET Core integration), takes the typed client by constructor injection.
- `PaymentsOptions` — bound from configuration with data-annotation validation; fails fast on startup if `BaseAddress` is missing.

The article snippet uses a custom `SetHandlerLifetime` override to make the lifetime explicit. This sample uses the framework default (2 minutes) and adds the standard resilience handler instead, because that is the current Microsoft guidance for transient-fault handling in .NET 10. Both are correct; pick the one that matches your team's observability story.

## Run locally

```bash
cp local.settings.json.example local.settings.json
func start
```

Then call the function:

```bash
curl -i -X POST http://localhost:7071/api/payments \
  -H 'Content-Type: application/json' \
  -d '{"OrderId":"o-1","CustomerId":"c-1","Amount":42.00,"Currency":"EUR"}'
```

The call will fail against `https://payments.example.com/` (the example URL); replace `Payments__BaseAddress` in `local.settings.json` with a real endpoint or a [WireMock.Net](https://github.com/WireMock-Net/WireMock.Net) instance to see the success path.

## Why this matters

Three fail modes that `new HttpClient()` produces inside a function:

1. **Socket exhaustion under load.** Each `new HttpClient()` opens its own connection pool. Functions that scale out and instantiate per-call run the host out of ephemeral ports.
2. **Stale DNS.** A long-lived `HttpClient` caches DNS forever; rotating an upstream IP needs a worker recycle.
3. **No layered policies.** Timeouts and retries end up scattered through the function bodies instead of registered once and reused.

The factory plus the resilience handler removes all three. The function code stays focused on what it actually does (call the payments API, log, return).
