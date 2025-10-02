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
        // get the system text json schema settings
        // get the json schema generator with settings as the parameter
        // use the gnerator to generate the schema out of the type of userV1 object
        // convert the schema to json

        #region Define Messaging Layer

        // Define queue with schema metadata in arguments
        var queueName = "user.events.v1";
        var exchangeName = "user.exchange";
        var routingKey = "user.created";

        // Create exchange
        await ch.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false
        );

        // Declare queue with schema information in arguments as a new Dictionary of string and object
        //provide x-schema-type
        //provide x-schema-version
        //provide x-schema-definition with the json schema

        await ch.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: queueArguments
        );

        await ch.QueueBindAsync(queueName, exchangeName, routingKey);

        #endregion

        // Create an instance of the message to publish

        string jsonUser = JsonConvert.SerializeObject(validUser, Formatting.None);
        if (!ValidateSchema(jsonUser, typeof(UserV1)))
        {
            Console.WriteLine("Message rejected - schema not valid.");
            return;
        }
        
        bool published = await PublishMessageAsync<UserV1>(
            channel: ch,
            message: validUser,
            exchange: exchangeName,
            routingKey: routingKey,
            schemaVersion: "v1",
            messageType: "user.created",
            correlationId: validUser.UserId.ToString()
        );

        if (published)
            Console.WriteLine("✅ Valid message published successfully.");
        else
            Console.WriteLine("❌ Failed to publish valid message.");


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
            Headers = new Dictionary<string, object?>
            {
                { "schema-version", schemaVersion },
                { "message-type", messageType },
                { "published-at", DateTimeOffset.UtcNow.ToString("O") }
            }
        };

        await channel.BasicPublishAsync(exchange, routingKey, false, properties, Encoding.UTF8.GetBytes(json));
        return true;
    }
}