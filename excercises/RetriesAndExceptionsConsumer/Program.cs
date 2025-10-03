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
        await ch.BasicQosAsync(0, 10, false);

        Console.WriteLine("✅ Connected to RabbitMQ\n");
        #endregion

        var consumer = new AsyncEventingBasicConsumer(ch);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            byte[] payloadBytes = ea.Body.ToArray();

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

                string status = Encoding.UTF8.GetString(payloadBytes);
                if (status == "failed")
                {
                    throw new NotImplementedException("Simulating consumer failure");
                }
                Console.WriteLine($"Message {ea.BasicProperties.MessageId} processed correctly");

                await ch.BasicAckAsync(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                var properties = ea.BasicProperties;

                int maxRetries = int.Parse(Encoding.UTF8.GetString((byte[])properties.Headers["x-max-retries"]));
                int delay = int.Parse(Encoding.UTF8.GetString((byte[])properties.Headers["x-delay"]));
                int retryCount = int.Parse(Encoding.UTF8.GetString((byte[])properties.Headers["x-retry-count"]));

                retryCount++;

                if (retryCount < maxRetries)
                {
                    properties.Headers["x-retry-count"] = retryCount + 1;

                    BasicProperties newMessageProperties = new BasicProperties();
                    newMessageProperties.Headers = new Dictionary<string, object?>
                    {
                        { "x-retry-count", retryCount.ToString() },
                        { "x-max-retries", maxRetries.ToString() },
                        { "x-delay", delay.ToString() }
                    };
                    newMessageProperties.MessageId = properties.MessageId;

                    await Task.Delay(delay * retryCount);

                    await ch.BasicAckAsync(ea.DeliveryTag, false);
                    Console.WriteLine($"Message {ea.BasicProperties.MessageId} retried");

                    await ch.BasicPublishAsync(ea.Exchange, ea.RoutingKey, false, newMessageProperties, payloadBytes);
                }
                else
                {
                    Console.WriteLine($"Message {ea.BasicProperties.MessageId} reached max retries. Sending to dead letter queue.");
                    await ch.BasicNackAsync(ea.DeliveryTag, false, false); // Send to DLQ}
                }
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