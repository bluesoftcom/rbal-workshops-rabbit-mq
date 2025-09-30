using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using Newtonsoft.Json;

namespace InboxOutboxPattern;

// Outbox Producer - Implements Transactional Outbox Pattern
public class OutboxProducer
{
    private readonly InboxOutboxDbContext _dbContext;
    private readonly ConnectionFactory _rabbitMqFactory;

    public OutboxProducer(InboxOutboxDbContext dbContext, ConnectionFactory rabbitMqFactory)
    {
        _dbContext = dbContext;
        _rabbitMqFactory = rabbitMqFactory;
    }

    public async Task CreateOrderAsync(Order order)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        
        try
        {
            // 1. Save business entity
            _dbContext.Orders.Add(order);
            await _dbContext.SaveChangesAsync();

            // 2. Create outbox message in same transaction
            var orderCreatedEvent = new
            {
                OrderId = order.Id,
                CustomerId = order.CustomerId,
                ProductName = order.ProductName,
                Quantity = order.Quantity,
                Price = order.Price,
                CreatedAt = order.CreatedAt,
                EventId = Guid.NewGuid().ToString(),
                EventVersion = "1.0"
            };

            var outboxMessage = new OutboxMessage
            {
                EventType = "OrderCreated",
                AggregateId = order.Id.ToString(),
                Payload = JsonConvert.SerializeObject(orderCreatedEvent),
                Exchange = "orders.exchange",
                RoutingKey = "order.created",
                CorrelationId = Guid.NewGuid().ToString()
            };

            _dbContext.OutboxMessages.Add(outboxMessage);
            await _dbContext.SaveChangesAsync();

            // 3. Commit transaction - ensures atomicity
            await transaction.CommitAsync();

            Console.WriteLine($"Order {order.Id} and outbox message saved atomically");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Console.WriteLine($"Transaction failed: {ex.Message}");
            throw;
        }
    }
}