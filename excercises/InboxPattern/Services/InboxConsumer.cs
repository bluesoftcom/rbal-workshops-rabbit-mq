using System;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using Microsoft.Data.Sqlite;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Newtonsoft.Json;

namespace InboxOutboxPattern;

// Inbox Consumer - Implements Inbox Pattern for idempotent message processing
public class InboxConsumer
{
    private IChannel _channel;
    private SqliteConnection _sqlConnection;

    public InboxConsumer(IChannel channel, SqliteConnection sqlConnection)
    {
        _channel = channel;
        _sqlConnection = sqlConnection;
    }

    public async Task StartConsumingAsync()
    {
        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            using var transaction = _sqlConnection.BeginTransaction();

            var messageId = ea.BasicProperties.MessageId ?? Guid.NewGuid().ToString();
            var correlationId = ea.BasicProperties.CorrelationId;
            var payload = Encoding.UTF8.GetString(ea.Body.ToArray());

            try
            {
                // Store in inbox and process
                await ProcessMessageAsync(messageId, "", payload, correlationId, ea.Exchange, ea.RoutingKey, transaction);
                await _channel.BasicAckAsync(ea.DeliveryTag, false);
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ÔŁî Error processing message {messageId}: {ex.Message}");
                await _channel.BasicNackAsync(ea.DeliveryTag, false, false);
                await transaction.RollbackAsync();
            }
        };

        await _channel.BasicConsumeAsync(
            queue: "q.orders.inboxoutbox",
            autoAck: false,
            consumer: consumer
        );

        Console.WriteLine("Consumer started - waiting for messages...");
    }

    private async Task ProcessMessageAsync(string messageId, string eventType,
        string payload, string? correlationId, string exchange, string routingKey, SqliteTransaction transaction)
    {
        // Check InboxMessage for duplicates
        var isProcessed = await CheckIfMessageProcessedAsync(transaction, messageId);

        if (isProcessed)
        {
            Console.WriteLine($"Message {messageId} has already been processed.");
            return;
        }

        try
        {
            // Create InboxMessage entry
            var inboxMessage = new InboxMessage
            {
                CorrelationId = correlationId,
                EventType = "",
                SourceExchange = exchange,
                SourceRoutingKey = routingKey,
                Payload = payload
            };

            await InsertInboxMessageAsync(transaction, inboxMessage);
            Console.WriteLine("Message stored in inbox");

            // DO THE WORK

            // If all goes well, mark InboxMessage as processed
            await MarkInboxMessageAsProcessedAsync(transaction, messageId);
            Console.WriteLine("Message marked as processed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Transaction failed: {ex.Message}");
            throw;
        }
    }

    private async Task InsertInboxMessageAsync(SqliteTransaction transaction, InboxMessage inboxMessage)
    {
        string insertSql = @"
                INSERT INTO tb_inbox (CorrelationId, SourceExchange, SourceRoutingKey, Payload, EventType, IsProcessed)
                VALUES (@CorrelationId, @SourceExchange, @SourceRoutingKey, @Payload, @EventType, @IsProcessed);
                SELECT last_insert_rowid();";

        using var command = new SqliteCommand(insertSql, _sqlConnection, transaction);
        command.Parameters.AddWithValue("@EventType", inboxMessage.EventType);
        command.Parameters.AddWithValue("@Payload", inboxMessage.Payload);
        command.Parameters.AddWithValue("@CorrelationId", inboxMessage.CorrelationId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@SourceExchange", inboxMessage.SourceExchange ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@SourceRoutingKey", inboxMessage.SourceRoutingKey ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@IsProcessed", 0);

        await command.ExecuteNonQueryAsync();
    }

    private async Task MarkInboxMessageAsProcessedAsync(SqliteTransaction transaction, string messageId)
    {
        string updateSql = @"
            UPDATE tb_inbox 
            SET IsProcessed = 1, ProcessedAt = @ProcessedAt 
            WHERE Id = @Id";

        using var command = new SqliteCommand(updateSql, _sqlConnection, transaction);
        command.Parameters.AddWithValue("@Id", messageId);
        command.Parameters.AddWithValue("@ProcessedAt", DateTime.Now);

        await command.ExecuteNonQueryAsync();
    }

    private async Task<bool> CheckIfMessageProcessedAsync(SqliteTransaction transaction, string messageId)
    {
        string checkSql = "SELECT IsProcessed FROM tb_inbox WHERE Id = @Id";

        using var command = new SqliteCommand(checkSql, _sqlConnection, transaction);
        command.Parameters.AddWithValue("@Id", messageId);

        var result = await command.ExecuteScalarAsync();
        return result != null && (bool)result;
    }

    public async Task StopConsumingAsync()
    {
        if (_channel != null)
        {
            await _channel.CloseAsync();
            _channel.Dispose();
        }

        Console.WriteLine("­čŤĹ Inbox consumer stopped");
    }
}
