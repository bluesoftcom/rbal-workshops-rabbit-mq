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
        string queue = "q.orders.inboxoutbox";
        string exchange = "ex.orders.inboxoutbox";
        using IConnection connection = await factory.CreateConnectionAsync();
        using IChannel channel = await connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(
            exchange: exchange,
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false
        );

        await channel.QueueDeclareAsync(
            queue: queue,
            durable: true,
            exclusive: false,
            autoDelete: false
        );

        await channel.QueueBindAsync(queue, exchange, string.Empty);

        await RunProducer(channel, sqlConnection);

        Console.WriteLine("Moving now to the Inbox Pattern - we have the messages in the queue,");
        Console.WriteLine("we just published them from our Outbox.");
        Console.WriteLine("Let's see what we can do with them now. Press any key...");
        Console.ReadLine();

        await RunConsumer(channel, sqlConnection);
    }

    private static async Task RunProducer(IChannel channel, SqliteConnection sqlConnection)
    {
        Console.WriteLine("\n=== PRODUCER WITH OUTBOX PATTERN ===");

        var producer = new OutboxProducer(channel, sqlConnection);

        // Simulate business operations
        var orders = new[]
        {
            new Order { Id=11, CustomerId = 1001, ProductName = "Laptop", Quantity = 1, Price = 1299.99m },
            new Order { Id=22, CustomerId = 1002, ProductName = "Mouse", Quantity = 2, Price = 29.99m },
            new Order { Id=33, CustomerId = 1003, ProductName = "Keyboard", Quantity = 1, Price = 79.99m }
        };

        foreach (var order in orders)
        {
            try
            {
                producer.SaveMessageToDB(order, sqlConnection);
                Console.WriteLine($"Order created: {order.Id} for Customer {order.CustomerId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating order: {ex.Message}");
            }
        }

        Console.WriteLine("So, we saved the messages to the database, now let's switch our attention to message publisher");
        
        Console.ReadKey();
        Console.WriteLine("Message Publisher is running constantly, press any key to stop it and move to next part...");
        while (!Console.KeyAvailable)
        {
            producer.PublishMessage();
        }
        if (Console.KeyAvailable) Console.ReadKey(true);

        Console.WriteLine("Messages were produced into the exchange and marked as sent");
    }

    private static async Task RunConsumer(IChannel channel, SqliteConnection sqlConnection)
    {
        var consumer = new InboxConsumer(channel, sqlConnection);

        Console.WriteLine("Starting consumer... Press any key to quit.");
        await consumer.StartConsumingAsync();

        // Wait for any key press
        Console.ReadKey(true);

        await consumer.StopConsumingAsync();
        Console.WriteLine("Consumer stopped.");
    }

    private static void InitDatabase(SqliteConnection connection)
    {
        string createTableOutboxSql = @"
            CREATE TABLE IF NOT EXISTS tb_outbox (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CorrelationId TEXT,
                Exchange TEXT,
                RoutingKey TEXT,
                Payload TEXT NOT NULL,
                EventType TEXT NOT NULL,
                ProducedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                IsProcessed INTEGER DEFAULT 0,
                ProcessedAt DATETIME,
                ErrorMessage TEXT,
                RetryCount INTEGER DEFAULT 0
            );";

        using (var command = new SqliteCommand(createTableOutboxSql, connection))
            command.ExecuteNonQuery();

            string createTableInboxSql = @"
                CREATE TABLE IF NOT EXISTS tb_inbox (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
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
