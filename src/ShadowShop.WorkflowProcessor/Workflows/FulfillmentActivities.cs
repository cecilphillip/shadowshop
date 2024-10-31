using DotNext;
using Temporalio.Activities;

namespace ShadowShop.WorkflowProcessor.Workflows;

public record FulfillOrder(string SessionId);

public class FulfillmentActivities(ILogger<FulfillmentActivities> logger)
{
    [Activity]    
    public async Task<Result<bool>> SendOrderConfirmation(FulfillOrder confirmation)
    {
        logger.LogInformation("Sending order confirmation to demo@email.com for order {OrderId}", confirmation.SessionId);
        await Task.Delay(TimeSpan.FromSeconds(2), ActivityExecutionContext.Current.CancellationToken);
        
        logger.LogInformation("Order confirmation sent");
        return Result.FromValue(true);
    }
    
    [Activity]    
    public async Task<Result<bool>> UpdateInventory(FulfillOrder confirmation)
    {
        logger.LogInformation("Updating inventory for order {OrderId}", confirmation.SessionId);
        await Task.Delay(TimeSpan.FromSeconds(2), ActivityExecutionContext.Current.CancellationToken);
        
        logger.LogInformation("Inventory updated");
        return Result.FromValue(true);
    }
    
    [Activity]    
    public async Task<Result<bool>> ScheduleDelivery(FulfillOrder confirmation)
    {
        logger.LogInformation("Scheduling delivery for order {OrderId}", confirmation.SessionId);
        await Task.Delay(TimeSpan.FromSeconds(2),ActivityExecutionContext.Current.CancellationToken);
        
        logger.LogInformation("Delivery scheduled");
        return Result.FromValue(true);
    }
}