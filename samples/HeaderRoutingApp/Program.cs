using System;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using System.Collections.Generic;
using System.Configuration;

public class HeaderRouting
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

        await ch.ExchangeDeclareAsync(exchange: "ex.headers", type: ExchangeType.Headers, durable: true, autoDelete: false, arguments: null);

        await ch.QueueDeclareAsync("q.headers", durable: true, exclusive: false, autoDelete: false, arguments: null);

        var headerArgs = new Dictionary<string, object>
        {
            { "x-match", "all" },
            { "type", "invoice" },
            { "currency", "EUR" }
        };

        await ch.QueueBindAsync("q.headers", "ex.headers", routingKey: string.Empty, arguments: headerArgs);

        var message = new { InvoiceNumber = "INV-123", Amount = 120.50, Currency = "EUR" };
        var body = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(message));

        var properties = new BasicProperties
        {
            Persistent = true,
            Headers = new Dictionary<string, object>
            {
                { "type", "invoice" },
                { "currency", "EUR" }
            }
        };

        await ch.BasicPublishAsync(exchange: "ex.headers", routingKey: string.Empty, basicProperties: properties, body: body, mandatory: true);

        Console.WriteLine("Message published to headers exchange.");
    }
}
