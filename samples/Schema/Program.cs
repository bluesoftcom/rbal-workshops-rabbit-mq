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

namespace SchemaApp;

public class Program
{
    public static async Task Main(string[] args)
    {
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

        await DemonstrateSchemaWithRabbitMQ(ch);
    }

    private static Task SchemaEvolutionAvro()
    {
        // Original schema (Version 1)
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

        // Evolved schema (Version 2) - Added optional fields with defaults
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
        GenericRecord userV1 = new GenericRecord(schemaV1!);
        userV1.Add("userId", 456L);
        userV1.Add("username", "johndoe");
        userV1.Add("email", "john@example.com");

        // Serialize with V1 schema
        using MemoryStream streamV1 = new MemoryStream();
        BinaryEncoder encoderV1 = new BinaryEncoder(streamV1);
        GenericWriter<GenericRecord> writerV1 = new GenericWriter<GenericRecord>(schemaV1);
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

    private static Task SchemaEvolutionJson()
    {
        // Original user (V1)
        var userV1 = new UserV1
        {
            UserId = 123,
            Username = "john_doe",
            Email = "john.doe@example.com"
        };

        // Evolved user (V2) - added optional fields
        var userV2 = new UserV2
        {
            Id = 124,
            Name = "jane_smith",
            Email = "jane.smith@example.com",
            FirstName = "Jane",
            LastName = "Smith",
            CreatedDate = DateTime.Now
        };

        var settings = new SystemTextJsonSchemaGeneratorSettings();
        var generator = new JsonSchemaGenerator(settings);
        var schemaV1 = generator.Generate(typeof(UserV1));
        var schemaV2 = generator.Generate(typeof(UserV2));

        var jsonV1 = JsonConvert.SerializeObject(userV1, Formatting.Indented);
        var jsonV2 = JsonConvert.SerializeObject(userV2, Formatting.Indented);
        
        return Task.CompletedTask;
    }

    private static async Task DemonstrateSchemaWithRabbitMQ(IChannel channel)
    {
        // Define queue with schema metadata in arguments
        var queueName = "user.events.v1";
        var exchangeName = "user.exchange";
        var routingKey = "user.created";

        // Create exchange
        await channel.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false
        );

        // Generate JSON Schema
        var settings = new SystemTextJsonSchemaGeneratorSettings();
        var generator = new JsonSchemaGenerator(settings);
        var userSchema = generator.Generate(typeof(UserV2));
        var schemaJson = userSchema.ToJson();

        // Declare queue with schema information in arguments
        var queueArguments = new Dictionary<string, object?>
        {
            { "x-schema-type", "json" },
            { "x-schema-version", "v2" },
            { "x-schema-definition", schemaJson },
            { "x-message-ttl", 3600000 } // 1 hour TTL
        };

        // var schemaQueueArgs = new Dictionary<string, object?>
        // {
        //     // Schema identification
        //     { "x-schema-name", "CustomerEvent" },
        //     { "x-schema-version", "2.0.0" },
        //     { "x-schema-format", "json-schema" },
        //     { "x-schema-compatibility", "backward" },
            
        //     // Schema definition (can be full schema or reference)
        //     { "x-schema-registry-id", "customer-event-v2" },
        //     { "x-schema-subject", "customer.events" },
            
        //     // Validation settings
        //     { "x-schema-validation", "strict" },
        //     { "x-schema-required", true },
            
        //     // Additional queue policies
        //     { "x-message-ttl", 86400000 }, // 24 hours
        //     { "x-max-length", 10000 },
        //     { "x-overflow", "reject-publish" }
        // };

        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: queueArguments
        );

        await channel.QueueBindAsync(queueName, exchangeName, routingKey);

        // Publish a valid message
        var validUser = new UserV2
        {
            Id = 1001,
            Name = "Alice Johnson",
            Email = "alice@example.com",
            FirstName = "Alice",
            LastName = "Johnson",
            CreatedDate = DateTime.Now
        };

        var validJson = JsonConvert.SerializeObject(validUser, Formatting.None);
        var validMessageBody = Encoding.UTF8.GetBytes(validJson);

