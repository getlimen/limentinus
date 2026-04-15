using Limentinus.API;
using Limentinus.Application.Common.Interfaces;
using Limentinus.Application.Services;
using Limentinus.Infrastructure.Control;
using Limentinus.Infrastructure.Persistence;
using Limentinus.Infrastructure.Tunnel;

var host = Host.CreateApplicationBuilder(args);

var limenUrl = Environment.GetEnvironmentVariable("LIMEN_CENTRAL_URL")
    ?? throw new InvalidOperationException("LIMEN_CENTRAL_URL required");
var identityPath = Environment.GetEnvironmentVariable("LIMEN_IDENTITY_PATH") ?? "./identity.json";

host.Services.AddSingleton<IIdentityStore>(_ => new FileIdentityStore(identityPath));
host.Services.AddSingleton<ILimenControlClient>(sp =>
    new LimenWebSocketChannel(new Uri(limenUrl),
        sp.GetRequiredService<ILogger<LimenWebSocketChannel>>()));
host.Services.AddSingleton<EnrollmentService>();
host.Services.AddSingleton<IWireGuardClient>(sp =>
    new WgQuickClient(sp.GetRequiredService<ILogger<WgQuickClient>>()));
host.Services.AddHostedService<AgentWorker>();

await host.Build().RunAsync();
