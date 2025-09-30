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
            VirtualHost = virtualHost
        };

        // Create a connection asynchronously - wait for completion of the Task to utilize the connection object
        using IConnection conn = await factory.CreateConnectionAsync();

        // Create a consumer dedicated channel for the rabbitMQ connection
        using IChannel ch = await conn.CreateChannelAsync();

        #endregion

        #region Define the RabbitMQ transport layer

        // Declare Exchange
        // type - direct: we are publishing a message into a queue bound to the exchange
        // durable - true: the exchange will be recreated in case of RabbitMQ restart
        // autoDelete - false: the exchange will not be deleted once all message recipients are disconnected
        await ch.ExchangeDeclareAsync(exchange: "amq.direct", type: ExchangeType.Direct, durable: true, autoDelete: false, arguments: null);

        // Declare a Queue - this is the representation of the transport layer that will store the published messages, 
        // for the consumers to read
        // exclusive - false: more than one consumer can retrieve messages from this queue
        // autoDelete - true: once all messages are consumed, the queue will be deleted
        await ch.QueueDeclareAsync(queue: "q1", durable: false, exclusive: false, autoDelete: true, arguments: null);

        // Declare a Binding - this is the link between an exchange and a queue, specifying what message will be delivered to which queue, 
        // There can be multiple bindings for exchanges/queues, think of it like a many-to-many relation
        // routingKey - a key that will send the message to the corresponding queue, based on the binding
        await ch.QueueBindAsync(queue: "q1", exchange: "amq.direct", routingKey: "1", arguments: null);

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
        await ch.BasicPublishAsync(exchange: "amq.direct", routingKey: "1", body: body);

        #endregion

        #region Validate the publish

        // Pull message from the queue and check if we did get a message out
        var res = await ch.BasicGetAsync(queue: "q1", autoAck: true);
        Console.WriteLine(res != null ? "OK" : "FAILED");
        
        #endregion
    }
}
