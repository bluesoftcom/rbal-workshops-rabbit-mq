using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog;
using Serilog.Extensions.Hosting;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;
using System.Text.Json;

namespace MonitoringTracingApp;

/// <summary>
/// Comprehensive RabbitMQ producer/consumer with monitoring, logging, metrics and tracing
/// This application demonstrates:
/// - Structured logging with Serilog
/// - Metrics collection for Grafana using OpenTelemetry
/// - Distributed tracing implementation
/// - Console metrics and tracing output
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        // Configure Serilog first for early logging
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console(outputTemplate: 
                "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        try
        {
            Log.Information("Starting MonitoringTracingApp - RabbitMQ with comprehensive observability");

            var builder = Host.CreateDefaultBuilder(args);

            // Configure Serilog
            builder.UseSerilog((context, configuration) =>
            {
                configuration
                    .MinimumLevel.Information()
                    .Enrich.FromLogContext()
                    .WriteTo.Console(outputTemplate:
                        "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj} {Properties}{NewLine}{Exception}");
            });

            // Add services
            builder.ConfigureServices((context, services) =>
            {
                services.AddSingleton<RabbitMQService>();
                services.AddSingleton<MessageProducer>();
                services.AddSingleton<MessageConsumer>();
                services.AddHostedService<MonitoringService>();

                // Configure OpenTelemetry
                services.AddOpenTelemetry()
                    .ConfigureResource(resource => resource
                        .AddService("MonitoringTracingApp", "1.0.0")
                        .AddAttributes(new Dictionary<string, object>
                        {
                            ["service.name"] = "rabbitmq-monitoring-app",
                            ["service.version"] = "1.0.0",
                            ["service.instance.id"] = Environment.MachineName
                        }))
                    .WithTracing(tracing => tracing
                        .AddSource("MonitoringTracingApp")
                        .SetSampler(new AlwaysOnSampler())
                        .AddConsoleExporter())
                    .WithMetrics(metrics => metrics
                        .AddMeter("MonitoringTracingApp")
                        .AddConsoleExporter());
            });

            var app = builder.Build();

            // Get services
            var services = app.Services;
            var producer = services.GetRequiredService<MessageProducer>();
            var consumer = services.GetRequiredService<MessageConsumer>();

            Log.Information("Starting message production and consumption...");

            // Start consumer first
            _ = Task.Run(() => consumer.StartAsync());

            // Give consumer time to start
            await Task.Delay(1000);

            // Produce some messages
            for (int i = 0; i < 10; i++)
            {
                var message = new
                {
                    Id = Guid.NewGuid(),
                    Timestamp = DateTime.UtcNow,
                    Content = $"Test message {i + 1}",
                    Sequence = i + 1
                };

                await producer.PublishAsync(message);
                await Task.Delay(2000); // Wait 2 seconds between messages
            }

            Log.Information("Message production completed. Consumer continues running...");
            Log.Information("Check console output for metrics and traces");
            Log.Information("Press Ctrl+C to stop the application");

            // Keep the application running
            var cancellationTokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cancellationTokenSource.Cancel();
                Log.Information("Shutdown requested by user");
            };

            await Task.Delay(-1, cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
        }
        finally
        {
            Log.Information("Application shutting down...");
            Log.CloseAndFlush();
        }
    }
}

/// <summary>
/// RabbitMQ connection and channel management service
/// </summary>
public class RabbitMQService : IDisposable
{
    private readonly ILogger<RabbitMQService> _logger;
    private IConnection? _connection;
    private IChannel? _channel;
    private readonly ActivitySource _activitySource;
    private readonly Meter _meter;
    private readonly Counter<long> _connectionCounter;
    
    public RabbitMQService(ILogger<RabbitMQService> logger)
    {
        _logger = logger;
        _activitySource = new ActivitySource("MonitoringTracingApp");
        _meter = new Meter("MonitoringTracingApp");
        _connectionCounter = _meter.CreateCounter<long>("rabbitmq_connections_total", "Total RabbitMQ connections created");
        
        _ = InitializeConnectionAsync();
    }

    private async Task InitializeConnectionAsync()
    {
        using var activity = _activitySource.StartActivity("RabbitMQ.Connection.Initialize");
        
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = "localhost",
                Port = 5672,
                UserName = "guest",
                Password = "guest",
                VirtualHost = "/",
                RequestedConnectionTimeout = TimeSpan.FromSeconds(30),
                RequestedHeartbeat = TimeSpan.FromSeconds(60)
            };

            _connection = await factory.CreateConnectionAsync("MonitoringTracingApp");
            _channel = await _connection.CreateChannelAsync();

