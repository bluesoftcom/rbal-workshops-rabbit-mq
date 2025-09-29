# RabbitMQ Inbox/Outbox Pattern Implementation

## Overview

This application demonstrates the **Inbox/Outbox Pattern** implementation using RabbitMQ as a message broker and AWS RDS SQL Server as the database. These patterns are essential for maintaining data consistency in distributed systems and ensuring reliable message processing.

## What are Inbox/Outbox Patterns?

### Outbox Pattern
The **Transactional Outbox Pattern** ensures that database changes and message publishing happen atomically:
1. Business operations and message publishing happen in the same database transaction
2. Messages are stored in an "outbox" table instead of being published directly
3. A separate process reads from the outbox and publishes messages to the message broker
4. This guarantees that messages are only published if the business transaction succeeds

### Inbox Pattern  
The **Inbox Pattern** ensures idempotent message processing and exactly-once delivery:
1. Incoming messages are first stored in an "inbox" table with a unique message ID
2. Before processing, check if the message was already processed (idempotency)
3. Process the message and mark it as completed in the same transaction
4. Duplicate messages are automatically ignored

## Architecture Components

### 1. Domain Models
- **Order**: Business entity representing customer orders
- **OutboxMessage**: Stores events to be published to RabbitMQ
- **InboxMessage**: Stores received messages for idempotent processing

### 2. Core Classes
- **OutboxProducer**: Creates orders and stores events in outbox table atomically
- **OutboxMessageProcessor**: Background service that publishes outbox messages to RabbitMQ
- **InboxConsumer**: Consumes RabbitMQ messages using inbox pattern for idempotency

### 3. Database Schema
```sql
-- Orders table
CREATE TABLE Orders (
    Id int IDENTITY(1,1) PRIMARY KEY,
    CustomerId int NOT NULL,
    ProductName nvarchar(200) NOT NULL,
    Quantity int NOT NULL,
    Price decimal(18,2) NOT NULL,
    CreatedAt datetime2 NOT NULL,
    Status nvarchar(50) NOT NULL
);

-- Outbox messages table
CREATE TABLE OutboxMessages (
    Id int IDENTITY(1,1) PRIMARY KEY,
    EventType nvarchar(100) NOT NULL,
    AggregateId nvarchar(100) NOT NULL,
    Payload nvarchar(max) NOT NULL,
    CreatedAt datetime2 NOT NULL,
    IsProcessed bit NOT NULL DEFAULT 0,
    ProcessedAt datetime2 NULL,
    RetryCount int NOT NULL DEFAULT 0,
    ErrorMessage nvarchar(1000) NULL,
    Exchange nvarchar(100) NOT NULL,
    RoutingKey nvarchar(100) NOT NULL,
    CorrelationId nvarchar(100) NULL
);

-- Inbox messages table
CREATE TABLE InboxMessages (
    Id int IDENTITY(1,1) PRIMARY KEY,
    MessageId nvarchar(100) NOT NULL UNIQUE,
    EventType nvarchar(100) NOT NULL,
    Payload nvarchar(max) NOT NULL,
    ReceivedAt datetime2 NOT NULL,
    IsProcessed bit NOT NULL DEFAULT 0,
    ProcessedAt datetime2 NULL,
    RetryCount int NOT NULL DEFAULT 0,
    ErrorMessage nvarchar(1000) NULL,
    CorrelationId nvarchar(100) NULL,
    SourceExchange nvarchar(100) NULL,
    SourceRoutingKey nvarchar(100) NULL
);
```

## Configuration

### AWS RDS SQL Server Setup
1. Create an AWS RDS SQL Server instance
2. Configure security groups to allow connections from your application
3. Update the connection string in `App.config`:

```xml
<connectionStrings>
  <add name="DefaultConnection" 
       connectionString="Server=your-instance.region.rds.amazonaws.com,1433;Database=InboxOutboxDemo;User Id=admin;Password=YourPassword;TrustServerCertificate=true;" />
</connectionStrings>
```

### RabbitMQ Configuration
The application uses the shared `App.config` from the parent directory for RabbitMQ settings:
- Host, Port, Username, Password, VirtualHost

## Demo Modes

### 1. Producer with Outbox Pattern
- Creates orders and stores events in outbox table atomically
- Demonstrates transactional consistency between business operations and messaging

