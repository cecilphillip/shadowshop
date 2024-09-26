namespace ShadowShop.AppHost.Resources;

public class RedisStackResource(string name): ContainerResource(name), IResourceWithServiceDiscovery, IResourceWithConnectionString
{
    internal const string ServerEndpointName = "server";
    internal const int DefaultServerEndpointPort = 6379;
    private EndpointReference? _serverEndpoint;
    
    internal const string InsightEndpointName = "insight";
    internal const int DefaultInsightEndpointPort = 8001;
    
    internal const string RedisArgsEnvVarName = "REDIS_ARGS";
    public ParameterResource? PasswordParameter { get; internal set; }
    
    public EndpointReference ServerEndpoint => _serverEndpoint ??= new(this, ServerEndpointName);

    public ReferenceExpression ConnectionStringExpression =>
        PasswordParameter is not null
            ? ReferenceExpression.Create(
                $"{ServerEndpoint.Property(EndpointProperty.Host)}:{ServerEndpoint.Property(EndpointProperty.Port)},password={PasswordParameter}")
            : ReferenceExpression.Create(
                $"{ServerEndpoint.Property(EndpointProperty.Host)}:{ServerEndpoint.Property(EndpointProperty.Port)}");
}

public static class RedisStackBuilderExtensions
{
    public static IResourceBuilder<RedisStackResource> AddRedisStack(
        this IDistributedApplicationBuilder builder, string name, 
        int? serverPort = null, int? insightPort = null,  IResourceBuilder<ParameterResource>? password = null)
    {
        
        var redisStack = new RedisStackResource(name);
        var passwordParameter = password?.Resource;
        redisStack.PasswordParameter = passwordParameter;        

        return builder.AddResource(redisStack)
            .WithImage("redis/redis-stack")
            .WithImageRegistry("docker.io")
            .WithImageTag("latest")
            .WithEndpoint(
                port: serverPort,
                targetPort: RedisStackResource.DefaultServerEndpointPort,
                name: RedisStackResource.ServerEndpointName
            )
            .WithHttpEndpoint(
                port: insightPort,
                targetPort: RedisStackResource.DefaultInsightEndpointPort,
                name: RedisStackResource.InsightEndpointName
            )
            .ExcludeFromManifest();
    }
    
    public static IResourceBuilder<RedisStackResource> WithPassword(
        this IResourceBuilder<RedisStackResource> builder, IResourceBuilder<ParameterResource> password)
    {
        builder.Resource.PasswordParameter = password.Resource;
        return builder.WithEnvironment(ctx =>
        {
            ctx.EnvironmentVariables[RedisStackResource.RedisArgsEnvVarName] = ReferenceExpression.Create($"--requirepass {password.Resource}");
            ctx.EnvironmentVariables["REDISCLI_AUTH"] = password.Resource;
        });
    }

    public static IResourceBuilder<RedisStackResource> WithConfiguration(
        this IResourceBuilder<RedisStackResource> builder, string source)
    {
        return builder.WithBindMount(source, "/redis-stack.conf");
    }
    
    public static IResourceBuilder<TDestination> WithReference<TDestination>(
        this IResourceBuilder<TDestination> builder, IResourceBuilder<RedisStackResource> source)
        where TDestination : IResourceWithEnvironment
    {
        builder
            .WithReference(source as IResourceBuilder<IResourceWithServiceDiscovery>)
            .WithReference(source as IResourceBuilder<IResourceWithConnectionString>);
        
        return builder.WithEnvironment(ctx =>
        {
            if (source.Resource.PasswordParameter is not null)
            {
                ctx.EnvironmentVariables["REDIS_PASSWORD"] = source.Resource.PasswordParameter;
            }
            
            ctx.EnvironmentVariables["REDIS_ADDRESS"] =
                source.GetEndpoint(RedisStackResource.ServerEndpointName);
            
            ctx.EnvironmentVariables["REDIS_INSIGHT_ADDRESS"] =
                source.GetEndpoint(RedisStackResource.InsightEndpointName);
        });
    }
}