        // Validate against schema before publishing
        var validationErrors = userSchema.Validate(validJson);
        var isValid = validationErrors.Count == 0;

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            ContentEncoding = "utf-8",
            Headers = new Dictionary<string, object?>
            {
                { "schema-version", "v2" },
                { "schema-valid", isValid },
                { "message-type", "customer.created" }
            },
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            MessageId = Guid.NewGuid().ToString()
        };

        await channel.BasicPublishAsync(
            exchange: exchangeName,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: properties,
            body: validMessageBody
        );

        // Try to publish an invalid message (missing required field)
        var invalidJson = "{\"Id\": 1002, \"Email\": \"invalid@example.com\"}";
        var invalidValidationErrors = userSchema.Validate(invalidJson);
        var isInvalidValid = invalidValidationErrors.Count == 0;

        if (!isInvalidValid)
        {
            Console.WriteLine("Validation errors:");
            foreach (var error in invalidValidationErrors)
            {
                Console.WriteLine($"  - {error}");
            }
            Console.WriteLine("Message rejected - not published to queue.");
        }
    }

    private static async Task DemonstrateAvroSchemaWithRabbitMQ(IChannel channel)
    {
        var queueName = "user.events.avro";
        var exchangeName = "user.exchange";
        var routingKey = "user.created";

        // Create exchange
        await channel.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false
        );

        // Define Avro schema
        string avroSchemaJson = """
        {
          "type": "record",
          "name": "OrderEvent",
          "namespace": "com.example.orders",
          "fields": [
            {"name": "orderId", "type": "long"},
            {"name": "customerId", "type": "long"},
            {"name": "productName", "type": "string"},
            {"name": "quantity", "type": "int"},
            {"name": "totalPrice", "type": "double"},
            {"name": "orderTimestamp", "type": "string"},
            {"name": "status", "type": {"type": "enum", "name": "OrderStatus", "symbols": ["PENDING", "CONFIRMED", "SHIPPED", "DELIVERED"]}}
          ]
        }
        """;

        var avroSchema = Schema.Parse(avroSchemaJson);

        // Declare queue with Avro schema metadata
        var queueArguments = new Dictionary<string, object?>
        {
            { "x-schema-type", "avro" },
            { "x-schema-version", "2.0" },
            { "x-schema-definition", avroSchemaJson },
            { "x-message-ttl", 7200000 } // 2 hours TTL
        };

        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: queueArguments
        );

        await channel.QueueBindAsync(queueName, exchangeName, routingKey);

        // Create and serialize an Avro message
        var orderRecord = new GenericRecord(avroSchema as RecordSchema);
        orderRecord.Add("orderId", 5001L);
        orderRecord.Add("customerId", 1001L);
        orderRecord.Add("productName", "Wireless Mouse");
        orderRecord.Add("quantity", 1);
        orderRecord.Add("totalPrice", 29.99);
        orderRecord.Add("orderTimestamp", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssZ"));
        orderRecord.Add("status", "PENDING");

        // Serialize to Avro binary
        using var memoryStream = new MemoryStream();
        var encoder = new BinaryEncoder(memoryStream);
        var writer = new GenericWriter<GenericRecord>(avroSchema as RecordSchema);
        writer.Write(orderRecord, encoder);
        var avroBytes = memoryStream.ToArray();

        var avroProperties = new BasicProperties
        {
            ContentType = "application/avro",
            ContentEncoding = "binary",
            Headers = new Dictionary<string, object?>
            {
                { "schema-version", "2.0" },
                { "schema-type", "avro" },
                { "message-type", "order.created" },
                { "avro-schema-id", "order-event-v1" }
            },
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            MessageId = Guid.NewGuid().ToString()
        };

        await channel.BasicPublishAsync(
            exchange: exchangeName,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: avroProperties,
            body: avroBytes
        );
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

            var registrationProperties = new BasicProperties
            {
                ContentType = "application/json",
                Headers = new Dictionary<string, object?>
                {
                    { "action", "register" },
                    { "schema-id", schema.Id },
                    { "subject", schema.Subject }
                },
                MessageId = Guid.NewGuid().ToString()
            };

            await channel.BasicPublishAsync(
                exchange: schemaRegistryExchange,
                routingKey: "schema.register",
                mandatory: false,
                basicProperties: registrationProperties,
                body: registrationBody
            );
        }
    }

    private static async Task DemonstrateProducerSchemaEnforcement(IChannel channel)
    {
        var enforcedQueue = "enforced.schema.queue";
        
        // Queue with strict schema enforcement
        await channel.QueueDeclareAsync(
            queue: enforcedQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                { "x-schema-enforcement", "strict" },
                { "x-schema-required-headers", "schema-name,schema-version,schema-validated" },
                { "x-reject-invalid-schema", true }
            }
        );

        // Demonstrate proper schema enforcement in producer
        var producerValidator = new SchemaValidator();
        
        // Valid message
        var validCustomer = new UserV1
        {
            UserId = 12345,
            Username = "User",
            Email = "sample@test.com"
        };

        var validationResult = producerValidator.ValidateUserV1(validCustomer);
        
        if (validationResult.IsValid)
        {
            var enforcedProperties = new BasicProperties
            {
                ContentType = "application/json",
                Headers = new Dictionary<string, object?>
                {
                    { "schema-name", "User" },
                    { "schema-version", "2.0.0" },
                    { "schema-validated", true },
                    { "schema-validation-timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
                    { "producer-validation-passed", true }
                }
            };

            await channel.BasicPublishAsync(
                exchange: "",
                routingKey: enforcedQueue,
                mandatory: false,
                basicProperties: enforcedProperties,
                body: Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(validCustomer))
            );
        }
        else
        {
            foreach (var error in validationResult.Errors)
            {
                Console.WriteLine($"        • {error}");
            }
        }
    }
}

// Schema Validation Helper Class
public class SchemaValidator
{
    private readonly JsonSchema _userV1Schema;
    private readonly JsonSchema _userV2Schema;

    public SchemaValidator()
    {
        var generator = new JsonSchemaGenerator(new SystemTextJsonSchemaGeneratorSettings());
        _userV1Schema = generator.Generate(typeof(UserV1));
        _userV2Schema = generator.Generate(typeof(UserV2));
    }

    public ValidationResult ValidateUserV1(UserV1 user)
    {
        var json = JsonConvert.SerializeObject(user);
        var errors = _userV1Schema.Validate(json);
        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors.Select(e => e.ToString()).ToList()
        };
    }

    public ValidationResult ValidateUser(UserV2 user)
    {
        var json = JsonConvert.SerializeObject(user);
        var errors = _userV2Schema.Validate(json);
        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors.Select(e => e.ToString()).ToList()
        };
    }
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
}

// Domain Models for JSON Schema demonstration
public class UserV1
{
    public int UserId { get; set; }
    public required string Username { get; set; }
    public required string Email { get; set; }
}

public class UserV2
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }

    // New optional fields for evolution
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateTime? CreatedDate { get; set; }
}
