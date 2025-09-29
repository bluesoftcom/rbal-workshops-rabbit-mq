using System;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using System.Configuration;

public class BasicPublisher
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

        await ch.ExchangeDeclareAsync(exchange: "amq.direct", type: "direct", durable: true, autoDelete: false, arguments: null);
        await ch.QueueDeclareAsync(queue: "q1", durable: false, exclusive: false, autoDelete: true, arguments: null);
        await ch.QueueBindAsync(queue: "q1", exchange: "amq.direct", routingKey: "1", arguments: null);

        var body = Encoding.UTF8.GetBytes("hello");
        await ch.BasicPublishAsync(exchange: "amq.direct", routingKey: "1", body: body);

        var res = await ch.BasicGetAsync(queue: "q1", autoAck: true);
        Console.WriteLine(res != null ? "OK" : "FAILED");
    }
}
