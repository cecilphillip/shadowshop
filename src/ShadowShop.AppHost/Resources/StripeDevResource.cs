using System.Reflection;

namespace ShadowShop.AppHost.Resources;

public class StripeDevResource(string name, string command, string workingDir = "./.config/stripe")
    : ExecutableResource(name, command, workingDir);

public static class StripeCliBuilderExtensions
{
    public static IResourceBuilder<StripeDevResource> AddStripeDevProxy(this IDistributedApplicationBuilder builder,
        string name, IResourceBuilder<ParameterResource> secretKey, IResourceBuilder<ProjectResource> project,
        string path)
    {
        var webhookEndpoint = ReferenceExpression.Create(
            $"{project.Resource.GetEndpoint("https")}{path}");

        var scriptFileName = "listen.sh";
        return builder.AddResource(new StripeDevResource(name, "bash"))
            .WithArgs(context => { context.Args.Add(scriptFileName); })
            .WithEnvironment("STRIPE_SECRET_KEY", secretKey)
            .WithEnvironment("WEBHOOK_ENDPOINT", webhookEndpoint)
            .WithEnvironment("STRIPE_DEVICE_NAME", Assembly.GetExecutingAssembly().GetName().Name)
            .ExcludeFromManifest();
    }
}