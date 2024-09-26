using System.Net.Mime;
using RabbitMQ.Client;
using System.Text.Json;

namespace ShadowShop.Frontend.Services;

public class QueueClient(IConnection connection)
{
    public void Publish<T>(T message, string queueName)
    {
        var channel = connection.CreateModel();
        
        EnsureQueue(channel, queueName);
        
        var props = channel.CreateBasicProperties();
        props.CorrelationId = Guid.NewGuid().ToString("N");
        props.ContentType = MediaTypeNames.Application.Json;
        
        var json = JsonSerializer.SerializeToUtf8Bytes(message);
        
        channel.BasicPublish("", queueName, props, json);
    }

    private void EnsureQueue(IModel channel, string queueName)
    {
       channel.QueueDeclare(queueName, false, false, false);
       channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: true);
    }
}