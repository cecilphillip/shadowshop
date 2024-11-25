using ShadowShop.Frontend.Services;
using Stripe;
using Stripe.Checkout;

namespace ShadowShop.Frontend;

public static class WebHookEndpoints
{
    public static RouteGroupBuilder MapWebhooks(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/webhooks");

        group.MapPost("stripe", async (HttpRequest request, IConfiguration config, QueueClient queueClient, ILoggerFactory loggerFactory) =>
            {
                var logger =  loggerFactory.CreateLogger("StripeWebhooks");
                try
                {
                    var payload = await new StreamReader(request.Body).ReadToEndAsync();
                    var webhookSecret = config.GetValue<string>("STRIPE_WEBHOOK_SECRET");
                    var stripeEvent = EventUtility.ConstructEvent(
                        payload, request.Headers["Stripe-Signature"], webhookSecret);

                    // Do something fun
                    switch (stripeEvent.Type)
                    {
                        case EventTypes.CheckoutSessionCompleted when stripeEvent.Data.Object is Session
                        {
                            PaymentStatus: "paid"
                        } session:

                            queueClient.Publish(new FulfillOrder(session.Id), "checkout-completed-events");
                            break;
                    }

                    return Results.Ok();
                }
                catch (StripeException e)
                {
                    logger.LogError(e, "Stripe Error");
                    return Results.BadRequest(e.Message);
                }
            }
        );

        return group;
    }

    record FulfillOrder(string SessionId);
}