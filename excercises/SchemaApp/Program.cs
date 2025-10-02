using System;
using System.Configuration;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using Newtonsoft.Json;
using NJsonSchema;
using NJsonSchema.Generation;
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
        // get the system text json schema settings
        // get the json schema generator with settings as the parameter
        // use the gnerator to generate the schema out of the type of userV1 object
        // convert the schema to json

        #region Define Messaging Layer

        // Define queue with schema metadata in arguments
        var queueName = "";
        var routingKey = "";

        // Declare queue with schema information in arguments as a new Dictionary of string and object
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

        // Create an instance of the message to publish

        
    }

    private static bool ValidateSchema(string json, Type schemaClass)
    {
        // Generate JSON Schema using the JsonSchemaGenerator to generate schema out of the schemaClass
        // using schema.Validate, validate the payload
        // return the validation outcome
    }
}