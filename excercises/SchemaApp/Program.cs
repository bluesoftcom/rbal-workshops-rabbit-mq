using System;
using System.Configuration;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using Newtonsoft.Json;
using Avro;
using Avro.Generic;
using Avro.IO;
using Models;
using NJsonSchema.Validation;

namespace SchemaApp;

public class Program
{
    public static async Task Main(string[] args)
    {
        #region Initialize RabbitMQ Connection
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
            Ssl = new SslOption { Enabled = true, ServerName = host }
        };

        IConnection conn = await factory.CreateConnectionAsync();
        IChannel ch = await conn.CreateChannelAsync();

        #endregion

        // Generate JSON Schema
        var generator = new JSchemaGenerator();
        var jsonSchema = generator.Generate(typeof(UserV1)).ToJson();
        // var generator = new JsonSchemaGenerator(new JsonSchemaGeneratorSettings());
        // var schema = generator.Generate(typeof(UserV1));
        // var jsonSchema = schema.ToJson();

        #region Define Messaging Layer

        // Define queue with schema metadata in arguments
        var queueName = "q.common";
        var routingKey = userName;

        // Declare queue with schema information in arguments as a new Dictionary of string and object
        var queueArguments = new Dictionary<string, object>
        {
            { "x-schema-type", "json" },
            { "x-schema-version", "V1" },
            { "x-schema-definition", jsonSchema }
        };
        //provide x-schema-type
        //provide x-schema-version
        //provide x-schema-definition with the json schema

        await ch.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: queueArguments
        );

        #endregion

        var consumer = new AsyncEventingBasicConsumer(ch);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            // validate the message, acknowledge if the schema is valid
        };

        await ch.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: consumer);

        Console.WriteLine("Basic consumer started. Press [enter] to exit.");
        Console.ReadLine();
    }

    private static bool ValidateSchema(string json, Type schemaClass)
    {
        // Generate JSON Schema using the JsonSchemaGenerator to generate schema out of the schemaClass
        // using schema.Validate, validate the payload
        // return the validation outcome
    }
    
    
}