using InboxOutboxPattern;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace InboxPatternExcercise.Services
{
    internal class Producer
    {
        private static IChannel _channel = null;
        public Producer(IChannel channel)
        {
            _channel = channel;
        }
        public async void CreateMessageLayer(IChannel channel, string exchange, string queue)
        {    
            await channel.ExchangeDeclareAsync(
                exchange: exchange,
                type: ExchangeType.Fanout,
                durable: true,
                autoDelete: false
            );

            await channel.QueueDeclareAsync(
                queue: queue,
                durable: true,
                exclusive: false,
                autoDelete: false
            );

            await channel.QueueBindAsync(queue, exchange, string.Empty);
        }

        public async void PublishOrderMessage(string exchange, Order order)
        {
            string json = JsonConvert.SerializeObject(order);
            byte[] body = Encoding.UTF8.GetBytes(json);
            BasicProperties properties = new BasicProperties
            {
                CorrelationId = order.Id.ToString(),
                MessageId = Guid.NewGuid().ToString(),
            };

            await _channel.BasicPublishAsync(
                exchange: exchange,
                routingKey: string.Empty,
                mandatory: true,
                basicProperties: properties,
                body: body
            );
        }

        public async void PublishOrderMessageErrorous(string exchange, Order order)
        {
            string json = JsonConvert.SerializeObject(order);
            byte[] body = Encoding.UTF8.GetBytes(json);
            BasicProperties properties = new BasicProperties
            {
                CorrelationId = order.Id.ToString(),
                MessageId = Guid.NewGuid().ToString(),
            };

            await _channel.BasicPublishAsync(
                exchange: exchange,
                routingKey: string.Empty,
                mandatory: true,
                basicProperties: properties,
                body: body
            );

            await _channel.BasicPublishAsync(
                exchange: exchange,
                routingKey: string.Empty,
                mandatory: true,
                basicProperties: properties,
                body: body
            );
        }
    }
}
