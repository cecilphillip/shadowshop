using ShadowShop.Frontend.Services;
using Stripe;
using Stripe.Checkout;
using Stripe.Extensions.AspNetCore;

namespace ShadowShop.Frontend;

class CustomWebhookHandler(QueueClient queueClient, StripeWebhookContext context)
    : StripeWebhookHandler<CustomWebhookHandler>(context)
{
    public override Task OnCheckoutSessionCompletedAsync(Event evt)
    {
        if (evt.Data.Object is Session checkoutSession)
            queueClient.Publish(new FulfillOrder(checkoutSession.Id), "checkout-completed-events");
        
        return Task.CompletedTask;
    }
}
record FulfillOrder(string SessionId);