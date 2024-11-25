using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aspire.Hosting.Redis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;

namespace ShadowShop.AppHost.Resources;

public class RedisStackResource(string name): ContainerResource(name), IResourceWithServiceDiscovery, IResourceWithConnectionString
{
    internal const string ServerEndpointName = "server";
    internal const int DefaultServerEndpointPort = 6379;
    private EndpointReference? _serverEndpoint;
    
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
        this IDistributedApplicationBuilder builder, string name, int? serverPort = null)
    {
        var redisStack = new RedisStackResource(name);

        string? connectionString = null;

        builder.Eventing.Subscribe<ConnectionStringAvailableEvent>(redisStack, async (_, ct) =>
        {
            connectionString = await redisStack.ConnectionStringExpression.GetValueAsync(ct).ConfigureAwait(false);

            if (connectionString == null)
            {
                throw new DistributedApplicationException($"ConnectionStringAvailableEvent was published for the '{redisStack.Name}' resource but the connection string was null.");
            }
        });
        
        var healthCheckKey = $"{name}_check";
        builder.Services.AddHealthChecks().AddRedis(_ => connectionString ?? throw new InvalidOperationException("Connection string is unavailable"), name: healthCheckKey);
        
        return builder.AddResource(redisStack)
            .WithImage("redis/redis-stack-server")
            .WithImageRegistry("docker.io")
            .WithImageTag("latest")
            .WithEndpoint(
                port: serverPort,
                targetPort: RedisStackResource.DefaultServerEndpointPort,
                name: RedisStackResource.ServerEndpointName
            )
            .WithHealthCheck(healthCheckKey)
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
        });
    }

    public static IResourceBuilder<RedisStackResource> WithRedisInsight(this IResourceBuilder<RedisStackResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (builder.ApplicationBuilder.Resources.OfType<RedisInsightResource>().SingleOrDefault() is not null)
        {
            return builder;
        }

        var containerName = $"{builder.Resource.Name}-insight";

        var resource = new RedisInsightResource(containerName);
        builder.ApplicationBuilder.AddResource(resource)
            .WithImage("redis/redisinsight")
            .WithImageRegistry("docker.io")
            .WithHttpEndpoint(port:5540, targetPort: 5540, name: "http")
            .ExcludeFromManifest();

        // Wait for all endpoints to be allocated before attempting to import databases
        var endpointsAllocatedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        builder.ApplicationBuilder.Eventing.Subscribe<AfterEndpointsAllocatedEvent>((_, _) =>
        {
            endpointsAllocatedTcs.TrySetResult();
            return Task.CompletedTask;
        });

        builder.ApplicationBuilder.Eventing.Subscribe<ResourceReadyEvent>(resource, async (e, ct) =>
        {
            var redisInstances = builder.ApplicationBuilder.Resources.OfType<RedisStackResource>();

            var redisStackResources = redisInstances.ToArray();
            if (redisStackResources.Length == 0) return;
            
            await endpointsAllocatedTcs.Task.ConfigureAwait(false);

            var redisInsightResource = builder.ApplicationBuilder.Resources.OfType<RedisInsightResource>().Single();
            var insightEndpoint = redisInsightResource.PrimaryEndpoint;

            using var client = new HttpClient();
            client.BaseAddress = new Uri($"{insightEndpoint.Scheme}://{insightEndpoint.Host}:{insightEndpoint.Port}");

            var rls = e.Services.GetRequiredService<ResourceLoggerService>();
            var resourceLogger = rls.GetLogger(resource);
            
            await UpdateSettings(client, resourceLogger, ct).ConfigureAwait(false);
            await ImportDatabases(resourceLogger, redisStackResources, client, ct).ConfigureAwait(false);
        });

        return builder;

        static async Task ImportDatabases(ILogger resourceLogger, IEnumerable<RedisStackResource> redisInstances, HttpClient client, CancellationToken cancellationToken)
        {
            var databasesPath = "/api/databases";

            var pipeline = new ResiliencePipelineBuilder().AddRetry(new Polly.Retry.RetryStrategyOptions
            {
                Delay = TimeSpan.FromSeconds(2),
                MaxRetryAttempts = 5,
            }).Build();

            using var stream = new MemoryStream();
            
            // Remove existing databases
            var lookup = await pipeline.ExecuteAsync(async _ =>
            {
                var getDatabasesResponse = await client.GetFromJsonAsync<RedisDbEntry[]>(databasesPath, cancellationToken).ConfigureAwait(false);
                return getDatabasesResponse?.ToLookup(
                    i => i.Name ?? throw new InvalidDataException("Database name is missing."),
                    i => i.Id ?? throw new InvalidDataException("Database ID is missing."));
            }, cancellationToken).ConfigureAwait(false);

            var databasesToDelete = new List<Guid>();

            await using var writer = new Utf8JsonWriter(stream);

            writer.WriteStartArray();
            foreach (var redisResource in redisInstances)
            {
                if (lookup is not null && lookup.Contains(redisResource.Name))
                    databasesToDelete.AddRange(lookup[redisResource.Name]);

                if (redisResource.ServerEndpoint.IsAllocated)
                {
                    var endpoint = redisResource.ServerEndpoint;
                    writer.WriteStartObject();

                    writer.WriteString("host", redisResource.Name);
                    writer.WriteNumber("port", endpoint.TargetPort!.Value);
                    writer.WriteString("name", redisResource.Name);
                    writer.WriteNumber("db", 0);
                        
                    writer.WriteString("username", "default");
                    writer.WriteString("password", redisResource.PasswordParameter?.Value);
                    writer.WriteString("connectionType", "STANDALONE");
                    writer.WriteEndObject();
                }
            }
            writer.WriteEndArray();
            
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            stream.Seek(0, SeekOrigin.Begin);

            var content = new MultipartFormDataContent();

            var fileContent = new StreamContent(stream);
            content.Add(fileContent, "file", "redis_databases.json");
            var apiUrl = $"{databasesPath}/import";

            try
            {
                if (databasesToDelete.Count != 0)
                {
                    await pipeline.ExecuteAsync(async _ =>
                    {
                        var deleteContent = JsonContent.Create(new
                        {
                            ids = databasesToDelete
                        });

                        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, databasesPath)
                        {
                            Content = deleteContent
                        };

                        var deleteResponse = await client.SendAsync(deleteRequest, cancellationToken).ConfigureAwait(false);
                        deleteResponse.EnsureSuccessStatusCode();

                    }, cancellationToken).ConfigureAwait(false);
                }

                await pipeline.ExecuteAsync(async (ctx) =>
                {
                    var response = await client.PostAsync(apiUrl, content, ctx)
                        .ConfigureAwait(false);

                    response.EnsureSuccessStatusCode();
                }, cancellationToken).ConfigureAwait(false);

            }
            catch (Exception ex)
            {
                resourceLogger.LogError("Could not import Redis databases into RedisInsight. Reason: {Reason}", ex.Message);
            }
        }
        
        static async Task UpdateSettings(HttpClient client, ILogger resourceLogger, CancellationToken ct)
        {
            var insightSettings = new
            {
                agreements = new
                {
                    eula = true,
                    analytics = false,
                    notifications = false,
                    encryption = false
                }
            };
            
            var requestContent = JsonContent.Create(insightSettings);
            var pipeline = new ResiliencePipelineBuilder().AddRetry(new Polly.Retry.RetryStrategyOptions
            {
                MaxRetryAttempts = 3, Delay = TimeSpan.FromSeconds(2),
            }).Build();

            try
            {
                await pipeline.ExecuteAsync(async (ctx) =>
                {
                    var response = await client.PatchAsync("/api/settings", requestContent, ctx)
                        .ConfigureAwait(false);
                
                    response.EnsureSuccessStatusCode();
                }, ct).ConfigureAwait(false);

            }
            catch (Exception ex)
            {
                resourceLogger.LogError("Couldn't update RedisInsight settings Reason: {Reason}", ex.Message);
            }
        }
    }

    private class RedisDbEntry
    {
        [JsonPropertyName("id")]
        public Guid? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}