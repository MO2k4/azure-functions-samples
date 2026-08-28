using CommunityToolkit.Aspire.Hosting.Dapr;

var builder = DistributedApplication.CreateBuilder(args);

// Optional, and the sample was run both ways to be sure: comment this line out and both
// *-dapr-cli resources still start, POST /orders still returns 201, and the sidecar still reports
// state.redis on /v1.0/metadata. WithDaprSidecar() calls builder.ApplicationBuilder.AddDapr()
// itself, and AddDapr registers its lifecycle hook through TryAddEventingSubscriber, so the
// second call is a no-op.
//
// What the explicit call is actually for is the configure overload: DaprOptions is the only way
// to point at a Dapr CLI that is not on PATH (DaprPath), turn sidecar telemetry off
// (EnableTelemetry), or hook the publish step. The sample calls it with no callback because this
// is the line that names the dependency; treat it as documentation, not as wiring.
//
// There is no builder.AddDaprSidecar("name"), whatever the blog posts say; the compiler answers
// CS1061. Sidecars attach to a resource, they are never declared standalone.
builder.AddDapr();

// The state store as an Aspire resource. The name is the Dapr component name: it is the string
// order-service passes to SaveStateAsync, and the only thing the application knows about its own
// database.
//
// LocalPath is not optional in practice. Leave it out and the integration writes its own
// component with `spec.type: state.in-memory` into a temp folder, then passes that folder as
// --resources-path. That flag replaces the sidecar's default --components-path, so the machine's
// own ~/.dapr/components is not merged in and every write is lost when the sidecar restarts.
var stateStore = builder.AddDaprStateStore(
    "statestore",
    new DaprComponentOptions
    {
        LocalPath = Path.Combine(builder.AppHostDirectory, "..", "components", "statestore.yaml"),
    });

// The invocation target. It has no Dapr package reference and no Dapr type in its source: being
// reachable as "inventory-service" is a property of the sidecar declared here, not of the code.
var inventory = builder.AddProject<Projects.DaprAspireDemo_InventoryService>("inventory-service")
    .WithDaprSidecar(sidecar => sidecar.WithOptions(new DaprSidecarOptions
    {
        // The app ID is the address. Omit it and the sidecar takes the resource name, which
        // happens to be identical here; spelling it out is what stops a later rename of the
        // Aspire resource from silently breaking every caller.
        AppId = "inventory-service",
    }));

// The caller. The state store reference goes on the SIDECAR builder, not on the project builder.
// project.WithDaprSidecar().WithReference(component) still compiles, but it carries [Obsolete]
// ("Add reference to the sidecar resource instead of the project resource") and this repo builds
// with TreatWarningsAsErrors, so CS0618 fails the build. The package README for 13.0.0 still
// shows the obsolete form.
var orders = builder.AddProject<Projects.DaprAspireDemo_OrderService>("order-service")
    .WithDaprSidecar(sidecar => sidecar
        .WithOptions(new DaprSidecarOptions
        {
            AppId = "order-service",
        })
        .WithReference(stateStore));

// Ordinary Aspire orchestration, unrelated to Dapr: it holds order-service back until
// inventory-service reports healthy, which is what removes the ERR_DIRECT_INVOKE window at
// startup. The HTTP call between them still goes over Dapr, not over service discovery.
orders.WaitFor(inventory);

builder.Build().Run();