### 2. Consumer with Inbox Pattern  
- Consumes messages from RabbitMQ using inbox pattern
- Ensures idempotent processing and exactly-once delivery
- Handles duplicate message detection automatically

### 3. Complete Demo
- Runs producer, consumer, and outbox processor together
- Shows end-to-end message flow with both patterns
- Demonstrates full system operation

### 4. Message Processor
- Standalone outbox message processor
- Publishes pending messages from outbox to RabbitMQ
- Handles retry logic and error management

## Key Benefits

### Reliability
- **Atomicity**: Database changes and message publishing happen together or not at all
- **Consistency**: No lost messages due to database/broker failures
- **Durability**: Messages are persisted in database before being published

### Idempotency
- **Duplicate Detection**: Automatic handling of duplicate message delivery
- **Exactly-Once Processing**: Each message is processed exactly once
- **Fault Tolerance**: System can recover from failures without data corruption

### Observability
- **Message Tracking**: Full audit trail of all messages
- **Error Handling**: Comprehensive error logging and retry mechanisms
- **Monitoring**: Easy to monitor message processing status

## Usage Examples

### Creating Orders with Outbox Pattern
```csharp
var producer = new OutboxProducer(dbContext, rabbitMqFactory);
var order = new Order 
{ 
    CustomerId = 1001, 
    ProductName = "Laptop", 
    Quantity = 1, 
    Price = 1299.99m 
};

// This saves order AND creates outbox message atomically
await producer.CreateOrderAsync(order);
```

### Processing Messages with Inbox Pattern
```csharp
var consumer = new InboxConsumer(dbContext, rabbitMqFactory);

// Automatically handles:
// - Duplicate message detection
// - Idempotent processing  
// - Error handling and retries
await consumer.StartConsumingAsync();
```

### Background Outbox Processing
```csharp
var processor = new OutboxMessageProcessor(dbContext, rabbitMqFactory);

// Continuously processes outbox messages
// - Publishes to RabbitMQ
// - Updates processing status
// - Handles failures and retries
await processor.StartProcessingAsync();
```

## Error Handling

### Outbox Pattern
- Transaction rollback on any failure
- Automatic retry with exponential backoff
- Dead letter handling for persistent failures
- Error logging with full context

### Inbox Pattern
- Duplicate message detection and skip
- Retry logic with configurable limits
- Error message storage for debugging
- Graceful degradation on processing failures

## Monitoring and Troubleshooting

### Database Queries for Monitoring
```sql
-- Check outbox processing status
SELECT 
    IsProcessed,
    COUNT(*) as MessageCount,
    AVG(RetryCount) as AvgRetries
FROM OutboxMessages 
GROUP BY IsProcessed;

-- Check inbox processing status  
SELECT 
    IsProcessed,
    COUNT(*) as MessageCount,
    MAX(ReceivedAt) as LastReceived
FROM InboxMessages 
GROUP BY IsProcessed;

-- Find failed messages
SELECT * FROM OutboxMessages 
WHERE RetryCount >= 3 AND NOT IsProcessed;

SELECT * FROM InboxMessages 
WHERE RetryCount >= 3 AND NOT IsProcessed;
```

### Performance Considerations
- Index on `(IsProcessed, CreatedAt)` for efficient outbox polling
- Index on `MessageId` for fast duplicate detection
- Regular cleanup of processed messages
- Connection pooling for database connections
- Batch processing for high throughput scenarios

## Best Practices

1. **Database Transactions**: Always use transactions for atomicity
2. **Message Deduplication**: Use unique message IDs for idempotency
3. **Error Handling**: Implement comprehensive retry logic
4. **Monitoring**: Set up alerts for failed messages
5. **Cleanup**: Regularly archive processed messages
6. **Connection Management**: Use connection pooling and proper disposal
7. **Configuration**: Externalize all configuration settings
8. **Testing**: Test failure scenarios and recovery procedures

## Running the Application

1. **Setup Database**: Ensure AWS RDS SQL Server is running and accessible
2. **Configure Connection**: Update connection string in App.config
3. **Setup RabbitMQ**: Ensure RabbitMQ is running (local or cloud)
4. **Run Application**: Choose demo mode when prompted
5. **Monitor**: Check console output and database tables for message flow

This implementation provides a production-ready foundation for reliable messaging in distributed systems using the proven inbox/outbox patterns.