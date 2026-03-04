# Design: Testcontainers Integration Tests for Middleware

**Date:** 2026-03-04
**Project:** HttpTriggerDemo (Azure Functions isolated worker model)
**Status:** Approved

## Goal

Add two middleware implementations to the HttpTriggerDemo function app and verify them with real end-to-end integration tests. Tests run the function app inside a Docker container managed by Testcontainers, making actual HTTP requests — no mocking of the Functions runtime.

## Scope

### New production code (`HttpTriggerDemo/`)

| File | Purpose |
|------|---------|
| `Middleware/CorrelationIdMiddleware.cs` | Reads `X-Correlation-Id` from the request; generates a new GUID if absent; writes the value to the response header |
| `Middleware/ExceptionHandlingMiddleware.cs` | Wraps `next(context)` in try/catch; logs the exception; returns HTTP 500 with an empty body (no exception details leaked) |
| `ErrorFunction.cs` | HTTP trigger at `/api/error` that throws unconditionally — used to exercise the exception handler |
| `Program.cs` | Updated to register both middleware |
| `Dockerfile` | Multi-stage build producing a runnable Azure Functions container |

### New test code (`HttpTriggerDemo.Tests/`)

| File | Purpose |
|------|---------|
| `FunctionAppContainerFixture.cs` | xUnit `IAsyncLifetime` collection fixture that builds and starts the Docker container |
| `CorrelationIdMiddlewareTests.cs` | Two tests: generated ID when header absent; provided ID echoed back |
| `ExceptionHandlingMiddlewareTests.cs` | Two tests: HTTP 500 returned; response body is empty |

## Architecture

### Dockerfile (in `HttpTriggerDemo/`)

Multi-stage build:

```
Stage 1 — build:  mcr.microsoft.com/dotnet/sdk:10.0
  COPY . /src
  dotnet publish -o /app/publish

Stage 2 — runtime:  mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated10.0
  ENV AzureWebJobsScriptRoot=/home/site/wwwroot
  ENV AzureFunctionsJobHost__Logging__Console__IsEnabled=true
  COPY --from=build /app/publish /home/site/wwwroot
```

The build context is the `HttpTriggerDemo/` folder. No `../` paths needed.

### Testcontainers fixture

```
FunctionAppContainerFixture (IAsyncLifetime, xUnit collection fixture)
  ├── Builds image via ImageFromDockerfileBuilder
  │     └── Path resolved with CommonDirectoryPath.GetSolutionDirectory()
  ├── Binds container port 80 → random host port
  ├── Wait strategy: HTTP probe on GET /api/hello (port 80)
  │     └── Default: 1 s retry, 60 s timeout
  ├── InitializeAsync() → StartAsync()
  ├── DisposeAsync() → DisposeAsync()
  └── CreateClient() → HttpClient at mapped host port
```

Tests use `[Collection(FunctionAppContainerFixture.Name)]` to share the single container instance across all middleware test classes.

### Middleware registration order in `Program.cs`

```csharp
builder.UseMiddleware<ExceptionHandlingMiddleware>(); // outermost — catches all
builder.UseMiddleware<CorrelationIdMiddleware>();     // innermost — per-request header
```

## Middleware behaviour

### CorrelationIdMiddleware

- Request header present → use value as-is
- Request header absent → generate `Guid.NewGuid().ToString()`
- Response header always set (tests can always assert on it)

### ExceptionHandlingMiddleware

- Catches `Exception` (base type)
- Response: HTTP 500, empty body, no `Content-Type`
- Logs at `LogLevel.Error` with the exception (visible in container stdout on test failure)
- Does not rethrow — pipeline must complete normally

## Packages

Add to `Directory.Packages.props` and `HttpTriggerDemo.Tests.csproj`:

- `Testcontainers` — `ContainerBuilder`, `ImageFromDockerfileBuilder`, `Wait`

## Testing constraints

- Docker must be running locally and in CI
- Image is built once per test session (Testcontainers caches by Dockerfile content hash)
- No external backing services required — all middleware logic is self-contained
