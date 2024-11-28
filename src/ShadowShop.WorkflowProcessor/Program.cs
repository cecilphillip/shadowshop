using ShadowShop.WorkflowProcessor;
using ShadowShop.Service.Extensions;
using ShadowShop.WorkflowProcessor.Workflows;
using Temporalio.Extensions.Hosting;
using VaultSharp.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults()
    .AddConfigurationDefaults();

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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapDefaultEndpoints();

app.Run();
