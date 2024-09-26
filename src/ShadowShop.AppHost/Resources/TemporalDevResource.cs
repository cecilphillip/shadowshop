namespace ShadowShop.AppHost.Resources;

public class TemporalDevResource(string name) : ContainerResource(name), IResourceWithServiceDiscovery
{
    public const string ServiceEndpointName = "grpc";
    public const int DefaultServiceEndpointPort = 7233;

    public const string UIEndpointName = "ui";
    public const int DefaultUIEndpointPort = 8233;
}

public static class TemporalDevBuilderExtensions
{
    public static IResourceBuilder<TemporalDevResource> AddTemporalDevServer(
        this IDistributedApplicationBuilder builder, string name = "temporal-dev", string nameSpace = "",
        int? servicePort = null, int targetServicePort = TemporalDevResource.DefaultServiceEndpointPort,
        int? uiPort = null, int targetUiPort = TemporalDevResource.DefaultUIEndpointPort)
    {
        return builder.AddResource(new TemporalDevResource(name))
            .WithImage("")
            .WithDockerfile("./.config/temporal")
            .WithBuildArg("SERVICE_PORT", targetServicePort)
            .WithBuildArg("NAMESPACE", nameSpace)
            .WithHttpEndpoint(
                port: servicePort,
                targetPort: targetServicePort,
                name: TemporalDevResource.ServiceEndpointName
            )
            .WithHttpEndpoint(
                port: uiPort,
                targetPort: targetUiPort,
                name: TemporalDevResource.UIEndpointName
            )
            .ExcludeFromManifest();
    }

    public static IResourceBuilder<TDestination> WithReference<TDestination>(
        this IResourceBuilder<TDestination> builder, IResourceBuilder<TemporalDevResource> source)
        where TDestination : IResourceWithEnvironment
    {
        return builder
            .WithReference(source as IResourceBuilder<IResourceWithServiceDiscovery>)
            .WithEnvironment(ctx =>
            {
                ctx.EnvironmentVariables["TEMPORAL_ADDRESS"] =
                    source.GetEndpoint(TemporalDevResource.ServiceEndpointName);

                ctx.EnvironmentVariables["TEMPORAL_UI_ADDRESS"] =
                    source.GetEndpoint(TemporalDevResource.UIEndpointName);
            });
    }
}