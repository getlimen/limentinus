using Limentinus.API;

var host = Host.CreateApplicationBuilder(args);

host.Services.AddLimentinus();
host.Services.AddHostedService<AgentWorker>();

await host.Build().RunAsync();
