namespace ShadowShop.AppHost.Resources;

public class GrafanaStackResource(string name): ContainerResource(name), IResourceWithServiceDiscovery
{
    public const string GrafanaEndpointName = "grafana";
    public const int DefaultGrafanaEndpointPort = 3000;
    
    public const string OtelEndpointName = "otel";
    public const int DefaultOtelEndpointPort = 4317;
    
    public const string Platform = "linux/amd64";
}

public static class GrafanaStackBuilderExtensions
{
    public static IResourceBuilder<GrafanaStackResource> AddGrafanaStack(this IDistributedApplicationBuilder builder,
        string name, int? grafanaPort = null, int? otelPort = null)
    {
        var grafanaStack = new GrafanaStackResource(name);
        
        return builder.AddResource(grafanaStack)
            .WithImage("grafana/otel-lgtm")
            .WithImageTag("latest")
            .WithImageRegistry("docker.io")
            .WithHttpEndpoint(
                port: grafanaPort,
                targetPort: GrafanaStackResource.DefaultGrafanaEndpointPort,
                name: GrafanaStackResource.GrafanaEndpointName
            )
            .WithHttpEndpoint(
                port: otelPort,
                targetPort: GrafanaStackResource.DefaultOtelEndpointPort,
                name: GrafanaStackResource.OtelEndpointName
            )
            .WithContainerRuntimeArgs("--platform", GrafanaStackResource.Platform)
            .ExcludeFromManifest();
    }
    
    public static IResourceBuilder<TDestination> WithReference<TDestination>(
        this IResourceBuilder<TDestination> builder, IResourceBuilder<GrafanaStackResource> source)
        where TDestination : IResourceWithEnvironment
    {
        builder.WithReference(source as IResourceBuilder<IResourceWithServiceDiscovery>);
        
        return builder.WithEnvironment(ctx =>
        {
            ctx.EnvironmentVariables["OTEL_EXPORTER_OTLP_ENDPOINT"] = source.Resource.GetEndpoint(GrafanaStackResource.OtelEndpointName);
        });
    }
}