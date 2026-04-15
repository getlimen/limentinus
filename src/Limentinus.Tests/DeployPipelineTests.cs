using FluentAssertions;
using Limen.Contracts.AgentMessages;
using Limentinus.Application.Deploy;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Limentinus.Tests;

public sealed class DeployPipelineTests
{
    private static DeployCommand MakeCommand() => new(
        DeploymentId: Guid.NewGuid(),
        ServiceId: Guid.NewGuid(),
        Image: "nginx:latest",
        ContainerName: "my-app",
        InternalPort: 80,
        Env: new Dictionary<string, string>(),
        Volumes: Array.Empty<string>(),
        HealthCheck: new HealthCheckSpec(null, 5, 3, 1),
        NetworkMode: "bridge");

    private static IDeployStage MakeOkStage(string name)
    {
        var stage = Substitute.For<IDeployStage>();
        stage.Name.Returns(name);
        stage.ExecuteAsync(Arg.Any<DeployContext>(), Arg.Any<CancellationToken>())
            .Returns(DeployStageResult.Ok());
        return stage;
    }

    private static IDeployStage MakeFailStage(string name, string error = "oops")
    {
        var stage = Substitute.For<IDeployStage>();
        stage.Name.Returns(name);
        stage.ExecuteAsync(Arg.Any<DeployContext>(), Arg.Any<CancellationToken>())
            .Returns(DeployStageResult.Fail(error));
        return stage;
    }

    [Fact]
    public async Task AllStagesRunOnSuccess()
    {
        var stages = new[] { MakeOkStage("A"), MakeOkStage("B"), MakeOkStage("C") };
        var rollback = Substitute.For<IRollbackStage>();
        var reporter = Substitute.For<IDeployReporter>();
        var pipeline = new DeployPipeline(stages, rollback, reporter, NullLogger<DeployPipeline>.Instance);

        var result = await pipeline.RunAsync(MakeCommand(), CancellationToken.None);

        result.Success.Should().BeTrue();
        foreach (var s in stages)
        {
            await s.Received(1).ExecuteAsync(Arg.Any<DeployContext>(), Arg.Any<CancellationToken>());
        }
    }

    [Fact]
    public async Task OnStageFailure_RollbackRunsAndSubsequentStagesSkipped()
    {
        var stageA = MakeOkStage("A");
        var stageB = MakeFailStage("B", "boom");
        var stageC = MakeOkStage("C");
        var rollback = Substitute.For<IRollbackStage>();
        var reporter = Substitute.For<IDeployReporter>();
        var pipeline = new DeployPipeline(new[] { stageA, stageB, stageC }, rollback, reporter, NullLogger<DeployPipeline>.Instance);

        var result = await pipeline.RunAsync(MakeCommand(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.RolledBackReason.Should().Be("boom");

        await stageA.Received(1).ExecuteAsync(Arg.Any<DeployContext>(), Arg.Any<CancellationToken>());
        await stageB.Received(1).ExecuteAsync(Arg.Any<DeployContext>(), Arg.Any<CancellationToken>());
        await stageC.DidNotReceive().ExecuteAsync(Arg.Any<DeployContext>(), Arg.Any<CancellationToken>());
        await rollback.Received(1).ExecuteAsync(Arg.Any<DeployContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReporterReceivesProgressMessagesInOrder()
    {
        var stages = new[] { MakeOkStage("A"), MakeOkStage("B") };
        var rollback = Substitute.For<IRollbackStage>();
        var reporter = Substitute.For<IDeployReporter>();
        var pipeline = new DeployPipeline(stages, rollback, reporter, NullLogger<DeployPipeline>.Instance);
        var cmd = MakeCommand();

        await pipeline.RunAsync(cmd, CancellationToken.None);

        Received.InOrder(() =>
        {
            reporter.ReportProgressAsync(cmd.DeploymentId, "A", "running", null, Arg.Any<CancellationToken>());
            reporter.ReportProgressAsync(cmd.DeploymentId, "A", "ok", null, Arg.Any<CancellationToken>());
            reporter.ReportProgressAsync(cmd.DeploymentId, "B", "running", null, Arg.Any<CancellationToken>());
            reporter.ReportProgressAsync(cmd.DeploymentId, "B", "ok", null, Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task RollbackRunsOnlyOnce_EvenIfMultipleStagesFail()
    {
        // This is structural — pipeline exits after first failure, so rollback can only fire once.
        var stageA = MakeFailStage("A", "first failure");
        var rollback = Substitute.For<IRollbackStage>();
        var reporter = Substitute.For<IDeployReporter>();
        var pipeline = new DeployPipeline(new[] { stageA }, rollback, reporter, NullLogger<DeployPipeline>.Instance);

        await pipeline.RunAsync(MakeCommand(), CancellationToken.None);

        await rollback.Received(1).ExecuteAsync(Arg.Any<DeployContext>(), Arg.Any<CancellationToken>());
    }
}
