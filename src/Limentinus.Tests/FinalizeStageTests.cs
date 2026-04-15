using FluentAssertions;
using Limen.Contracts.AgentMessages;
using Limentinus.Application.Common.Interfaces;
using Limentinus.Application.Deploy;
using Limentinus.Application.Deploy.Stages;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Limentinus.Tests;

public sealed class FinalizeStageTests
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
    public async Task WhenRenameNewThrows_RollbackRestoresOldToOriginalName()
    {
        // Arrange
        var driver = Substitute.For<IDockerDriver>();

        // Step 1 (StopContainerAsync) and step 2 (rename old → temp) succeed.
        // Step 3 (rename new → target) throws.
        driver
            .RenameContainerAsync("new-456", "my-app", Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("rename new failed"));

        var finalizeStage = new FinalizeStage(driver);
        var rollbackStage = new RollbackStage(driver, NullLogger<RollbackStage>.Instance);

        var ctx = MakeContext("new-456", "old-123");

        // Act: finalize fails
        var result = await finalizeStage.ExecuteAsync(ctx, CancellationToken.None);

        // Assert: finalize reported failure
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("rename new failed");

        // Assert: ctx records that old was renamed to temp name
        ctx.RenamedOldContainerName.Should().NotBeNull();
        ctx.RenamedOldContainerName.Should().StartWith("my-app-old-");

        // Act: rollback runs
        await rollbackStage.ExecuteAsync(ctx, CancellationToken.None);

        // Assert: rollback renamed old back to original name before starting it
        await driver.Received(1).RenameContainerAsync("old-123", "my-app", Arg.Any<CancellationToken>());
        await driver.Received(1).StartContainerByIdAsync("old-123", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenNoOldContainer_JustRenamesNewToTargetName()
    {
        var driver = Substitute.For<IDockerDriver>();
        var stage = new FinalizeStage(driver);
        var ctx = MakeContext("new-456", null);

        var result = await stage.ExecuteAsync(ctx, CancellationToken.None);

        result.Success.Should().BeTrue();
        await driver.Received(1).RenameContainerAsync("new-456", "my-app", Arg.Any<CancellationToken>());
        await driver.DidNotReceive().StopContainerAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await driver.DidNotReceive().RemoveContainerAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenOldContainerExists_FollowsFullFourStepSequence()
    {
        var driver = Substitute.For<IDockerDriver>();
        var stage = new FinalizeStage(driver);
        var ctx = MakeContext("new-456", "old-123");

        var result = await stage.ExecuteAsync(ctx, CancellationToken.None);

        result.Success.Should().BeTrue();

        // Step 1: stop old
        await driver.Received(1).StopContainerAsync("old-123", Arg.Any<CancellationToken>());
        // Step 2: rename old → temp
        await driver.Received(1).RenameContainerAsync("old-123", Arg.Is<string>(s => s.StartsWith("my-app-old-")), Arg.Any<CancellationToken>());
        // Step 3: rename new → target
        await driver.Received(1).RenameContainerAsync("new-456", "my-app", Arg.Any<CancellationToken>());
        // Step 4: remove old
        await driver.Received(1).RemoveContainerAsync("old-123", Arg.Any<CancellationToken>());
    }
}
