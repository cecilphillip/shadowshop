using Microsoft.EntityFrameworkCore;
using ShadowShop.CatalogDb;
using ShadowShop.CatalogInitializer;
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

builder.AddNpgsqlDbContext<CatalogDbContext>("catalogDb", null,
    optionsBuilder => optionsBuilder.UseNpgsql(npgsqlBuilder =>
        npgsqlBuilder.MigrationsAssembly(typeof(Program).Assembly.GetName().Name)));

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(Initializer.ActivitySourceName));

builder.Services.AddStripe(builder.Configuration);
builder.Services.AddSingleton<Initializer>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<Initializer>());
builder.Services.AddHealthChecks()
    .AddCheck<CatalogDbInitializerHealthCheck>("DbInitializer", null);

var app = builder.Build();

app.MapDefaultEndpoints();

await app.RunAsync();