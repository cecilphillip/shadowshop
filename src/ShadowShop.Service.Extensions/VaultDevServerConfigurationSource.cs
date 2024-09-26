using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nito.AsyncEx;

namespace ShadowShop.Service.Extensions;

public class VaultOptions
{
    public string VaultAddress { get; init; } = string.Empty;
    public string VaultToken { get; init; }= string.Empty;
    public string VaultMount { get; init; }= string.Empty;
    public bool AllowInsecure { get; init; }
    public bool PeriodicRefresh { get; init; } = false;
    public int RefreshInterval { get; init; } = 30;
}
public class VaultDevServerConfigurationSource(VaultOptions options, IServiceCollection services) : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        var provider = services.BuildServiceProvider();
        return new VaultDevServerConfigurationProvider(options, provider);
    }
}

public class VaultDevServerConfigurationProvider(VaultOptions options, IServiceProvider provider)
    : ConfigurationProvider
{
    private int _currentVersion = -1;

    public override void Load()
    {
        AsyncContext.Run(LoadAsync);
    }
    
    private async Task LoadAsync()
    {
        if(options.PeriodicRefresh)
        {
            var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.RefreshInterval));
            while (await timer.WaitForNextTickAsync(CancellationToken.None))
            {
                await LoadFromVaultAsync();
            }
        }
        else
        {
            await LoadFromVaultAsync();
        }
    }

    private async Task LoadFromVaultAsync()
    {
        using var httpClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient("vault");
        var response = await httpClient.GetAsync($"v1/{options.VaultMount}/data/stripe");
        
        if(response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            using var payload = JsonDocument.Parse(content);
            
            var version = payload.RootElement.GetProperty("data").GetProperty("metadata").GetProperty("version").GetInt32();
            
            if(version == _currentVersion)
                return;
            
            var dataValues = payload.RootElement.GetProperty("data").GetProperty("data").EnumerateObject();
            
            foreach(var property in dataValues)
            {
                var key = $"STRIPE_{property.Name.ToUpper()}";
                var value = property.Value.GetString();
                Data[key] = value;
            }
            _currentVersion = version;
            OnReload();
        }
    }
}

public static class ConfigurationManagerExtensions
{
    public static IConfigurationBuilder AddVaultDevServerConfiguration(this ConfigurationManager configBuilder, Func<VaultOptions> configureOptions, IServiceCollection services)
    {
        var options = configureOptions();
        
        if(options.VaultAddress == null)
            throw new ArgumentNullException(nameof(options.VaultAddress));
        
        services.AddHttpClient("vault", c =>
        {
            c.BaseAddress = new Uri(options.VaultAddress);
            c.DefaultRequestHeaders.Add("X-Vault-Token", options.VaultToken);
        });
        (configBuilder as IConfigurationBuilder).Add(new VaultDevServerConfigurationSource(options, services));

        return configBuilder;
    }
}