            // Declare queue
            await _channel.QueueDeclareAsync(
                queue: "monitoring_queue",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            _connectionCounter.Add(1, new KeyValuePair<string, object?>("status", "success"));
            
            _logger.LogInformation("RabbitMQ connection established successfully");
            activity?.SetStatus(ActivityStatusCode.Ok);
            activity?.SetTag("rabbitmq.host", "localhost");
            activity?.SetTag("rabbitmq.queue", "monitoring_queue");
        }
        catch (Exception ex)
        {
            _connectionCounter.Add(1, new KeyValuePair<string, object?>("status", "failed"));
            
            _logger.LogError(ex, "Failed to establish RabbitMQ connection");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public async Task<IChannel> GetChannelAsync()
    {
        if (_channel == null || _channel.IsClosed)
        {
            _logger.LogWarning("Channel is null or closed, reinitializing connection");
            await InitializeConnectionAsync();
        }
        
        return _channel!;
    }

    public void Dispose()
    {
        using var activity = _activitySource.StartActivity("RabbitMQ.Connection.Dispose");
        
        try
        {
            _channel?.CloseAsync();
            _channel?.Dispose();
            _connection?.CloseAsync();
            _connection?.Dispose();
            _activitySource.Dispose();
            _meter.Dispose();
            
            _logger.LogInformation("RabbitMQ connection disposed successfully");
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing RabbitMQ connection");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        }
    }
}

/// <summary>
/// Message producer with comprehensive monitoring
/// </summary>
public class MessageProducer
{
    private readonly RabbitMQService _rabbitMQService;
    private readonly ILogger<MessageProducer> _logger;
    private readonly ActivitySource _activitySource;
    private readonly Meter _meter;
    private readonly Counter<long> _messagesPublished;
    private readonly Histogram<double> _publishDuration;

    public MessageProducer(RabbitMQService rabbitMQService, ILogger<MessageProducer> logger)
    {
        _rabbitMQService = rabbitMQService;
        _logger = logger;
        _activitySource = new ActivitySource("MonitoringTracingApp");
        _meter = new Meter("MonitoringTracingApp");
        _messagesPublished = _meter.CreateCounter<long>("messages_published_total", "Total messages published");
        _publishDuration = _meter.CreateHistogram<double>("message_publish_duration_seconds", "Duration of message publishing");
    }

    public async Task PublishAsync<T>(T message)
    {
        using var activity = _activitySource.StartActivity("Message.Publish");
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var channel = await _rabbitMQService.GetChannelAsync();
            var messageId = Guid.NewGuid().ToString();
            var correlationId = Activity.Current?.Id ?? Guid.NewGuid().ToString();
            
            var messageJson = JsonSerializer.Serialize(message, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            var body = Encoding.UTF8.GetBytes(messageJson);

            var properties = new BasicProperties
            {
                Persistent = true,
                MessageId = messageId,
                CorrelationId = correlationId,
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                Headers = new Dictionary<string, object?>
                {
                    ["traceId"] = Activity.Current?.TraceId.ToString() ?? "",
                    ["spanId"] = Activity.Current?.SpanId.ToString() ?? ""
                }
            };

            await channel.BasicPublishAsync(
                exchange: "",
                routingKey: "monitoring_queue",
                body: body,
                basicProperties: properties);

            stopwatch.Stop();
            var duration = stopwatch.Elapsed.TotalSeconds;

            _messagesPublished.Add(1, 
                new KeyValuePair<string, object?>("queue", "monitoring_queue"),
                new KeyValuePair<string, object?>("status", "success"));
            _publishDuration.Record(duration, 
                new KeyValuePair<string, object?>("queue", "monitoring_queue"));

            _logger.LogInformation("Message published successfully. MessageId: {MessageId}, CorrelationId: {CorrelationId}, Duration: {Duration}ms",
                messageId, correlationId, stopwatch.ElapsedMilliseconds);

            activity?.SetTag("message.id", messageId);
            activity?.SetTag("message.correlation_id", correlationId);
            activity?.SetTag("message.size", body.Length);
            activity?.SetTag("rabbitmq.queue", "monitoring_queue");
            activity?.SetStatus(ActivityStatusCode.Ok);

            await Task.Delay(10); // Simulate some async work
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _messagesPublished.Add(1, 
                new KeyValuePair<string, object?>("queue", "monitoring_queue"),
                new KeyValuePair<string, object?>("status", "failed"));

            _logger.LogError(ex, "Failed to publish message after {Duration}ms", stopwatch.ElapsedMilliseconds);
            
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}

/// <summary>
/// Message consumer with comprehensive monitoring
/// </summary>
public class MessageConsumer
{
    private readonly RabbitMQService _rabbitMQService;
    private readonly ILogger<MessageConsumer> _logger;
    private readonly ActivitySource _activitySource;
    private readonly Meter _meter;
    private readonly Counter<long> _messagesConsumed;
    private readonly Histogram<double> _processingDuration;

    public MessageConsumer(RabbitMQService rabbitMQService, ILogger<MessageConsumer> logger)
    {
        _rabbitMQService = rabbitMQService;
        _logger = logger;
        _activitySource = new ActivitySource("MonitoringTracingApp");
        _meter = new Meter("MonitoringTracingApp");
        _messagesConsumed = _meter.CreateCounter<long>("messages_consumed_total", "Total messages consumed");
        _processingDuration = _meter.CreateHistogram<double>("message_processing_duration_seconds", "Duration of message processing");
    }

    public async Task StartAsync()
    {
        var channel = await _rabbitMQService.GetChannelAsync();
        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (model, ea) =>
        {
            var stopwatch = Stopwatch.StartNew();
            
            // Extract trace context from message headers
            var parentTraceId = ea.BasicProperties?.Headers?.ContainsKey("traceId") == true 
                ? ea.BasicProperties.Headers["traceId"]?.ToString() 
                : null;
            
            using var activity = _activitySource.StartActivity("Message.Consume");
            
            try
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                
                // Simulate message processing
                await ProcessMessageAsync(message, ea.BasicProperties?.MessageId ?? "unknown", ea.BasicProperties?.CorrelationId ?? "unknown");
                
                await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
                
                stopwatch.Stop();
                var duration = stopwatch.Elapsed.TotalSeconds;

                _messagesConsumed.Add(1, 
                    new KeyValuePair<string, object?>("queue", "monitoring_queue"),
                    new KeyValuePair<string, object?>("status", "success"));
                _processingDuration.Record(duration, 
                    new KeyValuePair<string, object?>("queue", "monitoring_queue"));

                _logger.LogInformation("Message processed successfully. MessageId: {MessageId}, CorrelationId: {CorrelationId}, Duration: {Duration}ms",
                    ea.BasicProperties?.MessageId ?? "unknown", ea.BasicProperties?.CorrelationId ?? "unknown", stopwatch.ElapsedMilliseconds);

                activity?.SetTag("message.id", ea.BasicProperties?.MessageId ?? "unknown");
                activity?.SetTag("message.correlation_id", ea.BasicProperties?.CorrelationId ?? "unknown");
                activity?.SetTag("message.size", body.Length);
                activity?.SetTag("rabbitmq.queue", "monitoring_queue");
                activity?.SetTag("parent.trace_id", parentTraceId);
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                
                _messagesConsumed.Add(1, 
                    new KeyValuePair<string, object?>("queue", "monitoring_queue"),
                    new KeyValuePair<string, object?>("status", "failed"));

                _logger.LogError(ex, "Failed to process message. MessageId: {MessageId}, Duration: {Duration}ms",
                    ea.BasicProperties?.MessageId ?? "unknown", stopwatch.ElapsedMilliseconds);
                
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                
                // Reject and requeue the message
                await channel.BasicRejectAsync(deliveryTag: ea.DeliveryTag, requeue: true);
            }
        };

        await channel.BasicConsumeAsync(queue: "monitoring_queue", autoAck: false, consumer: consumer);
        _logger.LogInformation("Started consuming messages from monitoring_queue");
    }

    private async Task ProcessMessageAsync(string message, string messageId, string correlationId)
    {
        using var activity = _activitySource.StartActivity("Message.Process");
        
        // Simulate processing time
        var random = new Random();
        var processingTime = random.Next(100, 500);
        await Task.Delay(processingTime);

        _logger.LogInformation("Processing message: {MessageId} with correlation: {CorrelationId}", 
            messageId, correlationId);

        activity?.SetTag("processing.duration_ms", processingTime);
        activity?.SetTag("message.content_length", message.Length);
    }
}

/// <summary>
/// Background service for monitoring and health checks
/// </summary>
public class MonitoringService : BackgroundService
{
    private readonly ILogger<MonitoringService> _logger;
    private readonly ActivitySource _activitySource;
    private readonly Meter _meter;
    private readonly ObservableGauge<long> _memoryUsage;
    private readonly ObservableGauge<double> _cpuUsage;

    public MonitoringService(ILogger<MonitoringService> logger)
    {
        _logger = logger;
        _activitySource = new ActivitySource("MonitoringTracingApp");
        _meter = new Meter("MonitoringTracingApp");
        
        _memoryUsage = _meter.CreateObservableGauge<long>("process_memory_usage_bytes", 
            () => GC.GetTotalMemory(false));
        _cpuUsage = _meter.CreateObservableGauge<double>("process_cpu_usage_percent", 
            () => GetCpuUsage());
    }

    private static double GetCpuUsage()
    {
        var process = Process.GetCurrentProcess();
        return process.TotalProcessorTime.TotalMilliseconds / Environment.ProcessorCount;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MonitoringService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            using var activity = _activitySource.StartActivity("Monitoring.HealthCheck");
            
            try
            {
                // Record system metrics
                var memoryUsage = GC.GetTotalMemory(false);
                var process = Process.GetCurrentProcess();
                
                _logger.LogInformation("Health check - Memory: {MemoryMB}MB, Threads: {ThreadCount}", 
                    memoryUsage / 1024 / 1024, process.Threads.Count);

                activity?.SetTag("memory.usage_mb", memoryUsage / 1024 / 1024);
                activity?.SetTag("process.thread_count", process.Threads.Count);
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during health check");
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }

        _logger.LogInformation("MonitoringService stopped");
    }
}
