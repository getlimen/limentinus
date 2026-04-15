using Limen.Contracts.AgentMessages;
using Microsoft.Extensions.Logging;

namespace Limentinus.Application.Deploy;

public sealed class DeployPipeline
{
    private readonly IEnumerable<IDeployStage> _stages;
    private readonly IRollbackStage _rollback;
    private readonly IDeployReporter _reporter;
    private readonly ILogger<DeployPipeline> _log;

    public DeployPipeline(
        IEnumerable<IDeployStage> stages,
        IRollbackStage rollback,
        IDeployReporter reporter,
        ILogger<DeployPipeline> log)
    {
        _stages = stages;
        _rollback = rollback;
        _reporter = reporter;
        _log = log;
    }

    public async Task<DeployResult> RunAsync(DeployCommand req, CancellationToken ct)
    {
        var ctx = new DeployContext(req);

        foreach (var stage in _stages)
        {
            await _reporter.ReportProgressAsync(req.DeploymentId, stage.Name, "running", null, ct);

            DeployStageResult result;
            try
            {
                result = await stage.ExecuteAsync(ctx, ct);
            }
            catch (Exception ex)
            {
                result = DeployStageResult.Fail(ex.Message);
            }

            if (!result.Success)
            {
                await _reporter.ReportProgressAsync(req.DeploymentId, stage.Name, $"failed: {result.Error}", null, ct);
                _log.LogWarning("Deploy {DeploymentId} stage {Stage} failed: {Error}", req.DeploymentId, stage.Name, result.Error);

                try
                {
                    await _rollback.ExecuteAsync(ctx, ct);
                }
                catch (Exception rex)
                {
                    _log.LogError(rex, "Rollback also failed for {DeploymentId}", req.DeploymentId);
                }

                return new DeployResult(req.DeploymentId, false, result.Error);
            }

            await _reporter.ReportProgressAsync(req.DeploymentId, stage.Name, "ok", null, ct);
        }

        return new DeployResult(req.DeploymentId, true, null);
    }
}
