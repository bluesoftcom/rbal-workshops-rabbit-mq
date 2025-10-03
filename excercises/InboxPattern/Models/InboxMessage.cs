using System;

namespace InboxOutboxPattern;

// Inbox Pattern Entity
public class InboxMessage
{
    // Identification
    public int Id { get; set; }
    public string? CorrelationId { get; set; }
    public string? SourceExchange { get; set; }
    public string? SourceRoutingKey { get; set; }
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
