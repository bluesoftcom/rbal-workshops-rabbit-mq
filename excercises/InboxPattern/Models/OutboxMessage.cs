using System;

namespace InboxOutboxPattern;

// Outbox Pattern Entity
public class OutboxMessage
{
    // Identification
    public int Id { get; set; }
    public string? CorrelationId { get; set; }
    public string? Exchange { get; set; }
    public string? RoutingKey { get; set; }
    // Message native properties
    public string Payload { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    //Message state
    public DateTime ProducedAt { get; set; } = DateTime.UtcNow;
    public bool IsProcessed { get; set; } = false;
    public DateTime? ProcessedAt { get; set; }
    // Error handling
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; } = 0;
}
