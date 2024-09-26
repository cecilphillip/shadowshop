echo "Forwarding stripe webhook events"
echo $WEBHOOK_ENDPOINT

stripe listen --forward-to $WEBHOOK_ENDPOINT --api-key $STRIPE_SECRET_KEY
