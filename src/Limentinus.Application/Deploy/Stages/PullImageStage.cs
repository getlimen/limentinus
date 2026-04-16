using Limentinus.Application.Common.Interfaces;

namespace Limentinus.Application.Deploy.Stages;

public sealed class PullImageStage : IDeployStage
{
    private readonly IDockerDriver _driver;
    private readonly IDeployReporter _reporter;

    public PullImageStage(IDockerDriver driver, IDeployReporter reporter)
    {
        _driver = driver;
        _reporter = reporter;
    }

    public string Name => "PullImage";

    public async Task<DeployStageResult> ExecuteAsync(DeployContext ctx, CancellationToken ct)
    {
        try
        {
            var progress = new Progress<string>(msg =>
            {
                _ = _reporter.ReportProgressAsync(ctx.Request.DeploymentId, Name, msg, null, ct)
                    .ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            // swallow reporter errors
                        }
                    }, TaskScheduler.Default);
            });

            ctx.PulledImageId = await _driver.PullImageAsync(ctx.Request.Image, progress, ct);
            return DeployStageResult.Ok();
        }
        catch (Exception ex)
        {
            return DeployStageResult.Fail(ex.Message);
        }
    }
}
