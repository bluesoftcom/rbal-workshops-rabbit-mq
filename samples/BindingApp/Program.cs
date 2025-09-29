using System;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using System.Configuration;

public class Binding
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

        await ch.ExchangeDeclareAsync(exchange: "ex.binding", type: ExchangeType.Direct, durable: true, autoDelete: false, arguments: null);
        await ch.QueueDeclareAsync("q.binding", durable: true, exclusive: false, autoDelete: false, arguments: null);

        await ch.QueueBindAsync("q.binding", "ex.binding", "binding.key", arguments: null);

        var body = Encoding.UTF8.GetBytes("Binding test message");
        await ch.BasicPublishAsync(exchange: "ex.binding", routingKey: "binding.key", body: body);

        Console.WriteLine("Message published to binding exchange and queue.");
    }
}
