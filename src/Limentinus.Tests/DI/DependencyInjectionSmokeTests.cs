using FluentAssertions;
using Limentinus.API;
using Limentinus.Application.Deploy;
using Limentinus.Infrastructure.Control;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Limentinus.Tests.DI;

public sealed class DependencyInjectionSmokeTests : IDisposable
{
    private readonly string _identityPath;

    public DependencyInjectionSmokeTests()
    {
        _identityPath = Path.GetTempFileName();
    }

    [Fact]
    public void AddLimentinus_CanResolveDeployPipeline_WithoutCircularDependency()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddLimentinus("https://limen.example.test", _identityPath);

        using var host = builder.Build();
        var sp = host.Services;

        // Assert DeployPipeline resolves (covers stages, docker driver, reporter)
        var pipeline = sp.GetRequiredService<DeployPipeline>();
        pipeline.Should().NotBeNull();

        // Assert channel resolves (covers the Func<DeployPipeline> cycle-breaker)
        var channel = sp.GetRequiredService<LimenWebSocketChannel>();
        channel.Should().NotBeNull();
    }

    public void Dispose()
    {
        try { File.Delete(_identityPath); } catch { /* best effort */ }
    }
}
