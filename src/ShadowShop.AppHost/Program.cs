using ShadowShop.AppHost.Resources;

var builder = DistributedApplication.CreateBuilder(args);

// Service Dependencies
var vault = builder.AddVaultDevServer("vault");

builder.AddExecutable("vault-setup-script", "bash", "./.config/vault", "setup.sh")
    .WithReference(vault);

var grafanaStack = builder.AddGrafanaStack("grafana", grafanaPort: 3000, otelPort: 4317);

var temporalDev = builder.AddTemporalDevServer(nameSpace:"ShadowShop");

var postgresPwd = builder.AddParameter("postgresspwd", true);
var catalogDb = builder.AddPostgres("catalog", port: 5432, password: postgresPwd)
    .WithDataVolume()
    .AddDatabase("catalogdb");

var rabbitPwd = builder.AddParameter("rabbitmqpassword", true);
var rmq = builder.AddRabbitMQ("rmq", password: rabbitPwd)
    .WithManagementPlugin(15672);

var redisPwd = builder.AddParameter("redispwd", true);;
var redisCache = builder.AddRedisStack("basketcache", password:redisPwd)
    .WithConfiguration("./.config/redis/redis.conf");

// Application Projects
builder.AddProject<Projects.ShadowShop_CatalogInitializer>("catalogInitializer")
    .WithReference(vault)
    .WithReference(catalogDb);

builder.AddProject<Projects.ShadowShop_WorkflowProcessor>("workflowprocessor")
    .WithReference(vault)
    .WithReference(temporalDev)
    .WithReference(rmq);

var catalogService = builder.AddProject<Projects.ShadowShop_CatalogService>("catalogservice")
    .WithReference(grafanaStack)
    .WithReference(catalogDb);

var basketService = builder.AddProject<Projects.ShadowShop_BasketService>("basketservice")
    .WithReference(grafanaStack)
    .WithReference(redisCache);

var frontend = builder.AddProject<Projects.ShadowShop_Frontend>("frontend")
    .WithReference(grafanaStack)
    .WithReference(basketService)
    .WithReference(catalogService)
    .WithReference(rmq)
    .WithReference(vault);

var stripeSecretKey = builder.AddParameter("stripesecretkey", true);
builder.AddStripeDevProxy("stripe-events-proxy", stripeSecretKey, frontend, "/webhooks/stripe");


builder.Build().Run();