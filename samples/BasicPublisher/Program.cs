using System;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using System.Configuration;

public class BasicPublisher
{
    public static async Task Main()
    {
        #region Read config values

        string host = ConfigurationManager.AppSettings["host"] ?? string.Empty;
        int configPort;
        int port = int.TryParse(ConfigurationManager.AppSettings["port"], out configPort) ? configPort : 5671;
        string userName = ConfigurationManager.AppSettings["userName"] ?? string.Empty;
        string password = ConfigurationManager.AppSettings["password"] ?? string.Empty;
        string virtualHost = ConfigurationManager.AppSettings["virtualHost"] ?? string.Empty;

        #endregion

        #region Initialize connection to RabbitMQ

        // Create connection factory
        ConnectionFactory factory = new ConnectionFactory
        {
            HostName = host,
            Port = port,
            UserName = userName,
            Password = password,
            VirtualHost = virtualHost,
            Ssl = new SslOption { Enabled = true, ServerName = host }
        };

        // Create a connection asynchronously - wait for completion of the Task to utilize the connection object
        using IConnection conn = await factory.CreateConnectionAsync();

        // Create a consumer dedicated channel for the rabbitMQ connection
        using IChannel ch = await conn.CreateChannelAsync();

        #endregion

        #region Define the RabbitMQ transport layer

        string exchangeName = "exchange.direct";
        string queueName = "sample_queue";

        // Declare Exchange
        // type - direct: we are publishing a message into a queue bound to the exchange
        // durable - true: the exchange will be recreated in case of RabbitMQ restart
        // autoDelete - false: the exchange will not be deleted once all message recipients are disconnected
        await ch.ExchangeDeclareAsync(exchange: exchangeName, type: ExchangeType.Direct, durable: true, autoDelete: false, arguments: null);

        // Declare a Queue - this is the representation of the transport layer that will store the published messages, 
        // for the consumers to read
        // exclusive - false: more than one consumer can retrieve messages from this queue
        // autoDelete - true: once all messages are consumed, the queue will be deleted
        await ch.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);

        // Declare a Binding - this is the link between an exchange and a queue, specifying what message will be delivered to which queue, 
        // There can be multiple bindings for exchanges/queues, think of it like a many-to-many relation
        // routingKey - a key that will send the message to the corresponding queue, based on the binding
        await ch.QueueBindAsync(queue: queueName, exchange: exchangeName, routingKey: "1", arguments: null);

        #endregion

        #region Prepare and publish a message

        // Create a sample text message and encode it into an array of Bytes
        byte[] body = Encoding.UTF8.GetBytes("Hello RabbitMQ!");

        // Execute an asynchronous operation of publishing messages into an exchange,
        // wait for the completion of the Task
        // We are waiting for finalization of the publish operation
        // This is not waiting for the consumers to pick up the message - this is done out of control of our application
        // Produce a message to the amq.direct exchange with a specified routingKey,
        // so the exchange can dispatch the message to a queue defined by binding
        await ch.BasicPublishAsync(exchange: exchangeName, routingKey: "1", body: body);

        #endregion

        #region Validate the publish

        // Pull message from the queue and check if we did get a message out
        var res = await ch.BasicGetAsync(queue: queueName, autoAck: true);
        Console.WriteLine(res != null ? "OK" : "FAILED");

        // Now let's publish some more messages
        for (int i = 0; i < 100; i++)
        {
            var message = $"Message {i}";
            await ch.BasicPublishAsync(exchange: exchangeName, routingKey: "1", body: Encoding.UTF8.GetBytes(message));
            await Task.Delay(500); // 500 millisecond delay between messages
        }

        #endregion
    }
}