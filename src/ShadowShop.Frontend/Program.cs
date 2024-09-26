using Microsoft.Extensions.Diagnostics.HealthChecks;
using ShadowShop.Frontend;
using ShadowShop.Frontend.Components;
using ShadowShop.Frontend.Services;
using ShadowShop.GrpcBasket;
using ShadowShop.Service.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Configuration.AddVaultDevServerConfiguration(() => new VaultOptions
{
    VaultAddress = builder.Configuration["VAULT_ADDR"] ?? string.Empty,
    VaultToken = builder.Configuration["VAULT_TOKEN"] ?? string.Empty,
    VaultMount = builder.Configuration["VAULT_APP_MOUNT"] ?? string.Empty,
    AllowInsecure = true
},  builder.Services);

builder.AddRabbitMQClient("rmq");

builder.Services.AddStripe(builder.Configuration);
builder.Services.AddHttpForwarderWithServiceDiscovery();

builder.Services.AddHttpServiceReference<CatalogServiceClient>("https+http://catalogservice", healthRelativePath: "health");

var isHttps = builder.Configuration["DOTNET_LAUNCH_PROFILE"] == "https";

builder.Services.AddTransient<QueueClient>();
builder.Services.AddSingleton<BasketServiceClient>()
    .AddGrpcServiceReference<Basket.BasketClient>($"{(isHttps ? "https" : "http")}://basketservice", failureStatus: HealthStatus.Degraded);

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

app.MapForwarder("/catalog/images/{id}", "https+http://catalogservice", "/api/v1/catalog/items/{id}/image");

app.MapWebhooks();

app.MapDefaultEndpoints();

app.Run();
