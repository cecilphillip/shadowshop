using ShadowShop.AppHost.Resources;

var builder = DistributedApplication.CreateBuilder(args);

// Service Dependencies
var vault = builder.AddVaultDevServer("vault");

builder.AddExecutable("vault-setup-script", "bash", "./.config/vault", "setup.sh")
    .WithReference(vault);

var grafanaStack = builder.AddGrafanaStack("grafana", grafanaPort: 3000, otelPort: 4317);

var temporalDev = builder.AddTemporalDevServer(nameSpace: "ShadowShop");

var postgresPwd = builder.AddParameter("postgresPassword", true);
var catalogDb = builder.AddPostgres("catalog", port: 5432, password: postgresPwd)
    .WithDataVolume()
    .AddDatabase("catalogDb");

var rabbitPwd = builder.AddParameter("rabbitmqPassword", true);
var rmq = builder.AddRabbitMQ("rmq", password: rabbitPwd)
    .WithManagementPlugin(15672);

var redisPwd = builder.AddParameter("redisPassword", true);
var redisCache = builder.AddRedisStack("basketCache")
    .WithPassword(redisPwd)
    .WithConfiguration("./.config/redis/redis.conf");

// Application Projects
builder.AddProject<Projects.ShadowShop_CatalogInitializer>("catalogInitializer")
    .WithReference(vault)
    .WithReference(catalogDb);

builder.AddProject<Projects.ShadowShop_WorkflowProcessor>("workflowProcessor")
    .WithReference(grafanaStack)
    .WithReference(vault)
    .WithReference(temporalDev)
    .WithReference(rmq);

var catalogService = builder.AddProject<Projects.ShadowShop_CatalogService>("catalogService")
    .WithReference(grafanaStack)
    .WithReference(catalogDb);

var basketService = builder.AddProject<Projects.ShadowShop_BasketService>("basketService")
    .WithReference(grafanaStack)
    .WithReference(redisCache);

var frontend = builder.AddProject<Projects.ShadowShop_Frontend>("frontend")
    .WithReference(basketService)
    .WithReference(catalogService)
    .WithReference(rmq)
    .WithReference(grafanaStack)
    .WithReference(vault);

// Stripe Events Proxy
var stripeSecretKey = builder.AddParameter("stripeSecretKey", true);
builder.AddStripeDevProxy("stripe-events-proxy", stripeSecretKey, frontend, "/webhooks/stripe");

// Run host
var app = builder.Build();
app.Run();