# RabbitMQ Schema Definition Implementation Guide
## Using Official RabbitMQ.Client Library

This guide demonstrates how to implement schema definition in RabbitMQ queues using the official RabbitMQ.Client library.

## 🔧 Core Implementation Patterns

### 1. Queue Arguments for Schema Metadata

```csharp
var schemaQueueArgs = new Dictionary<string, object?>
{
    // Schema identification
    { "x-schema-name", "CustomerEvent" },
    { "x-schema-version", "2.0.0" },
    { "x-schema-format", "json-schema" },
    { "x-schema-compatibility", "backward" },
    
    // Schema definition (can be full schema or reference)
    { "x-schema-registry-id", "customer-event-v2" },
    { "x-schema-subject", "customer.events" },
    
    // Validation settings
    { "x-schema-validation", "strict" },
    { "x-schema-required", true },
    
    // Additional queue policies
    { "x-message-ttl", 86400000 }, // 24 hours
    { "x-max-length", 10000 },
    { "x-overflow", "reject-publish" }
};

await channel.QueueDeclareAsync(
    queue: queueName,
    durable: true,
    exclusive: false,
    autoDelete: false,
    arguments: schemaQueueArgs
);
```

### 2. Message Properties for Schema Information

```csharp
var properties = new BasicProperties
{
    // Standard AMQP properties
    ContentType = "application/json",
    ContentEncoding = "utf-8",
    MessageId = Guid.NewGuid().ToString(),
    Type = "OrderCreated", // Message type
    
    // Custom headers for schema metadata
    Headers = new Dictionary<string, object?>
    {
        // Schema identification
        { "schema-name", "Order" },
        { "schema-version", "1.2.0" },
        { "schema-format", "json-schema" },
        { "schema-registry-subject", "orders" },
        { "schema-registry-id", 42 },
        
        // Validation information
        { "schema-validated", true },
        { "schema-validation-timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
        { "schema-compatibility-mode", "backward" },
        
        // Producer information
        { "producer-schema-version", "1.2.0" },
        { "producer-client-id", "order-service-v2.1" }
    }
};

await channel.BasicPublishAsync(
    exchange: "",
    routingKey: queueName,
    mandatory: false,
    basicProperties: properties,
    body: messageBody
);
```

### 3. Exchange-based Schema Registry

```csharp
// Create schema registry infrastructure
var schemaRegistryExchange = "schema-registry";
var schemaStorageQueue = "schema-registry.schemas";

await channel.ExchangeDeclareAsync(
    exchange: schemaRegistryExchange,
    type: ExchangeType.Topic,
    durable: true,
    autoDelete: false
);

await channel.QueueDeclareAsync(
    queue: schemaStorageQueue,
    durable: true,
    exclusive: false,
    autoDelete: false,
    arguments: new Dictionary<string, object?>
    {
        { "x-message-ttl", 0 }, // Persistent storage
        { "x-max-length", 100000 } // Large capacity for schemas
    }
);

await channel.QueueBindAsync(schemaStorageQueue, schemaRegistryExchange, "schema.register.*");

// Register a schema
var schemaRegistration = new
{
    Action = "REGISTER",
    Subject = "customer.events",
    Version = "2.0.0",
    SchemaId = Guid.NewGuid().ToString(),
    SchemaFormat = "json-schema",
    SchemaDefinition = customerSchema,
    Compatibility = "BACKWARD",
    RegisteredBy = "SchemaApp",
    RegisteredAt = DateTime.UtcNow
};
```

### 4. Schema Validation Middleware Pattern

```csharp
// Create validation pipeline
var validationExchange = "validation.pipeline";
var incomingQueue = "validation.incoming";
var validQueue = "validation.valid";
var invalidQueue = "validation.invalid";

await channel.ExchangeDeclareAsync(validationExchange, ExchangeType.Direct, durable: true);

// Route messages based on validation results
var routingKey = isValid ? "valid" : "invalid";
var validationProperties = new BasicProperties
{
    ContentType = "application/json",
    Headers = new Dictionary<string, object?>
    {
        { "validation-result", isValid },
        { "validation-timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
        { "validation-errors-count", validationErrors.Count },
        { "schema-version", "2.0.0" }
    }
};
```

