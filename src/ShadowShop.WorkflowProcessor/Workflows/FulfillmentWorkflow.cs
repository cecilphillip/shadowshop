using DotNext;
using Temporalio.Workflows;
using Temporalio.Common;

namespace ShadowShop.WorkflowProcessor.Workflows;

[Workflow]
public class FulfillmentWorkflow
{
    readonly RetryPolicy _retryPolicy = new RetryPolicy
    {
        InitialInterval = TimeSpan.FromSeconds(1),
        MaximumInterval = TimeSpan.FromSeconds(10),
        BackoffCoefficient = 2
    };

    private string SimpleStatus { get; set; } = "Workflow not started";

    [WorkflowRun]
    public async Task RunAsync(FulfillOrder orderSession)
    { 
        SimpleStatus = "Starting workflow";
        var activityResult = await Workflow.ExecuteActivityAsync<FulfillmentActivities, Result<bool>>(
            x => x.SendOrderConfirmation(orderSession),
            new() { RetryPolicy = _retryPolicy, StartToCloseTimeout = TimeSpan.FromSeconds(10) });

        if (activityResult)
        {
            SimpleStatus = "Order confirmation sent";
            activityResult = await  Workflow.ExecuteActivityAsync<FulfillmentActivities, Result<bool>>(
                x => x.UpdateInventory(orderSession),
                new() { RetryPolicy = _retryPolicy, StartToCloseTimeout = TimeSpan.FromSeconds(10)});
        }

        if (activityResult)
        {
            SimpleStatus = "Inventory updated";
            activityResult = await Workflow.ExecuteActivityAsync<FulfillmentActivities, Result<bool>>(
                x => x.ScheduleDelivery(orderSession),
                new() { RetryPolicy = _retryPolicy, StartToCloseTimeout = TimeSpan.FromSeconds(10)});
        }

        if (activityResult)
        {
            SimpleStatus = "Delivery scheduled";
        }
    }
    
    [WorkflowQuery]
    public string CurrentStatus() => SimpleStatus;
}