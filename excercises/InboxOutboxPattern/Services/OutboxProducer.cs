using System;
using System.Threading.Tasks;
using System.Configuration;
using System.Text;
using Microsoft.Data.SqlClient;
using RabbitMQ.Client;
using Newtonsoft.Json;

namespace InboxOutboxPattern;

// Outbox Producer - Implements Transactional Outbox Pattern
public class OutboxProducer
{
    private readonly string _connectionString;

    public OutboxProducer()
    {
        _connectionString = ConfigurationManager.ConnectionStrings["SqlServerConnectionNamespace"]?.ConnectionString 
            ?? throw new InvalidOperationException("SqlServerConnectionNamespace not found in App.config");
    }

    public async Task ProduceMessageAsync()
    {
        // Create a new Order within this method
        var order = new Order
        {
            Id = new Random().Next(1000, 9999),
            CustomerId = new Random().Next(1, 100),
            ProductName = "Sample Product",
            Quantity = new Random().Next(1, 10),
            Price = (decimal)(new Random().NextDouble() * 100),
            CreatedAt = DateTime.UtcNow,
            Status = "Created"
        };

        Console.WriteLine($"📦 Creating order: {order.Id} for customer {order.CustomerId}");
        Console.WriteLine($"   Product: {order.ProductName}, Qty: {order.Quantity}, Price: ${order.Price:F2}");

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();
        
        try
        {
            // 1. Create outbox message in transaction
            order order = new order();

            var outboxMessage = new OutboxMessage
            {
                EventType = "OrderCreated",
                AggregateId = order.Id.ToString(),
                Payload = JsonConvert.SerializeObject(orderCreatedEvent),
                Exchange = "orders.exchange",
                RoutingKey = "order.created",
                CorrelationId = Guid.NewGuid().ToString()
            };

            // Insert into tb_outbox
            string insertSql = @"
                INSERT INTO tb_outbox (EventType, AggregateId, Payload, Exchange, RoutingKey, CorrelationId)
                VALUES (@EventType, @AggregateId, @Payload, @Exchange, @RoutingKey, @CorrelationId)";

            using var command = new SqlCommand(insertSql, connection, transaction);
            command.Parameters.AddWithValue("@EventType", outboxMessage.EventType);
            command.Parameters.AddWithValue("@AggregateId", outboxMessage.AggregateId);
            command.Parameters.AddWithValue("@Payload", outboxMessage.Payload);
            command.Parameters.AddWithValue("@Exchange", outboxMessage.Exchange);
            command.Parameters.AddWithValue("@RoutingKey", outboxMessage.RoutingKey);
            command.Parameters.AddWithValue("@CorrelationId", outboxMessage.CorrelationId ?? (object)DBNull.Value);

            await command.ExecuteNonQueryAsync();
            Console.WriteLine("💾 Outbox message saved to database");

            // 2. Produce message to RabbitMQ
            await PublishToRabbitMQ(outboxMessage);
            Console.WriteLine("🐰 Message published to RabbitMQ");

            // 3. Commit transaction - ensures atomicity
            await transaction.CommitAsync();
            Console.WriteLine($"✅ Order {order.Id} and outbox message processed successfully");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Console.WriteLine($"❌ Transaction failed: {ex.Message}");
            throw;
        }
    }

    private async Task PublishToRabbitMQ(OutboxMessage outboxMessage)
    {
        var body = Encoding.UTF8.GetBytes(outboxMessage.Payload);
    }
}