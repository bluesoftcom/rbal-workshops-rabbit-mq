using System;
using System.Configuration;
using System.Threading.Tasks;
using RabbitMQ.Client;

namespace InboxOutboxPattern;

public class Program
{
    public static async Task Main(string[] args)
    {
        // Configuration
        string host = ConfigurationManager.AppSettings["host"] ?? "localhost";
        int configPort;
        int port = int.TryParse(ConfigurationManager.AppSettings["port"], out configPort) ? configPort : 5672;
        string userName = ConfigurationManager.AppSettings["userName"] ?? "guest";
        string password = ConfigurationManager.AppSettings["password"] ?? "guest";
        string virtualHost = ConfigurationManager.AppSettings["virtualHost"] ?? "/";

        // Database connection string for AWS RDS SQL Server
        // string connectionString = ConfigurationManager.ConnectionStrings["SqlServerConnectionNamespace"]?.ConnectionString;
        // connectionString = connectionString.Replace("{username}", ConfigurationManager.AppSettings["userName"]);
        string connectionString = "";
        // Initialize database
        using var dbContext = new InboxOutboxDbContext(connectionString);
        await dbContext.Database.EnsureCreatedAsync();

        // Initialize RabbitMQ connection
        var rabbitMqFactory = new ConnectionFactory
        {
            HostName = host,
            Port = port,
            UserName = userName,
            Password = password,
            VirtualHost = virtualHost
        };

        await RunProducer(dbContext, rabbitMqFactory);
        await RunConsumer(dbContext, rabbitMqFactory);
    }

    private static async Task RunProducer(InboxOutboxDbContext dbContext, ConnectionFactory rabbitMqFactory)
    {
        Console.WriteLine("\n=== PRODUCER WITH OUTBOX PATTERN ===");
        
        var producer = new OutboxProducer(dbContext, rabbitMqFactory);
        
        // Simulate business operations
        var orders = new[]
        {
            new Order { CustomerId = 1001, ProductName = "Laptop", Quantity = 1, Price = 1299.99m },
            new Order { CustomerId = 1002, ProductName = "Mouse", Quantity = 2, Price = 29.99m },
            new Order { CustomerId = 1003, ProductName = "Keyboard", Quantity = 1, Price = 79.99m }
        };

        foreach (var order in orders)
        {
            try
            {
                await producer.CreateOrderAsync(order);
                Console.WriteLine($"Order created: {order.Id} for Customer {order.CustomerId}");
                await Task.Delay(1000); // Simulate processing time
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating order: {ex.Message}");
            }
        }

        Console.WriteLine("\nPress any key to continue...");
        Console.ReadKey();
    }

    private static async Task RunConsumer(InboxOutboxDbContext dbContext, ConnectionFactory rabbitMqFactory)
    {
        Console.WriteLine("\n=== CONSUMER WITH INBOX PATTERN ===");
        
        var consumer = new InboxConsumer(dbContext, rabbitMqFactory);
        
        Console.WriteLine("Starting consumer... Press 'q' to quit.");
        await consumer.StartConsumingAsync();
        
        // Wait for user input
        ConsoleKeyInfo keyInfo;
        do
        {
            keyInfo = Console.ReadKey(true);
        } while (keyInfo.KeyChar != 'q');
        
        await consumer.StopConsumingAsync();
        Console.WriteLine("Consumer stopped.");
    }


    private static async Task RunMessageProcessor(InboxOutboxDbContext dbContext, ConnectionFactory rabbitMqFactory)
    {
        Console.WriteLine("\n=== OUTBOX MESSAGE PROCESSOR ===");
        
        var processor = new OutboxMessageProcessor(dbContext, rabbitMqFactory);
        
        Console.WriteLine("Starting message processor... Press 'q' to quit.");
        await processor.StartProcessingAsync();
        
        ConsoleKeyInfo keyInfo;
        do
        {
            keyInfo = Console.ReadKey(true);
        } while (keyInfo.KeyChar != 'q');
        
        await processor.StopProcessingAsync();
        Console.WriteLine("Message processor stopped.");
    }
}
