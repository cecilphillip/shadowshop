# Shadow Shop Sample 
A modified version of the [Aspire Shop](https://github.com/dotnet/aspire-samples/tree/main/samples/AspireShop) sample
application that adds integration with [Stripe](https://stripe.com) for payment processing, [temporal](https://temporal.io/)
for durable workflows, other customer Aspire Integrations. 

![Architecture Diagram](./docs/images/diagram_architecture.png)

## Aspire Integrations
- [HashiCorp Vault](./src/ShadowShop.AppHost/Resources/VaultResource.cs) - Secret store
- [Temporal](./src/ShadowShop.AppHost/Resources/TemporalDevResource.cs) - Workflow Engine
- [Stripe](./src/ShadowShop.AppHost/Resources/StripeDevResource.cs) - Payment event proxy
- [Grafana](./src/ShadowShop.AppHost/Resources/GrafanaStackResource.cs) - OpenTelemetry server and UI

## Getting Setup

### Prerequisites
- [.NET SDK](https://get.dot.net/) 8.0 or later
- [.NET Aspire workload](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/setup-tooling?tabs=linux&pivots=dotnet-cli)
- [Stripe account](https://dashboard.stripe.com) & [Stripe CLI](https://stripe.com/docs/stripe-cli)
- [Docker](https://www.docker.com)

### Run the solution

* Add your [Stripe API keys](https://dashboard.stripe.com/apikeys) to the [setup.sh](./src/ShadowShop.AppHost/.config/vault/setup.sh) file.
* Run the [App Host](./src/ShadowShop.AppHost) project