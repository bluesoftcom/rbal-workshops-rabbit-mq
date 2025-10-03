using InboxPatternExcercise.Services;
using Microsoft.Data.Sqlite;
using RabbitMQ.Client;
using System.Configuration;

namespace InboxOutboxPattern;

public class Program
{
    static SqliteConnection _sqliteConnection = null!;
    public static async Task Main(string[] args)
    {
        using SqliteConnection sqlConnection = new SqliteConnection("Data Source=:memory:");
        sqlConnection.Open();
        InitDatabase(sqlConnection);

        #region Initialize RabbitMQ Connection

        string hostName = ConfigurationManager.AppSettings["host"];
        int port = int.Parse(ConfigurationManager.AppSettings["port"]);
        string userName = ConfigurationManager.AppSettings["userName"];
        string password = ConfigurationManager.AppSettings["password"];
        string virtualHost = ConfigurationManager.AppSettings["virtualHost"];

        var factory = new ConnectionFactory()
        {
            HostName = hostName,
            UserName = userName,
            Password = password,
            VirtualHost = virtualHost,
            Ssl = new SslOption
            {
                Enabled = true,
                ServerName = hostName
            }
        };

        using IConnection connection = await factory.CreateConnectionAsync();
        using IChannel channel = await connection.CreateChannelAsync();

        #endregion

        string queue = "q.orders.withinbox";
        string exchange = "ex.orders.withinbox";

        #region Produce Messages

        Producer messageProducer = new Producer(channel);
        messageProducer.CreateMessageLayer(channel, exchange, queue);
        messageProducer.PublishOrderMessage(exchange, new Order
        {
            Id = 123,
            CreatedAt = DateTime.UtcNow,
            CustomerId = 456,
            Price = 52.66m,
            ProductName = "Keyboard",
            Quantity = 2,
            Status = "Shipped"
        });
        messageProducer.PublishOrderMessage(exchange, new Order
        {
            Id = 427,
            CreatedAt = DateTime.UtcNow,
            CustomerId = 426,
            Price = 23.54m,
            ProductName = "Mouse",
            Quantity = 2,
            Status = "Shipped"
        });
        messageProducer.PublishOrderMessageErrorous(exchange, new Order
        {
            Id = 853,
            CreatedAt = DateTime.UtcNow,
            CustomerId = 965,
            Price = 1.24m,
            ProductName = "Mouse",
            Quantity = 1,
            Status = "Returned"
        });

        #endregion

        Console.WriteLine("Messages are produced, let's start consuming");
        Console.WriteLine("Starting consumer... Press any key to quit.");
        MessageConsumer messageConsumer = new MessageConsumer(channel, sqlConnection);
        await messageConsumer.StartConsumingAsync(queue);

        // Wait for any key press
        Console.ReadKey(true);

        await messageConsumer.StopConsumingAsync();
        Console.WriteLine("Consumer stopped.");
    }

    private static void InitDatabase(SqliteConnection connection)
    {
        string createTableInboxSql = @"
                CREATE TABLE IF NOT EXISTS tb_inbox (
                    Id TEXT NOT NULL,
                    CorrelationId TEXT,
                    SourceExchange TEXT,
                    SourceRoutingKey TEXT,
                    Payload TEXT NOT NULL,
                    EventType TEXT NOT NULL,
                    ReceivedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    IsProcessed INTEGER DEFAULT 0,
                    ProcessedAt DATETIME,
                    ErrorMessage TEXT,
                    RetryCount INTEGER DEFAULT 0
                );";

        using (var command = new SqliteCommand(createTableInboxSql, connection))
            command.ExecuteNonQuery();
    }
}
