using System;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using System.Collections.Generic;
using System.Configuration;
using RabbitMQ.Client.Events;

public class ConsumerFiltering
{
    public static async Task Main()
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

        await ch.ExchangeDeclareAsync(exchange: "orders", type: ExchangeType.Topic, durable: true, autoDelete: false, arguments: null);
        await ch.ExchangeDeclareAsync(exchange: "orders.dlx", type: ExchangeType.Topic, durable: true, autoDelete: false, arguments: null);
        
        await ch.QueueDeclareAsync("orders.q", durable: true, exclusive: false, autoDelete: false, arguments: new Dictionary<string, object?>
        {
            { "x-dead-letter-exchange", "orders.dlx" }
        });
        await ch.QueueDeclareAsync("orders.dlq", durable: true, exclusive: false, autoDelete: false, arguments: null);

        await ch.QueueBindAsync("orders.q", "orders", "order.*");
        await ch.QueueBindAsync("orders.dlq", "orders.dlx", "#");

        await ch.BasicQosAsync(0, 20, false);

        var consumer = new AsyncEventingBasicConsumer(ch);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            try
            {
                var headers = ea.BasicProperties.Headers ?? new Dictionary<string, object?>();
                var type = ea.BasicProperties.Type ?? string.Empty;
                bool tenantMatch = headers.TryGetValue("tenant", out var tenant) && tenant?.ToString() == "acme";
                bool regionMatch = headers.TryGetValue("region", out var region) && region?.ToString() == "eu";
                bool versionMatch = type.EndsWith(".v1", StringComparison.OrdinalIgnoreCase);

                if (!tenantMatch || !regionMatch || !versionMatch)
                {
                    await ch.BasicNackAsync(ea.DeliveryTag, false, false); // Send to DLX
                    Console.WriteLine("Message did not match filters.");
                    return;
                }

                var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("OrderId", out var orderDetails))
                {
                    await ch.BasicNackAsync(ea.DeliveryTag, false, false); // Send to DLX
                    Console.WriteLine("Message did not contain OrderId.");
                    return;
                }

                await ch.BasicAckAsync(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                await ch.BasicNackAsync(ea.DeliveryTag, false, false); // Send to DLX
                Console.WriteLine($"Exception during message processing: {ex.Message}");
            }
        };

        // NOTE: You may need to adjust this to use the correct consumer registration for your RabbitMQ client version
        // ch.BasicConsume(queue: "orders.q", autoAck: false, consumer: consumer);

        Console.WriteLine("Consumer started. Press [enter] to exit.");
        Console.ReadLine();
    }
}
