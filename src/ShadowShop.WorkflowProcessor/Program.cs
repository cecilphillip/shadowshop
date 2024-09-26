using ShadowShop.WorkflowProcessor;
using ShadowShop.Service.Extensions;
using ShadowShop.WorkflowProcessor.Workflows;
using Temporalio.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Configuration.AddVaultDevServerConfiguration(() => new VaultOptions
{
    VaultAddress = builder.Configuration["VAULT_ADDR"] ?? string.Empty,
    VaultToken = builder.Configuration["VAULT_TOKEN"] ?? string.Empty,
    VaultMount = builder.Configuration["VAULT_APP_MOUNT"] ?? string.Empty,
    AllowInsecure = true
},  builder.Services);


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddStripe(builder.Configuration);
builder.AddRabbitMQClient("rmq", configureConnectionFactory: factory =>
{
    factory.DispatchConsumersAsync = true;
});

builder.Services.AddHostedService<CheckoutWorker>();

var temporalAddress =  builder.Configuration.GetValue<string>("TEMPORAL_ADDRESS")!;
var temporalUri = new Uri(temporalAddress);

builder.Services.AddTemporalClient(temporalUri.Authority, clientNamespace:"ShadowShop");
builder.Services.AddHostedTemporalWorker(temporalUri.Authority, clientNamespace:"ShadowShop", taskQueue:"checkout")
    .AddWorkflow<FulfillmentWorkflow>()
    .AddTransientActivities<FulfillmentActivities>();;

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapDefaultEndpoints();

app.Run();
