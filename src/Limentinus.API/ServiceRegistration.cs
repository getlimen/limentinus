using Limentinus.Application.Common.Interfaces;
using Limentinus.Application.Deploy;
using Limentinus.Application.Deploy.Stages;
using Limentinus.Application.Services;
using Limentinus.Infrastructure.Control;
using Limentinus.Infrastructure.Docker;
using Limentinus.Infrastructure.Persistence;
using Limentinus.Infrastructure.Tunnel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Limentinus.API;

public static class ServiceRegistration
{
    /// <summary>
    /// Registers all Limentinus services using values from the environment:
    /// LIMEN_CENTRAL_URL (required) and LIMEN_IDENTITY_PATH (optional, defaults to ./identity.json).
    /// </summary>
    public static IServiceCollection AddLimentinus(this IServiceCollection services)
    {
        var limenUrl = Environment.GetEnvironmentVariable("LIMEN_CENTRAL_URL")
            ?? throw new InvalidOperationException("LIMEN_CENTRAL_URL required");
        var identityPath = Environment.GetEnvironmentVariable("LIMEN_IDENTITY_PATH") ?? "./identity.json";
        return services.AddLimentinus(limenUrl, identityPath);
    }

    /// <summary>
    /// Registers all Limentinus services with explicit connection parameters.
    /// </summary>
    public static IServiceCollection AddLimentinus(this HostApplicationBuilder builder, string limenUrl, string identityPath)
        => builder.Services.AddLimentinus(limenUrl, identityPath);

    /// <summary>
    /// Core registration logic shared by all overloads.
    /// </summary>
    public static IServiceCollection AddLimentinus(this IServiceCollection services, string limenUrl, string identityPath)
    {
        services.AddSingleton<IIdentityStore>(_ => new FileIdentityStore(identityPath));
        services.AddSingleton<IWireGuardClient>(sp =>
            new WgQuickClient(sp.GetRequiredService<ILogger<WgQuickClient>>()));
        services.AddSingleton<EnrollmentService>();

        services.AddSingleton<IDockerDriver>(sp =>
            new DockerDotNetDriver(sp.GetRequiredService<ILogger<DockerDotNetDriver>>()));

        // Stages — order-sensitive
        services.AddSingleton<IDeployStage, PullImageStage>();
        services.AddSingleton<IDeployStage, CaptureOldStage>();
        services.AddSingleton<IDeployStage, StartNewStage>();
        services.AddSingleton<IDeployStage, HealthCheckStage>();
        services.AddSingleton<IDeployStage, FinalizeStage>();
        services.AddSingleton<IRollbackStage, RollbackStage>();

        // Channel acts as both control client and deploy reporter.
        // Func<DeployPipeline> breaks the circular dependency:
        //   DeployPipeline -> IDeployReporter -> LimenWebSocketChannel -> Func<DeployPipeline>
        services.AddSingleton<DeployPipeline>();
        services.AddSingleton<LimenWebSocketChannel>(sp =>
            new LimenWebSocketChannel(
                new Uri(limenUrl),
                sp.GetRequiredService<ILogger<LimenWebSocketChannel>>(),
                () => sp.GetRequiredService<DeployPipeline>()));
        services.AddSingleton<ILimenControlClient>(sp => sp.GetRequiredService<LimenWebSocketChannel>());
        services.AddSingleton<IDeployReporter>(sp => sp.GetRequiredService<LimenWebSocketChannel>());

        return services;
    }
}
