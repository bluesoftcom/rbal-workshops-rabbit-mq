using System.Configuration;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

public class BasicConsumer
{
    public static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Please provide the queue name");
            return;
        }
        string queueName = args[0];

        #region Configuration and init
        // Read configuration
        string host = ConfigurationManager.AppSettings["host"] ?? string.Empty;
        int configPort;
        int port = int.TryParse(ConfigurationManager.AppSettings["port"], out configPort) ? configPort : 5671;
        string userName = ConfigurationManager.AppSettings["userName"] ?? string.Empty;
        string password = ConfigurationManager.AppSettings["password"] ?? string.Empty;
        string virtualHost = ConfigurationManager.AppSettings["virtualHost"] ?? string.Empty;
        
        // Create a connection factory
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

        // define the queue you want to read from, provide arguments: exchange, durable, exclusive, autoDelete
        // await ch.QueueDeclareAsync(
        //     queue: "q.example",
        //     durable: true,
        //     exclusive: false,
        //     autoDelete: false,
        //     arguments: null
        // );

        // define a consumer with AsyncEventingBasicConsumer
        var consumer = new AsyncEventingBasicConsumer(ch);

        // define RecievedAsync event that will: read the message body, get the message string using UTF8 encoding,
        // output to the console the recieved message, acknowledge the message with BasicAckAsync
        consumer.ReceivedAsync += async (model, ea) =>
        {
            byte[] body = ea.Body.ToArray();
            string message = Encoding.UTF8.GetString(body);
            Console.WriteLine(" [x] Received {0}", message);
            await ch.BasicAckAsync(ea.DeliveryTag, false);
        };
        #endregion
        // Run the BasicConsumeAsync to start consuming messages from the queue
        await ch.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumer: consumer
        );

        Console.WriteLine("Basic consumer for queue:" + queueName + " started. Press [enter] to exit.");
        Console.ReadLine();
    }
}
