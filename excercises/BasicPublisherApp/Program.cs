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
        ConnectionFactory factory = throw new NotImplementedException();

        // Create a connection using connection factory
        IConnection conn = null;
        // Create a channel for the connection
        IChannel ch = null;

        // Declare an exchange we want to produce messages into
        // Provide arguments: exchange, type, durable, autoDelete
        await ch.ExchangeDeclareAsync();

        // Declare a queue we want our messages to go to
        // Provide arguments: queue, durable, exclusive, autoDelete

        // Declare a Binding to the queue,
        // Provide arguments: queue, exchange and routingKey

        // create a message body as string and get the UTF8 encoded bytes
        bytes[] body = null;

        // publish the message with BasicPublishAsync, into the defined exchange, use the correct routingKey

        // Provide the queue to validate the message production
        var res = await ch.BasicGetAsync(queue: "", autoAck: true);
        Console.WriteLine(res != null ? "OK" : "FAILED");
    }
}
