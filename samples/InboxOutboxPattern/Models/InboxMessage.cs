using System;

namespace InboxOutboxPattern;

// Inbox Pattern Entity
public class InboxMessage
{
    public int Id { get; set; }
    public string MessageId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public bool IsProcessed { get; set; } = false;
    public DateTime? ProcessedAt { get; set; }
    public int RetryCount { get; set; } = 0;
    public string? ErrorMessage { get; set; }
    
    // Source information
    public string? CorrelationId { get; set; }
    public string? SourceExchange { get; set; }
    public string? SourceRoutingKey { get; set; }
}