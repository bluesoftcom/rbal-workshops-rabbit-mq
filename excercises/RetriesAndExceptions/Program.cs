using RabbitMQ.Client;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Reflection.PortableExecutable;
using System.Text;

public class Program
{
    public static async Task Main(string[] args)
    {
        #region Initialize RabbitMQ Connection
        string host = ConfigurationManager.AppSettings["host"] ?? "localhost";
        int configPort;
        int port = int.TryParse(ConfigurationManager.AppSettings["port"], out configPort) ? configPort : 5672;
        string userName = ConfigurationManager.AppSettings["userName"] ?? "guest";
        string password = ConfigurationManager.AppSettings["password"] ?? "guest";
        string virtualHost = ConfigurationManager.AppSettings["virtualHost"] ?? "/";

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

        Console.WriteLine("Connected to RabbitMQ");
        #endregion

        var exchangeName = "ex.payments";
        var queue = "q.payments";

        await ManageRabbitMQObjects(ch, exchangeName, queue);

        var consumerStates = new string[]
        {
            "ok", "failed"
        };

        var random = new Random();

        for (int i = 0; i < 10; i++)
        {
            var currentState = consumerStates[random.Next(consumerStates.Length)];

            bool published = await PublishMessageAsync(
                channel: ch,
                payload: currentState,
                exchange: exchangeName,
                routingKey: string.Empty,
                schemaVersion: "v1",
                messageType: "user.created",
                correlationId: ""
            );
        }
    }

    private static async Task ManageRabbitMQObjects(IChannel ch, string exchangeName, string queueName)
    {
        // Declare the DLQ and DLX

        await ch.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false
        );

        var arguments = new Dictionary<string, object?>
        {
            //TODO: Queue configuration
            //{ "x-dead-letter-exchange", $"{exchangeName}.dlx"},
            //{"x-dead-letter-routing-key", string.Empty},
            //{ "x-queue-type", "quorum" },
            //{ "x-delivery-limit", 5 },
            //{ "x-message-ttl", 10000 } // 10 seconds
        };

        await ch.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: arguments
        );

        await ch.QueueBindAsync(
            queue: queueName,
            exchange: exchangeName,
            routingKey: string.Empty
        );
    }

    private static async Task<bool> PublishMessageAsync(
        IChannel channel,
        string payload,
        string exchange,
        string routingKey,
        string schemaVersion,
        string messageType,
        string? correlationId = null
    )
    {
        try
        {
            byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
            var messageId = Guid.NewGuid().ToString();

            var properties = new BasicProperties
            {
                ContentType = "application/json",
                MessageId = messageId,
                CorrelationId = correlationId ?? Guid.NewGuid().ToString(),
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                Headers = new Dictionary<string, object?>
                {
                    { "x-retry-count", "0" },
                    { "x-max-retries", "5" },
                    { "x-delay", "10000" }
                }
            };

            await channel.BasicPublishAsync(exchange, routingKey, false, properties, payloadBytes);

            return true;
        }
        catch
        {
            return false;
        }
    }
}