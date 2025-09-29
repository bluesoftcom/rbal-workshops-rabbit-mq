using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Newtonsoft.Json;

namespace InboxOutboxPattern;

// Inbox Consumer - Implements Inbox Pattern for idempotent message processing
public class InboxConsumer
{
    private readonly InboxOutboxDbContext _dbContext;
    private readonly ConnectionFactory _rabbitMqFactory;
    private IConnection? _connection;
    private IChannel? _channel;

    public InboxConsumer(InboxOutboxDbContext dbContext, ConnectionFactory rabbitMqFactory)
    {
        _dbContext = dbContext;
        _rabbitMqFactory = rabbitMqFactory;
    }

    public async Task StartConsumingAsync()
    {
        _connection = await _rabbitMqFactory.CreateConnectionAsync();
        _channel = await _connection.CreateChannelAsync();

        // Setup queue
        await _channel.QueueDeclareAsync(
            queue: "order.processing",
            durable: true,
            exclusive: false,
            autoDelete: false
        );

        // Set QoS to process one message at a time
        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            var messageId = ea.BasicProperties.MessageId ?? Guid.NewGuid().ToString();
            var correlationId = ea.BasicProperties.CorrelationId;
            
            try
            {
                // Check if message already processed (idempotency)
                using var dbContext = new InboxOutboxDbContext(_dbContext.Database.GetConnectionString()!);
                
                var existingMessage = await dbContext.InboxMessages
                    .FirstOrDefaultAsync(m => m.MessageId == messageId);

                if (existingMessage != null && existingMessage.IsProcessed)
                {
                    Console.WriteLine($"Message {messageId} already processed - skipping");
                    await _channel.BasicAckAsync(ea.DeliveryTag, false);
                    return;
                }

                var body = ea.Body.ToArray();
                var payload = Encoding.UTF8.GetString(body);
                var eventType = ea.BasicProperties.Headers?.TryGetValue("event-type", out var eventTypeValue) == true && eventTypeValue is byte[] bytes
                    ? Encoding.UTF8.GetString(bytes) : "Unknown";

                // Store in inbox
                var inboxMessage = new InboxMessage
                {
                    MessageId = messageId,
                    EventType = eventType,
                    Payload = payload,
                    CorrelationId = correlationId,
                    SourceExchange = ea.Exchange,
                    SourceRoutingKey = ea.RoutingKey
                };

                if (existingMessage == null)
                {
                    dbContext.InboxMessages.Add(inboxMessage);
                }
                else
                {
                    existingMessage.RetryCount++;
                    inboxMessage = existingMessage;
                }

                // Process the message
                await ProcessInboxMessageAsync(dbContext, inboxMessage, payload);
                
                await dbContext.SaveChangesAsync();
                await _channel.BasicAckAsync(ea.DeliveryTag, false);
                
                Console.WriteLine($"Processed message {messageId}: {eventType}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing message {messageId}: {ex.Message}");
                
                // Could implement retry logic or send to DLQ
                await _channel.BasicNackAsync(ea.DeliveryTag, false, false);
            }
        };

        await _channel.BasicConsumeAsync(
            queue: "order.processing",
            autoAck: false,
            consumer: consumer
        );

        Console.WriteLine("Inbox consumer started - waiting for messages...");
    }

    private async Task ProcessInboxMessageAsync(InboxOutboxDbContext dbContext, InboxMessage inboxMessage, string payload)
    {
        try
        {
            // Simulate business logic based on event type
            switch (inboxMessage.EventType)
            {
                case "OrderCreated":
                    await ProcessOrderCreatedEvent(dbContext, payload);
                    break;
                default:
                    Console.WriteLine($"Unknown event type: {inboxMessage.EventType}");
                    break;
            }

            inboxMessage.IsProcessed = true;
            inboxMessage.ProcessedAt = DateTime.UtcNow;
            inboxMessage.ErrorMessage = null;
        }
        catch (Exception ex)
        {
            inboxMessage.RetryCount++;
            inboxMessage.ErrorMessage = ex.Message;
            throw;
        }
    }

    private async Task ProcessOrderCreatedEvent(InboxOutboxDbContext dbContext, string payload)
    {
        var orderEvent = JsonConvert.DeserializeObject<dynamic>(payload);
        
        // Simulate processing: Update order status, send notifications, etc.
        if (orderEvent?.OrderId == null) return;
        var orderId = (int)orderEvent.OrderId;
        var order = await dbContext.Orders.FindAsync(orderId);
        
        if (order != null)
        {
            order.Status = "Processing";
            Console.WriteLine($"Order {orderId} status updated to 'Processing'");
            
            // Could trigger additional business logic here:
            // - Send confirmation email
            // - Update inventory
            // - Create shipping records
            // - Send notification to warehouse
        }
        
        // Simulate processing time
        await Task.Delay(500);
    }

    public async Task StopConsumingAsync()
    {
        if (_channel != null)
        {
            await _channel.CloseAsync();
            _channel.Dispose();
        }

        if (_connection != null)
        {
            await _connection.CloseAsync();
            _connection.Dispose();
        }
    }
}