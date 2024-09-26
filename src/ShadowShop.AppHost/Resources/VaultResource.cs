namespace ShadowShop.AppHost.Resources;

public class VaultServerResource(string name) : ContainerResource(name), IResourceWithServiceDiscovery
{
    internal const string PrimaryEndpointName = "http";
    internal const int DefaultContainerPort = 8200;
    internal const string DefaultTokenId = "dev-root";
    
    private EndpointReference? _primaryEndpoint;
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new(this, PrimaryEndpointName);
}

public static class VaultServerBuilderExtensions
{
    public static IResourceBuilder<VaultServerResource> AddVaultDevServer(this IDistributedApplicationBuilder builder,
        string name, int? port = null, string? rootTokenId = VaultServerResource.DefaultTokenId)
    {
        var address = $"0.0.0.0:{VaultServerResource.DefaultContainerPort}";
        var apiAddress = $"http://{address}";
        var args = new List<string> { "server", "-dev", "-dev-no-store-token" };

        return builder.AddResource(new VaultServerResource(name))
            .WithImage("hashicorp/vault")
            .WithImageRegistry("docker.io")
            .WithHttpEndpoint(
                port: port,
                targetPort: VaultServerResource.DefaultContainerPort,
                name: VaultServerResource.PrimaryEndpointName
            )
            .WithArgs(args.ToArray())
            .WithEnvironment("VAULT_LOG_LEVEL", "info")
            .WithEnvironment("VAULT_DEV_ROOT_TOKEN_ID", rootTokenId ?? VaultServerResource.DefaultTokenId)
            .WithEnvironment("VAULT_API_ADDR", apiAddress)
            .WithEnvironment("VAULT_ADDR", apiAddress)
            .WithEnvironment("VAULT_DEV_LISTEN_ADDRESS", address)
            .ExcludeFromManifest();
    }

    public static IResourceBuilder<TDestination> WithReference<TDestination>(
        this IResourceBuilder<TDestination> builder, IResourceBuilder<VaultServerResource> source,
        string? rootTokenId = null)
        where TDestination : IResourceWithEnvironment
    {
        builder.WithReference(source as IResourceBuilder<IResourceWithServiceDiscovery>);
        
        return builder.WithEnvironment(ctx =>
        {
            ctx.EnvironmentVariables["VAULT_ADDR"] = source.Resource.GetEndpoint(VaultServerResource.PrimaryEndpointName);
            ctx.EnvironmentVariables["VAULT_PORT"] =
                source.Resource.GetEndpoint(VaultServerResource.PrimaryEndpointName).Property(EndpointProperty.Port);
            ctx.EnvironmentVariables["VAULT_APP_MOUNT"] ="shadowshop";
            ctx.EnvironmentVariables["VAULT_TOKEN"] = rootTokenId ?? VaultServerResource.DefaultTokenId;
        });
    }
}
