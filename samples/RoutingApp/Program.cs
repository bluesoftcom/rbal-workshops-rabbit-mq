using System;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using System.Configuration;

public class Routing
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

        await ch.ExchangeDeclareAsync(exchange: "ex.direct", type: ExchangeType.Direct, durable: true, autoDelete: false, arguments: null);
        await ch.ExchangeDeclareAsync(exchange: "ex.topic", type: ExchangeType.Topic, durable: true, autoDelete: false, arguments: null);
        await ch.ExchangeDeclareAsync(exchange: "ex.fanout", type: ExchangeType.Fanout, durable: true, autoDelete: false, arguments: null);

        await ch.QueueDeclareAsync("q.billing", durable: true, exclusive: false, autoDelete: false, arguments: null);
        await ch.QueueDeclareAsync("q.analytics", durable: true, exclusive: false, autoDelete: false, arguments: null);

        await ch.QueueBindAsync("q.billing", "ex.direct", "billing.charge", arguments: null);
        await ch.QueueBindAsync("q.analytics", "ex.topic", "order.*.created", arguments: null);
        await ch.QueueBindAsync("q.analytics", "ex.fanout", "", arguments: null);

        // 3 consumers 1 producer-produces into 3 exchanges
        // configure 3 consumers to rea from these topics
    }
}
