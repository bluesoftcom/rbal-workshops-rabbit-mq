using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Configuration;
using System.Text;
using System.Threading.Channels;

public class Program
{
    public static async Task Main(string[] args)
    {
        var queue = "q.payments";

        #region Initialize RabbitMQ Connection
        string host = ConfigurationManager.AppSettings["host"] ?? "localhost";
        int configPort;
        int port = int.TryParse(ConfigurationManager.AppSettings["port"], out configPort) ? configPort : 5672;
        string userName = ConfigurationManager.AppSettings["userName"] ?? "guest";
        string password = ConfigurationManager.AppSettings["password"] ?? "guest";
        string virtualHost = ConfigurationManager.AppSettings["virtualHost"] ?? "/";

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

        // TODO: Add the prefetch count
        // Use BasicQosAsync to allow concurrent processing

        Console.WriteLine("✅ Connected to RabbitMQ\n");
        #endregion

        var consumer = new AsyncEventingBasicConsumer(ch);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            try
            {
                // Implement retry logic using message headers
                // There are a few parameters in the header we want to use:
                //"x-max-retries"
                //"x-delay"
                //"x-retry-count"
                // Delay the task
                // Overwrite the headers properties.Headers["x-retry-count"] = (int)properties.Headers["x-retry-count"] + 1;
                // Afterwards call a publisher with BasicPublishAsync to republish the message

                byte[] payloadBytes = ea.Body.ToArray();
                string status = Encoding.UTF8.GetString(payloadBytes);
                if (status == "failed")
                {   
                    throw new NotImplementedException("Simulating consumer failure");
                }
                Console.WriteLine($"Message {ea.BasicProperties.MessageId} processed correctly");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing message {ea.BasicProperties.MessageId}: {ex.Message}");
            }
        };

        await ch.BasicConsumeAsync(
            queue: queue,
            autoAck: false,
            consumer: consumer
        );

        Console.WriteLine("Consumer started - waiting for messages...");
        Console.ReadLine();
    }
}