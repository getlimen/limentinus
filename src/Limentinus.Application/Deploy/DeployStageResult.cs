namespace Limentinus.Application.Deploy;

public sealed record DeployStageResult(bool Success, string? Error)
{
    public static DeployStageResult Ok() => new(true, null);
    public static DeployStageResult Fail(string err) => new(false, err);
}
