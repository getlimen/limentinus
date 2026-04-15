using Limentinus.API;
using Limentinus.Application.Common.Interfaces;
using Limentinus.Application.Deploy;
using Limentinus.Application.Deploy.Stages;
using Limentinus.Application.Services;
using Limentinus.Infrastructure.Control;
using Limentinus.Infrastructure.Docker;
using Limentinus.Infrastructure.Persistence;
using Limentinus.Infrastructure.Tunnel;

var host = Host.CreateApplicationBuilder(args);

var limenUrl = Environment.GetEnvironmentVariable("LIMEN_CENTRAL_URL")
    ?? throw new InvalidOperationException("LIMEN_CENTRAL_URL required");
var identityPath = Environment.GetEnvironmentVariable("LIMEN_IDENTITY_PATH") ?? "./identity.json";

host.Services.AddSingleton<IIdentityStore>(_ => new FileIdentityStore(identityPath));
host.Services.AddSingleton<IWireGuardClient>(sp =>
    new WgQuickClient(sp.GetRequiredService<ILogger<WgQuickClient>>()));
host.Services.AddSingleton<EnrollmentService>();

host.Services.AddSingleton<IDockerDriver>(sp =>
    new DockerDotNetDriver(sp.GetRequiredService<ILogger<DockerDotNetDriver>>()));

// Stages — order-sensitive
host.Services.AddSingleton<IDeployStage, PullImageStage>();
host.Services.AddSingleton<IDeployStage, CaptureOldStage>();
host.Services.AddSingleton<IDeployStage, StartNewStage>();
host.Services.AddSingleton<IDeployStage, HealthCheckStage>();
host.Services.AddSingleton<IDeployStage, FinalizeStage>();
host.Services.AddSingleton<IRollbackStage, RollbackStage>();

// Channel acts as both control client and deploy reporter.
// Func<DeployPipeline> breaks the circular dependency:
//   DeployPipeline -> IDeployReporter -> LimenWebSocketChannel -> Func<DeployPipeline>
host.Services.AddSingleton<DeployPipeline>();
host.Services.AddSingleton<LimenWebSocketChannel>(sp =>
    new LimenWebSocketChannel(
        new Uri(limenUrl),
        sp.GetRequiredService<ILogger<LimenWebSocketChannel>>(),
        () => sp.GetRequiredService<DeployPipeline>()));
host.Services.AddSingleton<ILimenControlClient>(sp => sp.GetRequiredService<LimenWebSocketChannel>());
host.Services.AddSingleton<IDeployReporter>(sp => sp.GetRequiredService<LimenWebSocketChannel>());

host.Services.AddHostedService<AgentWorker>();

await host.Build().RunAsync();