### 5. Producer Schema Enforcement

```csharp
public class SchemaValidator
{
    private readonly JsonSchema _customerV2Schema;

    public SchemaValidator()
    {
        var generator = new JsonSchemaGenerator(new SystemTextJsonSchemaGeneratorSettings());
        _customerV2Schema = generator.Generate(typeof(CustomerV2));
    }

    public ValidationResult ValidateCustomerV2(CustomerV2 customer)
    {
        var json = JsonConvert.SerializeObject(customer);
        var errors = _customerV2Schema.Validate(json);
        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors.Select(e => e.ToString()).ToList()
        };
    }
}

// In producer code:
var producerValidator = new SchemaValidator();
var validationResult = producerValidator.ValidateCustomerV2(customer);

if (validationResult.IsValid)
{
    // Publish with schema enforcement headers
    var enforcedProperties = new BasicProperties
    {
        Headers = new Dictionary<string, object?>
        {
            { "schema-name", "CustomerV2" },
            { "schema-version", "2.0.0" },
            { "schema-validated", true },
            { "producer-validation-passed", true }
        }
    };
    
    await channel.BasicPublishAsync(exchange, routingKey, false, enforcedProperties, messageBody);
}
```

### 6. Consumer Schema Compatibility

```csharp
// Handle different schema versions in consumer
var schemaVersion = properties.Headers?["schema-version"]?.ToString();

switch (schemaVersion)
{
    case "1.0.0":
        var customerV1 = JsonConvert.DeserializeObject<CustomerV1>(messageBody);
        // Handle V1 customer
        break;
        
    case "2.0.0":
        var customerV2 = JsonConvert.DeserializeObject<CustomerV2>(messageBody);
        // Handle V2 customer
        break;
        
    default:
        // Handle unknown version or apply backward compatibility
        break;
}
```

## 📋 Best Practices

### Queue-Level Schema Definition
1. **Use x-* arguments** for custom schema metadata in queue declarations
2. **Store schema references** rather than full schemas in queue arguments
3. **Implement TTL policies** for schema-validated messages
4. **Set up dead letter exchanges** for invalid messages

### Message-Level Schema Information
1. **Always include schema version** in message headers
2. **Use standard content-type** headers (application/json, application/avro)
3. **Add validation timestamps** for audit trails
4. **Include producer identification** for traceability

### Schema Registry Implementation
1. **Use topic exchanges** for flexible schema routing
2. **Implement persistent storage** for schema definitions
3. **Version schemas semantically** (major.minor.patch)
4. **Maintain compatibility matrices** between versions

### Validation Strategies
1. **Validate at producer** before publishing
2. **Implement validation pipelines** for centralized validation
3. **Route invalid messages** to separate queues
4. **Log validation failures** for monitoring

### Consumer Compatibility
1. **Handle multiple schema versions** gracefully
2. **Implement backward compatibility** for field evolution
3. **Use schema headers** to determine message structure
4. **Provide graceful degradation** for unknown fields

## 🚀 Advanced Patterns

### Schema Evolution Pipeline
- **Forward compatibility**: New producers, old consumers
- **Backward compatibility**: Old producers, new consumers  
- **Full compatibility**: Both directions supported
- **Breaking changes**: Coordinated deployment required

### Schema Registry Integration
- **Confluent Schema Registry** compatibility
- **Custom registry** implementation
- **Schema versioning** strategies
- **Migration planning** for schema updates

### Monitoring and Observability
- **Schema validation metrics**
- **Version distribution tracking**
- **Error rate monitoring**
- **Performance impact measurement**

## 💡 Key Advantages

1. **Type Safety**: Schema validation prevents malformed messages
2. **Evolution Support**: Backward/forward compatibility planning
3. **Documentation**: Self-documenting message structures
4. **Tooling Integration**: IDE support and code generation
5. **Quality Assurance**: Automated validation in CI/CD pipelines
6. **Performance**: Efficient serialization with Avro
7. **Interoperability**: Cross-language message contracts

This implementation approach using the official RabbitMQ.Client library provides a robust foundation for schema management in distributed messaging systems.