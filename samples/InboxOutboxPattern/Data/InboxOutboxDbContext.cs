using Microsoft.EntityFrameworkCore;

namespace InboxOutboxPattern;

// Database Context
public class InboxOutboxDbContext : DbContext
{
    private readonly string _connectionString;

    public InboxOutboxDbContext(string connectionString)
    {
        _connectionString = connectionString;
    }

    public DbSet<Order> Orders { get; set; }
    public DbSet<OutboxMessage> OutboxMessages { get; set; }
    public DbSet<InboxMessage> InboxMessages { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlServer(_connectionString);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Order configuration
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ProductName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Status).HasMaxLength(50);
        });

        // Outbox Message configuration
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EventType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.AggregateId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Payload).IsRequired();
            entity.Property(e => e.Exchange).HasMaxLength(100);
            entity.Property(e => e.RoutingKey).HasMaxLength(100);
            entity.Property(e => e.CorrelationId).HasMaxLength(100);
            entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
            
            entity.HasIndex(e => new { e.IsProcessed, e.CreatedAt })
                  .HasDatabaseName("IX_OutboxMessages_Processing");
        });

        // Inbox Message configuration
        modelBuilder.Entity<InboxMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MessageId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.EventType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Payload).IsRequired();
            entity.Property(e => e.CorrelationId).HasMaxLength(100);
            entity.Property(e => e.SourceExchange).HasMaxLength(100);
            entity.Property(e => e.SourceRoutingKey).HasMaxLength(100);
            entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
            
            entity.HasIndex(e => e.MessageId)
                  .IsUnique()
                  .HasDatabaseName("IX_InboxMessages_MessageId");
            
            entity.HasIndex(e => new { e.IsProcessed, e.ReceivedAt })
                  .HasDatabaseName("IX_InboxMessages_Processing");
        });

        base.OnModelCreating(modelBuilder);
    }
}