using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Configuration;
using System.Text;
using System.Threading.Tasks;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace InboxOutboxPattern;

public class MessageConsumer
{
    private static IChannel _channel;
    private static SqliteConnection _sqlConnection;
    private static SqliteTransaction _transaction;

    public MessageConsumer(IChannel channel, SqliteConnection sqlConnection)
    {
        _channel = channel;
        _sqlConnection = sqlConnection;
        if (_sqlConnection.State != System.Data.ConnectionState.Open)
            _sqlConnection.Open();
        _transaction = _sqlConnection.BeginTransaction();
    }

    public async Task StartConsumingAsync(string queue)
    {
        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            try
            {
                var messageId = ea.BasicProperties.MessageId;
                var payload = Encoding.UTF8.GetString(ea.Body.ToArray());

                bool isAlreadyProcessed = CheckIfMessageProcessedAsync(_transaction, messageId!);
                if (isAlreadyProcessed)
                {
                    Console.WriteLine($"The message {messageId} was already processed!");
                    return;
                }

                InboxMessage messageForDB = new InboxMessage
                {
                    CorrelationId = ea.BasicProperties.CorrelationId,
                    Id = new Guid(messageId!),
                    Payload = payload,
                    ProcessedAt = DateTime.UtcNow,
                    SourceExchange = ea.Exchange,
                    SourceRoutingKey = ea.RoutingKey
                };
                InsertInboxMessageAsync(_transaction, messageForDB);

                string selectSql = "SELECT id FROM tb_test;";
                using (var selectCmd = new SqliteCommand(selectSql, _sqlConnection, _transaction))
                using (var reader = selectCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Console.WriteLine($"id: {reader.GetInt32(0)}");
                    }
                }

                Console.WriteLine(messageId);
                Console.WriteLine("Received the message with payload: " + payload);

                MarkInboxMessageAsProcessedAsync(_transaction, messageId);

                await _channel.BasicAckAsync(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                await _channel.BasicNackAsync(ea.DeliveryTag, false, false);
            }
        };

        await _channel.BasicConsumeAsync(queue, false, consumer);

        Console.WriteLine("Consumer started - waiting for messages...");
    }

    private void InsertInboxMessageAsync(SqliteTransaction transaction, InboxMessage inboxMessage)
    {
        string insertSql = @"
        INSERT INTO tb_inbox (Id, CorrelationId, SourceExchange, SourceRoutingKey, Payload, EventType, IsProcessed)
        VALUES (@Id, @CorrelationId, @SourceExchange, @SourceRoutingKey, @Payload, @EventType, @IsProcessed)
        RETURNING Id;";

        using var command = new SqliteCommand(insertSql, _sqlConnection, transaction);
        command.Parameters.AddWithValue("@EventType", inboxMessage.EventType ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Payload", inboxMessage.Payload ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@CorrelationId", inboxMessage.CorrelationId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@SourceExchange", inboxMessage.SourceExchange ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@SourceRoutingKey", inboxMessage.SourceRoutingKey ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@IsProcessed", 0);
        command.Parameters.AddWithValue("@Id", inboxMessage.Id);

        var result = command.ExecuteScalar();
        Console.Write(result is Guid guid ? guid : Guid.Parse(result.ToString()!));
    }

    private void MarkInboxMessageAsProcessedAsync(SqliteTransaction transaction, string messageId)
    {
        string updateSql = @"
            UPDATE tb_inbox 
            SET IsProcessed = 1, ProcessedAt = @ProcessedAt 
            WHERE Id = @Id";

        using var command = new SqliteCommand(updateSql, _sqlConnection, transaction);
        command.Parameters.AddWithValue("@Id", messageId);
        command.Parameters.AddWithValue("@ProcessedAt", DateTime.Now);

        command.ExecuteNonQuery();
    }

    private bool CheckIfMessageProcessedAsync(SqliteTransaction transaction, string messageId)
    {
        //string selectCmd = "SELECT Id FROM tb_inbox;";

        //using (var reader = selectCmd.ExecuteReader())
        //{
        //    while (reader.Read())
        //    {
        //        Console.WriteLine($"id: {reader.GetInt32(0)}");
        //    }
        //}
        string checkSql = "SELECT IsProcessed FROM tb_inbox WHERE Id = @Id";

        using var command = new SqliteCommand(checkSql, _sqlConnection, transaction);
        command.Parameters.AddWithValue("@Id", messageId);

        var result = command.ExecuteScalar();
        return result != null && (bool)result;
    }

    public async Task StopConsumingAsync()
    {
        if (_transaction != null)
        {
            _transaction.Commit(); // or _transaction.Rollback() if needed
            _transaction.Dispose();
        }
        if (_channel != null)
        {
            await _channel.CloseAsync();
            _channel.Dispose();
        }
        if (_sqlConnection != null)
        {
            _sqlConnection.Close();
            _sqlConnection.Dispose();
        }
        Console.WriteLine("Inbox consumer stopped");
    }
}
