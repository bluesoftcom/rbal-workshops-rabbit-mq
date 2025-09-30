using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;

namespace InboxOutboxPattern;

// Outbox Message Processor - Publishes messages from outbox to RabbitMQ
public class OutboxMessageProcessor
{
    private readonly InboxOutboxDbContext _dbContext;
    private readonly ConnectionFactory _rabbitMqFactory;
    private readonly Timer _processingTimer;
    private readonly object _lockObject = new object();
    private bool _isProcessing = false;

    public OutboxMessageProcessor(InboxOutboxDbContext dbContext, ConnectionFactory rabbitMqFactory)
    {
        _dbContext = dbContext;
        _rabbitMqFactory = rabbitMqFactory;
        _processingTimer = new Timer(ProcessOutboxMessages, null, Timeout.Infinite, Timeout.Infinite);
    }

    public async Task StartProcessingAsync()
    {
        await SetupRabbitMQInfrastructureAsync();
        _processingTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(5)); // Process every 5 seconds
        Console.WriteLine("Outbox processor started - checking every 5 seconds");
    }

    public Task StopProcessingAsync()
    {
        _processingTimer.Change(Timeout.Infinite, Timeout.Infinite);
        Console.WriteLine("Outbox processor stopped");
        return Task.CompletedTask;
    }

    private async Task SetupRabbitMQInfrastructureAsync()
    {
        using var connection = await _rabbitMqFactory.CreateConnectionAsync();
        using var channel = await connection.CreateChannelAsync();

        // Declare exchange
        await channel.ExchangeDeclareAsync(
            exchange: "orders.exchange",
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false
        );

        // Declare queues for demonstration
        await channel.QueueDeclareAsync(
            queue: "order.processing",
            durable: true,
            exclusive: false,
            autoDelete: false
        );

        await channel.QueueBindAsync("order.processing", "orders.exchange", "order.created");
        
        Console.WriteLine("RabbitMQ infrastructure setup completed");
    }

    private async void ProcessOutboxMessages(object? state)
    {
        lock (_lockObject)
        {
            if (_isProcessing) return;
            _isProcessing = true;
        }

        try
        {
            using var dbContext = new InboxOutboxDbContext(_dbContext.Database.GetConnectionString()!);
            
            var unprocessedMessages = await dbContext.OutboxMessages
                .Where(m => !m.IsProcessed && m.RetryCount < 3)
                .OrderBy(m => m.CreatedAt)
                .Take(10)
                .ToListAsync();

            if (unprocessedMessages.Any())
            {
                Console.WriteLine($"Processing {unprocessedMessages.Count} outbox messages...");
                
                using var connection = await _rabbitMqFactory.CreateConnectionAsync();
                using var channel = await connection.CreateChannelAsync();

                foreach (var message in unprocessedMessages)
                {
                    try
                    {
                        await PublishMessageAsync(channel, message);
                        
                        // Mark as processed
                        message.IsProcessed = true;
                        message.ProcessedAt = DateTime.UtcNow;
                        
                        Console.WriteLine($"Published message {message.Id}: {message.EventType}");
                    }
                    catch (Exception ex)
                    {
                        message.RetryCount++;
                        message.ErrorMessage = ex.Message;
                        Console.WriteLine($"Failed to publish message {message.Id}: {ex.Message}");
                    }
                }

                await dbContext.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in outbox processor: {ex.Message}");
        }
        finally
        {
            lock (_lockObject)
            {
                _isProcessing = false;
            }
        }
    }

    private async Task PublishMessageAsync(IChannel channel, OutboxMessage outboxMessage)
    {
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            MessageId = Guid.NewGuid().ToString(),
            CorrelationId = outboxMessage.CorrelationId,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            Headers = new Dictionary<string, object?>
            {
                { "event-type", outboxMessage.EventType },
                { "aggregate-id", outboxMessage.AggregateId },
                { "created-at", outboxMessage.CreatedAt.ToString("O") },
                { "source", "outbox-pattern" }
            }
        };

        var body = Encoding.UTF8.GetBytes(outboxMessage.Payload);

        await channel.BasicPublishAsync(
            exchange: outboxMessage.Exchange,
            routingKey: outboxMessage.RoutingKey,
            mandatory: false,
            basicProperties: properties,
            body: body
        );
    }
}