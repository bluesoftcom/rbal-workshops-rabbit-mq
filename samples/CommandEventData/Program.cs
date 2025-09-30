using System;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using System.Configuration;
using RabbitMQSimpleStartup.Models;

public class CommandEventData
{
    public static async Task Main()
    {
        string host = ConfigurationManager.AppSettings["host"] ?? string.Empty;
        int port = 5671;
        int configPort;
        port = int.TryParse(ConfigurationManager.AppSettings["port"], out configPort) ? configPort : port;
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

        await ch.ExchangeDeclareAsync(exchange: "cmd.exchange", type: "direct", durable: true);
        await ch.ExchangeDeclareAsync(exchange: "evt.exchange", type: "topic", durable: true);
        await ch.ExchangeDeclareAsync(exchange: "data.exchange", type: "fanout", durable: true);

        string routingKeyCmd = "billing.invoice.issue";
        string routingKeyEvt = "order.created.v1";

        // Command
        InvoiceCommand invoiceCmd = new InvoiceCommand
        {
            Id = Guid.NewGuid(),
            RoutingKey = "billing.invoice.issue",
            CreateDateTime = DateTime.Now,
            InvoiceNumber = "INV-123",
            InvoiceDetails = new InvoiceDetails { Amount = 120.50, Currency = "EUR" }
        };

        byte[] bodyCmd = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(invoiceCmd));

        BasicProperties propertiesCmd = new BasicProperties
        {
            Persistent = true,
            MessageId = invoiceCmd.Id.ToString(),
            Type = nameof(InvoiceCommand),
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };

        await ch.BasicPublishAsync(exchange: "cmd.exchange", routingKey: routingKeyCmd, basicProperties: propertiesCmd, body: bodyCmd, mandatory: true);

        // Event
        // ...existing code for event publishing...

        // Data
        // ...existing code for data publishing...
    }
}
