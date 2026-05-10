# BatchCheckpointDemo

Companion sample for the W20 article *When you've outgrown Azure Functions* (Monday tip: 10-minute timeout + checkpoint pattern).

A long-running queue-triggered function processes a batch in chunks and writes a cursor blob after each chunk. If the worker restarts mid-batch (timeout, OOM, scale-in), the next attempt reads the cursor and resumes after the last committed chunk instead of starting from item 1.

## What the sample shows

- `DrainBatchFunction` — queue trigger that loops chunks of work and commits a cursor before moving to the next chunk.
- `FakeBatchSource` — async stream of items, configurable size and per-chunk delay, so the function looks long-running without needing a real backend.
- `Program.cs` — registers the singleton `BlobContainerClient` and `IBatchSource`.

The pattern is independent of the trigger; the same shape works with `ServiceBusTrigger` or `EventHubTrigger`. The article's snippet uses `ServiceBusTrigger`; this sample uses Storage Queue so it runs end-to-end against Azurite without extra infrastructure.

## Run locally

Prerequisites: .NET 10 SDK, Azure Functions Core Tools v4, Azurite running on the default ports.

```bash
cp local.settings.json.example local.settings.json
func start
```

Then enqueue a batch command (one per batch you want to drain):

```bash
az storage message put \
  --queue-name batches \
  --connection-string "UseDevelopmentStorage=true" \
  --content '{"BatchId":"b-001","TotalItems":10000}'
```

Watch the logs: the function will commit `Committed batch b-001 up to item N` on every chunk. Kill the host (Ctrl-C) part-way through and re-run; the cursor blob `batch-cursors/batch-b-001.cursor` keeps the last `N`, and the next invocation resumes from there.

## Configuration

| Setting           | Default | Purpose                                              |
| ----------------- | ------- | ---------------------------------------------------- |
| `BatchSize`       | 500     | Items per chunk                                      |
| `TotalItems`      | 10000   | Items emitted by the fake source                     |
| `PerChunkDelayMs` | 200     | Sleep between chunks, used to simulate I/O latency   |

## Why this matters

Without the cursor, every retry restarts at item 1. On the Consumption plan with the 10-minute hard timeout, a batch that takes 9:55 succeeds; the same batch taking 10:05 fails, retries from scratch, and fails again. With the cursor, the second attempt picks up at item N, runs another nine minutes, and finishes. Same code, different completion guarantee.
