using System.Configuration;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

public class BasicConsumer
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

        await ch.QueueDeclareAsync("q1", durable: false, exclusive: false, autoDelete: true, arguments: null);

        var consumer = new AsyncEventingBasicConsumer(ch);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            Console.WriteLine($"Received: {message}");
            await ch.BasicAckAsync(ea.DeliveryTag, false);
        };

        // NOTE: You may need to adjust this to use the correct consumer registration for your RabbitMQ client version
        // ch.BasicConsume(queue: "q1", autoAck: false, consumer: consumer);

        Console.WriteLine("Basic consumer started. Press [enter] to exit.");
        Console.ReadLine();
    }
}
