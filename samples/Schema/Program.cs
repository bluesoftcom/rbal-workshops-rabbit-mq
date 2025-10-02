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

        // Define queue with schema metadata in arguments
        var queueName = "q.common";
        var exchangeName = "ex.common";
        var routingKey = "*";

        // // Create exchange
        await ch.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false
        );

        // // Declare queue with schema information in arguments
        // var queueArguments = new Dictionary<string, object?>
        // {
        //     { "x-schema-type", "json" },
        //     { "x-schema-version", "v1" },
        //     { "x-schema-definition", schemaJson }
        // };

        await ch.QueueDeclareAsync(
            durable: true,
            queue: queueName,
            exclusive: false,
            autoDelete: false,
            arguments: null
        );

        await ch.QueueBindAsync(queueName, exchangeName, routingKey);

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

        var usernames = new string[]
        {
            "andi.shima",
            "aleksander.vangjeli",
            "matilda.veliu",
            "aida.ymeri",
            "grent.mustafa",
            "gentiana.papakroni",
            "eldi.necaj",
            "bleona.minaj",
            "enxhi.maloku",
            "arben.rexhepi",
            "xhensila.jaho",
            "megi.xhemo",
            "ermal.kola",
            "enri.iseberi",
            "eva.kapciu",
            "sara.bushati",
            "paolo.xhovara",
            "renato.alushi",
            "jon.hoxha",
            "gersjan.nano",
            "irida.osja"
        };
        var random = new Random();
        
        // Publish messages in a loop with random usernames as routing keys
        Console.WriteLine("Publishing messages with random usernames as routing keys...\n");



        for (int i = 0; i < 3000; i++)
        {
            var randomUsername = usernames[random.Next(usernames.Length)];

            bool published = await PublishMessageAsync<UserV1>(
                channel: ch,
                message: validUser,
                exchange: exchangeName,
                routingKey: randomUsername,
                schemaVersion: "v1",
                messageType: "user.created",
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

    private static Task SchemaExampleAvro()
    {
        string avroSchemaV1 = """
        {
          "type": "record",
          "name": "User",
          "namespace": "com.example.user",
          "fields": [
            {"name": "userId", "type": "long"},
            {"name": "username", "type": "string"},
            {"name": "email", "type": "string"}
          ]
        }
        """;

        string avroSchemaV2 = """
        {
          "type": "record",
          "name": "User",
          "namespace": "com.example.user",
          "fields": [
            {"name": "userId", "type": "long"},
            {"name": "username", "type": "string"},
            {"name": "email", "type": "string"},
            {"name": "firstName", "type": ["null", "string"], "default": null},
            {"name": "lastName", "type": ["null", "string"], "default": null},
            {"name": "createdDate", "type": ["null", {"type": "long", "logicalType": "timestamp-millis"}], "default": null}
          ]
        }
        """;

        RecordSchema? schemaV1 = Schema.Parse(avroSchemaV1) as RecordSchema;
        RecordSchema? schemaV2 = Schema.Parse(avroSchemaV2) as RecordSchema;

        // Create a record with V1 schema
        GenericRecord userV1 = new GenericRecord(schemaV2!);
        userV1.Add("userId", 456L);
        userV1.Add("username", "johndoe");
        userV1.Add("email", "john@example.com");

        // Serialize with V1 schema
        using MemoryStream streamV1 = new MemoryStream();
        BinaryEncoder encoderV1 = new BinaryEncoder(streamV1);
        GenericWriter<GenericRecord> writerV1 = new GenericWriter<GenericRecord>(schemaV2);
        writerV1.Write(userV1, encoderV1);
        byte[] v1Bytes = streamV1.ToArray();

        Console.WriteLine("User record created with V1 schema:");
        foreach (var field in schemaV1!.Fields)
        {
            Console.WriteLine($"  {field.Name}: {userV1[field.Name]}");
        }
        Console.WriteLine();

        // Read V1 data with V2 schema (schema evolution)
        using MemoryStream readStreamV2 = new MemoryStream(v1Bytes);
        BinaryDecoder decoderV2 = new BinaryDecoder(readStreamV2);
        GenericReader<GenericRecord> readerV2 = new GenericReader<GenericRecord>(schemaV1, schemaV2); // writer schema V1, reader schema V2
        GenericRecord userV2 = readerV2.Read(new GenericRecord(schemaV2!), decoderV2);

        Console.WriteLine("Same data read with V2 schema (evolved):");
        foreach (var field in schemaV2!.Fields)
        {
            var value = userV2.TryGetValue(field.Name, out var fieldValue) ? fieldValue : "N/A";
            Console.WriteLine($"  {field.Name}: {value}");
        }
        Console.WriteLine();

        return Task.CompletedTask;
    }

    private static async Task SchemaRegistryPattern(IChannel channel)
    {
        // Simulate a schema registry using a dedicated exchange and queue
        var schemaRegistryExchange = "schema.registry";
        var schemaRegistryQueue = "schema.registry.store";

        await channel.ExchangeDeclareAsync(
            exchange: schemaRegistryExchange,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false
        );

        await channel.QueueDeclareAsync(
            queue: schemaRegistryQueue,
            durable: true,
            exclusive: false,
            autoDelete: false
        );

        await channel.QueueBindAsync(schemaRegistryQueue, schemaRegistryExchange, "schema.register");

        // Register schemas in the registry
        var schemas = new[]
        {
            new {
                Id = "customer-v1",
                Type = "json",
                Version = "1.0",
                Subject = "customer",
                Schema = new JsonSchemaGenerator(new SystemTextJsonSchemaGeneratorSettings()).Generate(typeof(UserV1)).ToJson()
            },
            new {
                Id = "customer-v2",
                Type = "json",
                Version = "2.0",
                Subject = "customer",
                Schema = new JsonSchemaGenerator(new SystemTextJsonSchemaGeneratorSettings()).Generate(typeof(UserV2)).ToJson()
            }
        };

        foreach (var schema in schemas)
        {
            var registrationMessage = new
            {
                Action = "REGISTER_SCHEMA",
                SchemaId = schema.Id,
                Subject = schema.Subject,
                Version = schema.Version,
                SchemaType = schema.Type,
                SchemaDefinition = schema.Schema,
                Timestamp = DateTime.UtcNow
            };

            var registrationJson = JsonConvert.SerializeObject(registrationMessage, Formatting.None);
            var registrationBody = Encoding.UTF8.GetBytes(registrationJson);

            await PublishMessageAsync(channel, registrationBody, schemaRegistryExchange, "", "v1", "schema.register");

        }
    }
}