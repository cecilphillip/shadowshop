using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ShadowShop.WorkflowProcessor.Workflows;
using Stripe;
using Temporalio.Client;

namespace ShadowShop.WorkflowProcessor;

public class CheckoutWorker(IConnection rmqConnection, ITemporalClient temporalClient) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var channel = rmqConnection.CreateModel();
        
        EnsureQueue(channel, Events.CheckoutSessionCompleted);
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.Received += OnTemporalReceived;
        
        channel.BasicConsume(queue: Events.CheckoutSessionCompleted, autoAck: false, consumer: consumer);
        
        return Task.CompletedTask;
    }
    
    private async Task OnTemporalReceived(object sender, BasicDeliverEventArgs eventArgs)
    {
        var orderInfo = JsonSerializer.Deserialize<FulfillOrder>(eventArgs.Body.Span)!;
        await temporalClient.StartWorkflowAsync<FulfillmentWorkflow>(w => w.RunAsync(orderInfo), new ()
        {
            Id = $"shadowshop-fulfillment-{Guid.NewGuid():N}",
            TaskQueue = "checkout"
        });
    } 

    private void EnsureQueue(IModel channel, string queueName)
    {
        channel.QueueDeclare(queueName, false, false, false);
        channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: true);
    }
}