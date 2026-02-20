# Azure Functions Samples

Companion code for the **Azure Functions for .NET Developers** series on dev.to.

## Projects

| Project | Article |
|---------|---------|
| [HttpTriggerDemo](./HttpTriggerDemo) | [Your First Azure Function: HTTP Triggers Step-by-Step](https://dev.to/martin_oehlert/your-first-azure-function-http-triggers-step-by-step-ib8) |
| [TriggerDemo](./TriggerDemo) | [Beyond HTTP: Timer, Queue, and Blob Triggers](https://dev.to/martin_oehlert/beyond-http-timer-queue-and-blob-triggers) |

## Prerequisites

- [.NET 10 SDK](https://dot.net/download) (LTS)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
- [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) (local storage emulator — required for all projects)

## Getting Started

### HttpTriggerDemo (Part 2 — HTTP Triggers)

```bash
cd HttpTriggerDemo
cp local.settings.json.example local.settings.json
npx azurite --silent --location /tmp/azurite &
func start
```

Three functions will start:

- `GET  /api/Hello?name=Azure` — query string pattern
- `POST /api/orders` — JSON body with `[FromBody]`
- `GET  /api/products/{category}/{id?}` — route parameters

### TriggerDemo (Part 3 — Timer, Queue, and Blob Triggers)

```bash
cd TriggerDemo
cp local.settings.json.example local.settings.json
npx azurite --silent --location /tmp/azurite &
func start
```

Three functions will start:

- `CleanupFunction` — timer trigger on the `CLEANUP_SCHEDULE` app setting (defaults to every minute in the example settings)
- `OrderProcessor` — queue trigger on the `orders` queue; writes a notification to the `notifications` queue
- `ImageProcessor` — blob trigger on the `uploads` container; writes output to the `thumbnails` container

**Testing locally:**

```bash
# Fire the timer function immediately (no need to wait for the schedule)
curl -X POST http://localhost:7071/admin/functions/CleanupFunction \
  -H "Content-Type: application/json" \
  -d '{"input": null}'

# Put a message on the orders queue
az storage message put \
  --queue-name orders \
  --content '{"OrderId":"ORD-001","CustomerId":"C42","Amount":129.99}' \
  --connection-string "UseDevelopmentStorage=true"

# Upload a file to trigger the blob function
az storage container create --name uploads --connection-string "UseDevelopmentStorage=true"
az storage blob upload \
  --container-name uploads \
  --name test-image.png \
  --file ./test-image.png \
  --connection-string "UseDevelopmentStorage=true"
```

## License

MIT
