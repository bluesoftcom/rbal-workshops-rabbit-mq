using System;
using System.Configuration;
using System.Threading.Tasks;
using RabbitMQ.Client;

namespace InboxOutboxPattern;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== RabbitMQ Inbox/Outbox Pattern Demo ===");
        
        // Configuration
        string host = ConfigurationManager.AppSettings["host"] ?? "localhost";
        int configPort;
        int port = int.TryParse(ConfigurationManager.AppSettings["port"], out configPort) ? configPort : 5672;
        string userName = ConfigurationManager.AppSettings["userName"] ?? "guest";
        string password = ConfigurationManager.AppSettings["password"] ?? "guest";
        string virtualHost = ConfigurationManager.AppSettings["virtualHost"] ?? "/";

        // Database connection string for AWS RDS SQL Server
        string connectionString = ConfigurationManager.ConnectionStrings["SqlServerConnectionNamespace"]?.ConnectionString
        connectionString = connectionString.replace("{username}", ConfigurationManager.AppSettings["userName"]);

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

        Console.WriteLine("Choose demo mode:");
        Console.WriteLine("1. Producer with Outbox Pattern");
        Console.WriteLine("2. Consumer with Inbox Pattern");
        Console.WriteLine("3. Complete Demo (Producer + Consumer)");
        Console.WriteLine("4. Message Processor (Process Outbox Messages)");
        
        var choice = Console.ReadKey().KeyChar;
        Console.WriteLine();

        switch (choice)
        {
            case '1':
                await RunProducerDemo(dbContext, rabbitMqFactory);
                break;
            case '2':
                await RunConsumerDemo(dbContext, rabbitMqFactory);
                break;
            case '3':
                await RunCompleteDemo(dbContext, rabbitMqFactory);
                break;
            case '4':
                await RunMessageProcessor(dbContext, rabbitMqFactory);
                break;
            default:
                await RunCompleteDemo(dbContext, rabbitMqFactory);
                break;
        }
    }

    private static async Task RunProducerDemo(InboxOutboxDbContext dbContext, ConnectionFactory rabbitMqFactory)
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

    private static async Task RunConsumerDemo(InboxOutboxDbContext dbContext, ConnectionFactory rabbitMqFactory)
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

    private static async Task RunCompleteDemo(InboxOutboxDbContext dbContext, ConnectionFactory rabbitMqFactory)
    {
        Console.WriteLine("\n=== COMPLETE INBOX/OUTBOX DEMO ===");
        
        // Start consumer first
        var consumer = new InboxConsumer(dbContext, rabbitMqFactory);
        var consumerTask = consumer.StartConsumingAsync();
        
        // Start message processor
        var messageProcessor = new OutboxMessageProcessor(dbContext, rabbitMqFactory);
        var processorTask = messageProcessor.StartProcessingAsync();
        
        await Task.Delay(2000); // Let consumer and processor start

        // Run producer
        var producer = new OutboxProducer(dbContext, rabbitMqFactory);
        
        var orders = new[]
        {
            new Order { CustomerId = 2001, ProductName = "Smartphone", Quantity = 1, Price = 699.99m },
            new Order { CustomerId = 2002, ProductName = "Tablet", Quantity = 1, Price = 399.99m },
            new Order { CustomerId = 2003, ProductName = "Headphones", Quantity = 2, Price = 149.99m }
        };

        Console.WriteLine("Creating orders with outbox pattern...");
        foreach (var order in orders)
        {
            await producer.CreateOrderAsync(order);
            Console.WriteLine($"Order {order.Id} created and queued for publishing");
            await Task.Delay(2000);
        }

        Console.WriteLine("\nDemo running... Press 'q' to stop.");
        ConsoleKeyInfo keyInfo;
        do
        {
            keyInfo = Console.ReadKey(true);
        } while (keyInfo.KeyChar != 'q');

        // Stop all services
        await consumer.StopConsumingAsync();
        await messageProcessor.StopProcessingAsync();
        
        Console.WriteLine("Demo stopped.");
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
