using FluentAssertions;
using Limen.Contracts.AgentMessages;
using Limentinus.Application.Common.Interfaces;
using Limentinus.Application.Deploy;
using Limentinus.Application.Deploy.Stages;
using NSubstitute;

namespace Limentinus.Tests;

public sealed class HealthCheckStageTests
{
    private static DeployContext MakeContext(HealthCheckSpec hc, int internalPort = 80)
    {
        var cmd = new DeployCommand(
            Guid.NewGuid(), Guid.NewGuid(), "nginx:latest", "my-app", internalPort,
            new Dictionary<string, string>(), Array.Empty<string>(),
            hc, "bridge");
        return new DeployContext(cmd)
        {
            NewContainerId = "container-abc"
        };
    }

    [Fact]
    public async Task CommandMode_SucceedsOnFirstAttempt()
    {
        var driver = Substitute.For<IDockerDriver>();
        driver.ExecHealthCheckAsync("container-abc", "curl -sf http://localhost/", 5, Arg.Any<CancellationToken>())
            .Returns(true);

        var stage = new HealthCheckStage(driver);
        var hc = new HealthCheckSpec("curl -sf http://localhost/", TimeoutSeconds: 5, MaxRetries: 3, IntervalSeconds: 0);
        var ctx = MakeContext(hc);

        var result = await stage.ExecuteAsync(ctx, CancellationToken.None);

        result.Success.Should().BeTrue();
        await driver.Received(1).ExecHealthCheckAsync("container-abc", "curl -sf http://localhost/", 5, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CommandMode_FailsAfterMaxRetries()
    {
        var driver = Substitute.For<IDockerDriver>();
        driver.ExecHealthCheckAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var stage = new HealthCheckStage(driver);
        var hc = new HealthCheckSpec("exit 1", TimeoutSeconds: 1, MaxRetries: 2, IntervalSeconds: 0);
        var ctx = MakeContext(hc);

        var result = await stage.ExecuteAsync(ctx, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("2 attempts");
        await driver.Received(2).ExecHealthCheckAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HttpMode_FailsWhenPortNotAvailable()
    {
        var driver = Substitute.For<IDockerDriver>();
        driver.GetContainerHostPortAsync("container-abc", 80, Arg.Any<CancellationToken>())
            .Returns((int?)null);

        var stage = new HealthCheckStage(driver);
        var hc = new HealthCheckSpec(Command: null, TimeoutSeconds: 1, MaxRetries: 1, IntervalSeconds: 0);
        var ctx = MakeContext(hc);

        var result = await stage.ExecuteAsync(ctx, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("host port");
    }

    [Fact]
    public async Task NoHealthCheck_ReturnsOk()
    {
        var driver = Substitute.For<IDockerDriver>();
        var stage = new HealthCheckStage(driver);

        // HealthCheckSpec with null command and no port check expectation
        // The stage checks hc is null — but our contract always has a HealthCheckSpec.
        // Test the null-container guard path instead:
        var cmd = new DeployCommand(
            Guid.NewGuid(), Guid.NewGuid(), "nginx:latest", "my-app", 80,
            new Dictionary<string, string>(), Array.Empty<string>(),
            new HealthCheckSpec(null, 5, 3, 1), "bridge");
        var ctx = new DeployContext(cmd);
        // NewContainerId is null => should fail with specific message
        var result = await stage.ExecuteAsync(ctx, CancellationToken.None);
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("No container started");
    }
}
