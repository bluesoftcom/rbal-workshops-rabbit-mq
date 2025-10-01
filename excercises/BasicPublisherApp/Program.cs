using System;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using System.Configuration;

public class BasicPublisher
{
    public static async Task Main()
    {
        // Read configuration
        string host = ConfigurationManager.AppSettings["host"] ?? string.Empty;
        int configPort;
        int port = int.TryParse(ConfigurationManager.AppSettings["port"], out configPort) ? configPort : 5671;
        string userName = ConfigurationManager.AppSettings["userName"] ?? string.Empty;
        string password = ConfigurationManager.AppSettings["password"] ?? string.Empty;
        string virtualHost = ConfigurationManager.AppSettings["virtualHost"] ?? string.Empty;

        // Create a factory, provide to the constructor: HostName, Port, UserName, Password, VirtualHost
        // Ensure to add SSL options with Enabled = true and ServerName = host
        ConnectionFactory factory = new ConnectionFactory
        {
            HostName = host,
            Port = port,
            UserName = userName,
            Password = password,
            VirtualHost = virtualHost,
            Ssl = new SslOption
            {
                Enabled = true,
                ServerName = host
            }
        };

        // Create a connection using connection factory
        using IConnection conn = await factory.CreateConnectionAsync();

        // Create a channel for the connection
        using IChannel ch = await conn.CreateChannelAsync();

        // Declare an exchange we want to produce messages into
        // Provide arguments: exchange, type, durable, autoDelete
        await ch.ExchangeDeclareAsync(
            exchange: "ex.example",
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            arguments: null
        );

        // Declare a queue we want our messages to go to
        // Provide arguments: queue, durable, exclusive, autoDelete
        await ch.QueueDeclareAsync(
            queue: "q.green",
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null
        );

        await ch.QueueDeclareAsync(
            queue: "q.blue",
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null
        );

        // Declare a Binding to the queue,
        // Provide arguments: queue, exchange and routingKey
        await ch.QueueBindAsync(
            queue: "q.green",
            exchange: "ex.example",
            routingKey: "green",
            arguments: null
        );
        await ch.QueueBindAsync(
            queue: "q.blue",
            exchange: "ex.example",
            routingKey: "blue",
            arguments: null
        );

        var rnd = new Random();
        string[] options = { "green", "blue" };
        for (int i = 0; i < 100; i++)
        {
            string color = options[rnd.Next(options.Length)];

            // create a message body as string and get the UTF8 encoded bytes
            byte[] body = Encoding.UTF8.GetBytes("This is a " + color + " message!");

            // publish the message with BasicPublishAsync, into the defined exchange, use the correct routingKey
            await ch.BasicPublishAsync(
                exchange: "ex.example",
                routingKey: color,
                body: body
            );
        }
        
    }
}
