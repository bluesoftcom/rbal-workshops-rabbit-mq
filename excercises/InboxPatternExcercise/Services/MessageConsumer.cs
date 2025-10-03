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
public class MessageConsumer
{
    private IChannel _channel;
    private SqliteConnection _sqlConnection;

    public MessageConsumer(IChannel channel, SqliteConnection sqlConnection)
    {
        _channel = channel;
        _sqlConnection = sqlConnection;
    }

    public async Task StartConsumingAsync(string queue)
    {
        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            // Begin a transaction on the database

            // Get the messageId and other important properties from the message properties

            // Get the payload
            var payload = Encoding.UTF8.GetString(ea.Body.ToArray());
            // use JsonConvert.DeserializeObject<Order> to convert the payload to the Order object

            // Check if the message was already recieved

            // If message was not recieved create a new InboxMessage
            // and insert it into the DB with InsertInboxMessageAsync

            // Perform validations, do the critical business logic
            Console.WriteLine("Recieved the message with payload: "+ payload+"");

            // If all went correctly, update the record as processed with MarkInboxMessageAsProcessedAsync
            // his step is redundant with the transaction - but it is an option to leave the message for later processing

            // if all went correctly, acknowledge the message and commit transaction

            // if an exception is caught - do a negative acknowledge and rollback the transaction
        };

        await _channel.BasicConsumeAsync(queue, false, consumer);

        Console.WriteLine("Consumer started - waiting for messages...");
    }

    private async Task InsertInboxMessageAsync(SqliteTransaction transaction, InboxMessage inboxMessage)
    {
        string insertSql = @"
                INSERT INTO tb_inbox (Id, CorrelationId, SourceExchange, SourceRoutingKey, Payload, EventType, IsProcessed)
                VALUES (@Id, @CorrelationId, @SourceExchange, @SourceRoutingKey, @Payload, @EventType, @IsProcessed);";

        using var command = new SqliteCommand(insertSql, _sqlConnection, transaction);
        command.Parameters.AddWithValue("@EventType", inboxMessage.EventType);
        command.Parameters.AddWithValue("@Payload", inboxMessage.Payload);
        command.Parameters.AddWithValue("@CorrelationId", inboxMessage.CorrelationId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@SourceExchange", inboxMessage.SourceExchange ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@SourceRoutingKey", inboxMessage.SourceRoutingKey ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@IsProcessed", 0);
        command.Parameters.AddWithValue("@Id", inboxMessage.Id);

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

        Console.WriteLine("­Inbox consumer stopped");
    }
}
