using System.Text.Json;
using Azure;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Options;
using Settlement.AppService.Configuration;
using Settlement.Core.Models;
using Settlement.Core.Services;

namespace Settlement.AppService.Services;

// Always-on background loop that drains the same queue the Function App reads.
// On App Service the host stays warm, so there is no per-invocation timeout
// to fight: a batch that takes 25 minutes runs to completion. The trade is
// that you pay for the worker whether the queue is empty or full.
public sealed class SettlementWorker(
    QueueClient queueClient,
    IPaymentSettler settler,
    IOptions<QueueOptions> queueOptions,
    SettlementWorkerStatus status,
    ILogger<SettlementWorker> logger) : BackgroundService
{
    private readonly QueueOptions _options = queueOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await queueClient.CreateIfNotExistsAsync(cancellationToken: stoppingToken);
        logger.LogInformation("Settlement worker started against queue {Queue}", _options.QueueName);

        while (!stoppingToken.IsCancellationRequested)
        {
            QueueMessage[] messages;
            try
            {
                var response = await queueClient.ReceiveMessagesAsync(
                    maxMessages: _options.MaxBatchMessages,
                    visibilityTimeout: TimeSpan.FromSeconds(_options.VisibilityTimeoutSeconds),
                    cancellationToken: stoppingToken);
                messages = response.Value;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (RequestFailedException ex)
            {
                logger.LogError(ex, "Failed to receive messages from {Queue}", _options.QueueName);
                await Task.Delay(_options.IdlePollingDelayMs, stoppingToken);
                continue;
            }

            if (messages.Length == 0)
            {
                await Task.Delay(_options.IdlePollingDelayMs, stoppingToken);
                continue;
            }

            foreach (var message in messages)
            {
                await ProcessAsync(message, stoppingToken);
            }
        }

        logger.LogInformation("Settlement worker stopping");
    }

    private async Task ProcessAsync(QueueMessage message, CancellationToken cancellationToken)
    {
        SettlementBatch? batch;
        try
        {
            batch = JsonSerializer.Deserialize<SettlementBatch>(message.Body.ToString());
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Discarding malformed message {MessageId}", message.MessageId);
            await queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, cancellationToken);
            return;
        }

        if (batch is null)
        {
            await queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, cancellationToken);
            return;
        }

        var progress = new Progress<SettlementProgress>(p => status.Update(batch.BatchId, p));
        try
        {
            var result = await settler.SettleAsync(batch, progress, cancellationToken);
            status.Complete(batch.BatchId, result);
            await queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Process is shutting down; leave the message visible for the next instance.
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Settlement of batch {BatchId} failed; message will reappear after visibility timeout", batch.BatchId);
        }
    }
}
