using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using RabbitMQ.Client;
using System.Text;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace InboxOutboxPattern;

// Outbox Producer - Implements Transactional Outbox Pattern
public class OutboxProducer
{
    private SqliteConnection _connection;
    private IChannel _channel;

    public OutboxProducer(IChannel channel, SqliteConnection sqlConnection)
    {
        _channel = channel;
        _connection = sqlConnection;
    }

    public async void PublishMessage()
    {
        SqliteTransaction? transaction = _connection.BeginTransaction();

        using var selectCmd = new SqliteCommand(@"
                SELECT Id, Payload, Exchange, RoutingKey, CorrelationId, EventType, ProducedAt FROM tb_outbox
                WHERE IsProcessed = 0
                ORDER BY ProducedAt ASC
                LIMIT 1;", _connection);
        selectCmd.Transaction = transaction;

        using var reader = selectCmd.ExecuteReader();
        if (!reader.Read())
        {
            transaction.Rollback();
            return; // No unprocessed messages
        }

        int id = reader.GetInt32(0);
        string payload = reader.GetString(1);
        string exchange = reader.IsDBNull(2) ? "" : reader.GetString(2);
        string routingKey = reader.IsDBNull(3) ? "" : reader.GetString(3);
        string correlationId = reader.IsDBNull(4) ? "" : reader.GetString(4);

        Console.WriteLine("Got message from DB with id:" + id.ToString()+", publishing it on the queue");

        var body = Encoding.UTF8.GetBytes(payload);

        var properties = new BasicProperties
        {
            CorrelationId = correlationId
        };
        await _channel.BasicPublishAsync(
            exchange: string.IsNullOrEmpty(exchange) ? "" : exchange,
            routingKey: string.IsNullOrEmpty(routingKey) ? "example-queue" : routingKey,
            mandatory: true,
            basicProperties: properties,
            body: body
        );

        using var updateCmd = new SqliteCommand(@"
                UPDATE tb_outbox
                SET IsProcessed = 1, ProcessedAt = CURRENT_TIMESTAMP
                WHERE Id = @Id;", _connection);
        updateCmd.Transaction = transaction;
        updateCmd.Parameters.AddWithValue("@Id", id);
        await updateCmd.ExecuteNonQueryAsync();
        transaction.Commit();
    }

    public async void SaveMessageToDB(Order order)
    {
        string exchange = "ex.orders.inboxoutbox";
        string routingKey = string.Empty;

        Console.WriteLine($"Creating order: {order.Id} for customer {order.CustomerId}");
        Console.WriteLine($"Product: {order.ProductName}, Qty: {order.Quantity}, Price: ${order.Price:F2}");

        using var transaction = _connection.BeginTransaction();

        try
        {
            // 1. Create outbox message in transaction
            var outboxMessage = new OutboxMessage
            {
                ProducedAt = DateTime.UtcNow,
                CorrelationId = order.Id.ToString(),
                EventType = "order.created",
                Exchange = exchange,
                IsProcessed = false,
                Payload = JsonConvert.SerializeObject(order),
                RoutingKey = routingKey
            };

            // Insert into tb_outbox
            string insertSql = @"
                INSERT INTO tb_outbox (CorrelationId, Exchange, RoutingKey, Payload, EventType, ProducedAt, IsProcessed)
                VALUES (@CorrelationId, @Exchange, @RoutingKey, @Payload, @EventType, @ProducedAt, @IsProcessed);
                SELECT last_insert_rowid();";

            using var command = new SqliteCommand(insertSql, _connection, transaction);
            command.Parameters.AddWithValue("@CorrelationId", outboxMessage.CorrelationId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@Exchange", outboxMessage.Exchange ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@RoutingKey", outboxMessage.RoutingKey ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@Payload", outboxMessage.Payload);
            command.Parameters.AddWithValue("@EventType", outboxMessage.EventType);
            command.Parameters.AddWithValue("@ProducedAt", outboxMessage.ProducedAt);
            command.Parameters.AddWithValue("@IsProcessed", outboxMessage.IsProcessed ? 1 : 0);

            var idObj = command.ExecuteScalar();
            outboxMessage.Id = Convert.ToInt32(idObj);

            // Commit transaction - ensures atomicity
            transaction.Commit();
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            Console.WriteLine($"Transaction failed: {ex.Message}");
            throw;
        }
    }
}
