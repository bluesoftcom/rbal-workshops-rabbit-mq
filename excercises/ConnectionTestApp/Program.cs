using System;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using System.Configuration;

public class ConnectionTest
{
    public static async Task Main()
    {
        Console.WriteLine("RabbitMQ Connection Test");
        Console.WriteLine("========================");

        try
        {
            // Read configuration
            string host = ConfigurationManager.AppSettings["host"] ?? string.Empty;
            int configPort;
            int port = int.TryParse(ConfigurationManager.AppSettings["port"], out configPort) ? configPort : 5671;
            string userName = ConfigurationManager.AppSettings["userName"] ?? string.Empty;
            string password = ConfigurationManager.AppSettings["password"] ?? string.Empty;
            string virtualHost = ConfigurationManager.AppSettings["virtualHost"] ?? string.Empty;

            // Display configuration (mask password for security)
            Console.WriteLine($"Host: {host}");
            Console.WriteLine($"Port: {port}");
            Console.WriteLine($"UserName: {userName}");
            Console.WriteLine($"Password: {"*".PadLeft(password.Length, '*')}");
            Console.WriteLine($"VirtualHost: {virtualHost}");
            Console.WriteLine();

            // Create connection factory with SSL
            var factory = new ConnectionFactory()
            {
                HostName = host,
                Port = port,
                UserName = userName,
                Password = password,
                VirtualHost = virtualHost,
                Ssl = new SslOption()
                {
                    Enabled = true,
                    ServerName = host
                }
            };

            Console.WriteLine("Attempting to connect to RabbitMQ...");

            // Test connection
            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            Console.WriteLine("✅ SUCCESS: Connected to RabbitMQ!");
            Console.WriteLine($"Connection ID: {connection.ClientProvidedName}");
            Console.WriteLine($"Server Properties: {connection.ServerProperties.Count} properties");
            Console.WriteLine("\n🎉 RabbitMQ connection test completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ FAILED: Connection test failed!");
            Console.WriteLine($"Error: {ex.Message}");
            
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            
            Console.WriteLine("\n💡 Troubleshooting tips:");
            Console.WriteLine("- Check if RabbitMQ server is running");
            Console.WriteLine("- Verify credentials in App.config");
            Console.WriteLine("- Ensure firewall allows connection");
            Console.WriteLine("- Check SSL configuration if using port 5671");
        }

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}
