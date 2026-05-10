using System.Text.Json;
using Azure;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Options;
using Settlement.ContainerApp.Configuration;
using Settlement.Core.Models;
using Settlement.Core.Services;

namespace Settlement.ContainerApp;

// Container Apps deployment: this worker is packaged in a Linux container and
// scaled by a KEDA queue trigger. The Container App scales to zero when the
// queue is empty and back up when messages arrive. Per-invocation timeout is
// gone (the worker runs until it cancels), and you only pay for execution
// seconds plus a small idle baseline.
//
// Code-wise this is the same loop as the App Service BackgroundService, minus
// the web host. That difference is the point: the App Service variant is
// paying for a web server it does not need; the Container App variant is not.
public sealed class SettlementWorker(
    QueueClient queueClient,
    IPaymentSettler settler,
    IOptions<QueueOptions> queueOptions,
    ILogger<SettlementWorker> logger) : BackgroundService
{
    private readonly QueueOptions _options = queueOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await queueClient.CreateIfNotExistsAsync(cancellationToken: stoppingToken);
        logger.LogInformation(
            "Settlement worker started against queue {Queue}",
            _options.QueueName);

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

        try
        {
            await settler.SettleAsync(batch, progress: null, cancellationToken);
            await queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Settlement of batch {BatchId} failed; message will reappear after visibility timeout", batch.BatchId);
        }
    }
}
