using System;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using Microsoft.Data.SqlClient;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Newtonsoft.Json;

namespace InboxOutboxPattern;

// Inbox Consumer - Implements Inbox Pattern for idempotent message processing
public class InboxConsumer
{
    private readonly string _connectionString;
    private IConnection? _connection;
    private IChannel? _channel;

    public InboxConsumer()
    {
        _connectionString = ConfigurationManager.ConnectionStrings["SqlServerConnectionNamespace"]?.ConnectionString 
            ?? throw new InvalidOperationException("SqlServerConnectionNamespace not found in App.config");
    }

    public async Task StartConsumingAsync()
    {
        var consumer = new AsyncEventingBasicConsumer();
        consumer.ReceivedAsync += async (model, ea) =>
        {
            var messageId = ea.BasicProperties.MessageId ?? Guid.NewGuid().ToString();
            var correlationId = ea.BasicProperties.CorrelationId;
            
            try
            {
                // Check if message already processed (idempotency)
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Check if the message is processed

                // Store in inbox and process
                await ProcessMessageAsync(connection, messageId, eventType, payload, correlationId, ea.Exchange, ea.RoutingKey);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error processing message {messageId}: {ex.Message}");
                await _channel.BasicNackAsync(ea.DeliveryTag, false, false);
            }
        };

        await _channel.BasicConsumeAsync(
            queue: "order.processing",
            autoAck: false,
            consumer: consumer
        );

        Console.WriteLine("Consumer started - waiting for messages...");
    }

    private async Task<bool> CheckIfMessageProcessedAsync(SqlConnection connection, string messageId)
    {
        string checkSql = "SELECT IsProcessed FROM tb_inbox WHERE MessageId = @MessageId";
        
        using var command = new SqlCommand(checkSql, connection);
        command.Parameters.AddWithValue("@MessageId", messageId);
        
        var result = await command.ExecuteScalarAsync();
        return result != null && (bool)result;
    }

    private async Task ProcessMessageAsync(SqlConnection connection, string messageId, string eventType, 
        string payload, string? correlationId, string exchange, string routingKey)
    {
        using var transaction = connection.BeginTransaction();
        
        try
        {
            // 1. Insert into tb_inbox
            var inboxMessage = new InboxMessage
            {
                MessageId = messageId,
                EventType = eventType,
                Payload = payload,
                CorrelationId = correlationId,
                SourceExchange = exchange,
                SourceRoutingKey = routingKey
            };

            await InsertInboxMessageAsync(connection, transaction, inboxMessage);
            Console.WriteLine("💾 Message stored in inbox");

            // 2. Process the message (deserialize to Order)
            await ProcessOrderMessageAsync(payload);

            // 3. Mark as processed
            await MarkMessageAsProcessedAsync(connection, transaction, messageId);
            Console.WriteLine("✅ Message marked as processed");

            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Console.WriteLine($"❌ Transaction failed: {ex.Message}");
            throw;
        }
    }

    private async Task InsertInboxMessageAsync(SqlConnection connection, SqlTransaction transaction, InboxMessage inboxMessage)
    {
        string insertSql = @"
            INSERT INTO tb_inbox (MessageId, EventType, Payload, CorrelationId, SourceExchange, SourceRoutingKey)
            VALUES (@MessageId, @EventType, @Payload, @CorrelationId, @SourceExchange, @SourceRoutingKey)";

        using var command = new SqlCommand(insertSql, connection, transaction);
        command.Parameters.AddWithValue("@MessageId", inboxMessage.MessageId);
        command.Parameters.AddWithValue("@EventType", inboxMessage.EventType);
        command.Parameters.AddWithValue("@Payload", inboxMessage.Payload);
        command.Parameters.AddWithValue("@CorrelationId", inboxMessage.CorrelationId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@SourceExchange", inboxMessage.SourceExchange ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@SourceRoutingKey", inboxMessage.SourceRoutingKey ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    private async Task ProcessOrderMessageAsync(string payload)
    {
        try
        {
            // Deserialize the message payload to Order object
            var orderEvent = JsonConvert.DeserializeObject<dynamic>(payload);
            
            if (orderEvent?.OrderId == null)
            {
                Console.WriteLine("⚠️ Invalid order event - OrderId missing");
                return;
            }

            // Create Order object from the message
            var order = new Order
            {
                Id = (int)orderEvent.OrderId,
                CustomerId = orderEvent.CustomerId ?? 0,
                ProductName = orderEvent.ProductName ?? "Unknown Product",
                Quantity = orderEvent.Quantity ?? 1,
                Price = orderEvent.Price ?? 0m,
                CreatedAt = orderEvent.CreatedAt != null ? DateTime.Parse(orderEvent.CreatedAt.ToString()) : DateTime.UtcNow,
                Status = "Processing"
            };

            Console.WriteLine($"📦 Processing Order:");
            Console.WriteLine($"   Order ID: {order.Id}");
            Console.WriteLine($"   Customer: {order.CustomerId}");
            Console.WriteLine($"   Product: {order.ProductName}");
            Console.WriteLine($"   Quantity: {order.Quantity}");
            Console.WriteLine($"   Price: ${order.Price:F2}");
            Console.WriteLine($"   Status: {order.Status}");

            // Simulate business logic processing
            await Task.Delay(500);
            Console.WriteLine($"🔄 Order {order.Id} processing completed");
            
            // Here you could:
            // - Save order to database
            // - Send confirmation email
            // - Update inventory
            // - Trigger other business processes
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error processing order: {ex.Message}");
            throw;
        }
    }

    private async Task MarkMessageAsProcessedAsync(SqlConnection connection, SqlTransaction transaction, string messageId)
    {
        string updateSql = @"
            UPDATE tb_inbox 
            SET IsProcessed = 1, ProcessedAt = GETUTCDATE() 
            WHERE MessageId = @MessageId";

        using var command = new SqlCommand(updateSql, connection, transaction);
        command.Parameters.AddWithValue("@MessageId", messageId);
        
        await command.ExecuteNonQueryAsync();
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

        Console.WriteLine("🛑 Inbox consumer stopped");
    }
}