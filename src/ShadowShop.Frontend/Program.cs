using Microsoft.Extensions.Diagnostics.HealthChecks;
using ShadowShop.Frontend;
using ShadowShop.Frontend.Components;
using ShadowShop.Frontend.Services;
using ShadowShop.GrpcBasket;
using ShadowShop.Service.Extensions;
using VaultSharp.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults()
    .AddConfigurationDefaults();

builder.AddRabbitMQClient("rmq");

builder.Services.AddStripe(builder.Configuration);
builder.Services.AddHttpForwarderWithServiceDiscovery();

builder.Services.AddHttpServiceReference<CatalogServiceClient>("https+http://catalogService", healthRelativePath: "health");

var isHttps = builder.Configuration["DOTNET_LAUNCH_PROFILE"] == "https";

builder.Services.AddTransient<QueueClient>();
builder.Services.AddSingleton<BasketServiceClient>()
    .AddGrpcServiceReference<Basket.BasketClient>($"{(isHttps ? "https" : "http")}://basketService", failureStatus: HealthStatus.Degraded);

builder.Services.AddRazorComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.UseStaticFiles();

app.MapRazorComponents<App>();

app.MapForwarder("/catalog/images/{id}", "https+http://catalogService", "/api/v1/catalog/items/{id}/image");

app.MapWebhooks();

app.MapDefaultEndpoints();

app.Run();
