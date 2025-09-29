using System.Text;
using RabbitMQ.Client;
using System.Configuration;
using System.Security.Cryptography.X509Certificates;

public class AccessControl
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
            Ssl = new SslOption
            {
                Enabled = true,
                Version = System.Security.Authentication.SslProtocols.Tls12,
                AcceptablePolicyErrors = System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch | System.Net.Security.SslPolicyErrors.None,
                ServerName = host,
                CertificateSelectionCallback = (s, t, l, r) => LoadClientCertIfPresent()
            }
        };

        IConnection conn = await factory.CreateConnectionAsync();
        IChannel ch = await conn.CreateChannelAsync();

        await ch.ExchangeDeclareAsync(exchange: "ex.access.headers", type: ExchangeType.Headers, durable: true, autoDelete: false, arguments: null);
        await ch.QueueDeclareAsync("q.access", durable: true, exclusive: false, autoDelete: false, arguments: null);

        var accessArgs = new Dictionary<string, object>
        {
            { "x-match", "all" },
            { "role", "admin" },
            { "department", "finance" }
        };

        await ch.QueueBindAsync("q.access", "ex.access.headers", routingKey: string.Empty, arguments: accessArgs);

        var message = new { UserId = "U-001", Action = "ApproveInvoice", Amount = 500.00 };
        var body = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(message));

        var properties = new BasicProperties
        {
            Persistent = true,
            Headers = new Dictionary<string, object?>
            {
                { "role", "admin" },
                { "department", "finance" }
            }
        };

        await ch.BasicPublishAsync(exchange: "ex.access.headers", routingKey: string.Empty, basicProperties: properties, body: body, mandatory: true);

        Console.WriteLine("Access control message published.");
    }
    static X509Certificate2? LoadClientCertIfPresent()
    {
        string pfxPath = Environment.GetEnvironmentVariable("AMQP_CLIENT_PFX");
        string pfxPass = Environment.GetEnvironmentVariable("AMQP_CLIENT_PFX_PASS");
        if (string.IsNullOrEmpty(pfxPath)) return null;
        return new X509Certificate2(pfxPath, pfxPass, X509KeyStorageFlags.MachineKeySet);
    }
}
