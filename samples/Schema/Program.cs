using System;
using System.Configuration;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using Newtonsoft.Json;
using NJsonSchema;
using NJsonSchema.Generation;
using Avro;
using Avro.Generic;
using Avro.IO;
using Models;
using NJsonSchema.Validation;

namespace SchemaApp;

public class Program
{
    public static async Task Main(string[] args)
    {
        #region Initialize RabbitMQ Connection
        string host = ConfigurationManager.AppSettings["host"] ?? string.Empty;
        int configPort;
        int port = int.TryParse(ConfigurationManager.AppSettings["port"], out configPort) ? configPort : 5671;
        string userName = ConfigurationManager.AppSettings["userName"] ?? string.Empty;
        string password = ConfigurationManager.AppSettings["password"] ?? string.Empty;
        string virtualHost = ConfigurationManager.AppSettings["virtualHost"] ?? string.Empty;

        var factory = new ConnectionFactory
        {
            HostName = host,
            Port = port,
            UserName = userName,
            Password = password,
            VirtualHost = virtualHost,
            Ssl = new SslOption { Enabled = true, ServerName = host }
        };

        IConnection conn = await factory.CreateConnectionAsync();
        IChannel ch = await conn.CreateChannelAsync();

        #endregion

        // Generate JSON Schema
        var settings = new SystemTextJsonSchemaGeneratorSettings();
        var generator = new JsonSchemaGenerator(settings);
        var userSchemaV1 = generator.Generate(typeof(UserV1));
        var schemaJson = userSchemaV1.ToJson();

        #region Define Messaging Layer

        string exchangeName = "ex.orders";
        // // Create exchange
        await ch.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: ExchangeType.Headers,
            durable: true,
            autoDelete: false
        );

        await ch.QueueDeclareAsync(
            durable: true,
            queue: "q.orders.xml",
            exclusive: false,
            autoDelete: false,
            arguments: null
        );

        await ch.QueueDeclareAsync(
            durable: true,
            queue: "q.orders.json",
            exclusive: false,
            autoDelete: false,
            arguments: null
        );

        await ch.QueueDeclareAsync(
            durable: true,
            queue: "q.orders.plaintext",
            exclusive: false,
            autoDelete: false,
            arguments: null
        );

        var headerJsonArgs = new Dictionary<string, object>
        {
            { "x-match", "all" },
            { "type", "json" }
        };

        var headerXmlArgs = new Dictionary<string, object>
        {
            { "x-match", "all" },
            { "type", "xml" }
        };

        var headerPlaintextArgs = new Dictionary<string, object>
        {
            { "x-match", "all" },
            { "type", "plaintext" }
        };

        await ch.QueueBindAsync("q.orders.json", "ex.orders", routingKey: string.Empty, arguments: headerJsonArgs);
        await ch.QueueBindAsync("q.orders.xml", "ex.orders", routingKey: string.Empty, arguments: headerXmlArgs);
        await ch.QueueBindAsync("q.orders.plaintext", "ex.orders", routingKey: string.Empty, arguments: headerPlaintextArgs);
        #endregion

        // Publish a valid message
        var validUser = new UserV1
        {
            UserId = 1001,
            Email = "alice@example.com",
            Username = "alice.johnson"
        };
        // var validUser = new UserV2
        // {
        //     UserId = 1001,
        //     Username = "Alice Johnson",
        //     Email = "alice@example.com",
        //     FirstName = "Alice",
        //     LastName = "Johnson",
        //     CreatedDate = DateTime.Now
        // };

        string jsonUser = JsonConvert.SerializeObject(validUser, Formatting.None);
        if (!ValidateSchema(jsonUser, typeof(UserV1)))
        {
            Console.WriteLine("Message rejected - schema not valid.");
            return;
        }

        
        // Publish messages in a loop with random usernames as routing keys
        Console.WriteLine("Publishing messages with random usernames as routing keys...\n");



        for (int i = 0; i < 3000; i++)
        {
            Dictionary<string, object?> headers = new Dictionary<string, object?>
            {
                { "type", "plaintext" }
            };

            bool published = await PublishMessageAsync<UserV1>(
                channel: ch,
                message: validUser,
                exchange: exchangeName,
                routingKey: string.Empty,
                schemaVersion: "v1",
                messageType: "user.created",
                headers: headers,
                correlationId: validUser.UserId.ToString()
            );

            if (published)
                Console.WriteLine("✅ Valid message published successfully.");
            else
                Console.WriteLine("❌ Failed to publish valid message.");
        }

        // Try to publish an invalid message
        var invalidJson = "{\"Id\": 1002, \"UserName\": \"invalid.user\", \"Email\": \"invalid@example.com\"}";
        if (!ValidateSchema(invalidJson, typeof(UserV1)))
        {
            Console.WriteLine("Message rejected - schema not valid.");
            return;
        }
    }

    private static bool ValidateSchema(string json, Type schemaClass)
    {
        var schema = new JsonSchemaGenerator(new SystemTextJsonSchemaGeneratorSettings()).Generate(schemaClass);
        ICollection<ValidationError> errors = schema.Validate(json);
        foreach (var error in errors)
        {
            Console.WriteLine($"  - {error}");
        }
        return errors.Count == 0;
    }

    private static async Task<bool> PublishMessageAsync<T>(
        IChannel channel,
        T message,
        string exchange,
        string routingKey,
        string schemaVersion,
        string messageType,
        Dictionary<string, object?> headers = null,
        string? correlationId = null
        ) where T : class

    {
        var json = JsonConvert.SerializeObject(message, Formatting.None);

        var messageId = Guid.NewGuid().ToString();
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            MessageId = messageId,
            CorrelationId = correlationId ?? Guid.NewGuid().ToString(),
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            Headers = headers
            
        };

        await channel.BasicPublishAsync(exchange, routingKey, false, properties, Encoding.UTF8.GetBytes(json));
        return true;
    }
}