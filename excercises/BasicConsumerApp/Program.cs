using System.Configuration;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

public class BasicConsumer
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

        // Create a connection factory

        // Create a connection using connection factory

        // Create a channel for the connection

        // define the queue you want to read from, provide arguments: exchange, durable, exclusive, autoDelete
        await ch.QueueDeclareAsync();

        // define a consumer with AsyncEventingBasicConsumer
        var consumer = null;

        // define RecievedAsync event that will: read the message body, get the message string using UTF8 encoding,
        // output to the console the recieved message, acknowledge the message with BasicAckAsync
        consumer.ReceivedAsync += async (model, ea) =>
        {

        };

        // Run the BasicConsumeAsync to start consuming messages from the queue

        Console.WriteLine("Basic consumer started. Press [enter] to exit.");
        Console.ReadLine();
    }
}
