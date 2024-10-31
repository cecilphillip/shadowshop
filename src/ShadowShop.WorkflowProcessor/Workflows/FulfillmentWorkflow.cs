using DotNext;
using Temporalio.Workflows;
using Temporalio.Common;

namespace ShadowShop.WorkflowProcessor.Workflows;

[Workflow]
public class FulfillmentWorkflow
{
    [WorkflowQuery]
    public string SimpleStatus { get; set; } = "Workflow not started";

    [WorkflowRun]
    public async Task RunAsync(FulfillOrder orderSession)
    { 
        SimpleStatus = "Starting workflow";
        var activityResult = await Workflow.ExecuteActivityAsync<FulfillmentActivities, Result<bool>>(
            x => x.SendOrderConfirmation(orderSession),
            new() { StartToCloseTimeout = TimeSpan.FromSeconds(10) });

        if (activityResult)
        {
            SimpleStatus = "Order confirmation sent";
            activityResult = await  Workflow.ExecuteActivityAsync<FulfillmentActivities, Result<bool>>(
                x => x.UpdateInventory(orderSession),
                new() { StartToCloseTimeout = TimeSpan.FromSeconds(10)});
        }

        if (activityResult)
        {
            SimpleStatus = "Inventory updated";
            activityResult = await Workflow.ExecuteActivityAsync<FulfillmentActivities, Result<bool>>(
                x => x.ScheduleDelivery(orderSession),
                new() { StartToCloseTimeout = TimeSpan.FromSeconds(10)});
        }

        if (activityResult)
        {
            SimpleStatus = "Delivery scheduled";
        }
    }
}