# Azure Functions Samples

Companion code for the **Azure Functions for .NET Developers** series on dev.to.

## Projects

| Project | Article |
|---------|---------|
| [HttpTriggerDemo](./HttpTriggerDemo) | [Your First Azure Function: HTTP Triggers Step-by-Step](https://dev.to/martin_oehlert/series/your-first-azure-function-http-triggers-step-by-step) |

## Prerequisites

- [.NET 10 SDK](https://dot.net/download) (LTS)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
- [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) (local storage emulator)

## Getting Started

```bash
cd HttpTriggerDemo
cp local.settings.json.example local.settings.json
npx azurite --silent --location /tmp/azurite &
func start
```

Three functions will start:

- `GET  /api/Hello?name=Azure` — query string pattern
- `POST /api/orders` — JSON body pattern
- `GET  /api/products/{category}/{id?}` — route parameters pattern

## License

MIT
