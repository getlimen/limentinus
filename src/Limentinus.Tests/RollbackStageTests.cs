using FluentAssertions;
using Limen.Contracts.AgentMessages;
using Limentinus.Application.Common.Interfaces;
using Limentinus.Application.Deploy;
using Limentinus.Application.Deploy.Stages;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Limentinus.Tests;

public sealed class RollbackStageTests
{
    private static DeployContext MakeContext(string? newId, string? oldId)
    {
        var cmd = new DeployCommand(
            Guid.NewGuid(), Guid.NewGuid(), "nginx:latest", "my-app", 80,
            new Dictionary<string, string>(), Array.Empty<string>(),
            new HealthCheckSpec(null, 5, 3, 1), "bridge");
        return new DeployContext(cmd)
        {
            NewContainerId = newId,
            OldContainerId = oldId
        };
    }

    [Fact]
    public async Task WhenBothContainersExist_StopsAndRemovesNewAndStartsOld()
    {
        var driver = Substitute.For<IDockerDriver>();
        var stage = new RollbackStage(driver, NullLogger<RollbackStage>.Instance);
        var ctx = MakeContext("new-123", "old-456");

        await stage.ExecuteAsync(ctx, CancellationToken.None);

        await driver.Received(1).StopContainerAsync("new-123", Arg.Any<CancellationToken>());
        await driver.Received(1).RemoveContainerAsync("new-123", Arg.Any<CancellationToken>());
        await driver.Received(1).StartContainerByIdAsync("old-456", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenNoOldContainer_OnlyRemovesNew()
    {
        var driver = Substitute.For<IDockerDriver>();
        var stage = new RollbackStage(driver, NullLogger<RollbackStage>.Instance);
        var ctx = MakeContext("new-123", null);

        await stage.ExecuteAsync(ctx, CancellationToken.None);

        await driver.Received(1).StopContainerAsync("new-123", Arg.Any<CancellationToken>());
        await driver.Received(1).RemoveContainerAsync("new-123", Arg.Any<CancellationToken>());
        await driver.DidNotReceive().StartContainerByIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenNoNewContainer_OnlyStartsOld()
    {
        var driver = Substitute.For<IDockerDriver>();
        var stage = new RollbackStage(driver, NullLogger<RollbackStage>.Instance);
        var ctx = MakeContext(null, "old-456");

        await stage.ExecuteAsync(ctx, CancellationToken.None);

        await driver.DidNotReceive().StopContainerAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await driver.DidNotReceive().RemoveContainerAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await driver.Received(1).StartContainerByIdAsync("old-456", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenDriverThrows_DoesNotPropagate()
    {
        var driver = Substitute.For<IDockerDriver>();
        driver.StopContainerAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => Task.FromException(new Exception("docker error")));
        var stage = new RollbackStage(driver, NullLogger<RollbackStage>.Instance);
        var ctx = MakeContext("new-123", "old-456");

        var act = async () => await stage.ExecuteAsync(ctx, CancellationToken.None);
        await act.Should().NotThrowAsync();
    }
}
