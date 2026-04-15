namespace Limentinus.Application.Common.Interfaces;

public interface IDockerDriver
{
    Task<string> PullImageAsync(string image, IProgress<string>? progress, CancellationToken ct);
    Task<string?> FindContainerIdByNameAsync(string name, CancellationToken ct);
    Task<string> StartContainerAsync(StartContainerSpec spec, CancellationToken ct);
    Task StartContainerByIdAsync(string containerId, CancellationToken ct);
    Task StopContainerAsync(string containerId, CancellationToken ct);
    Task RemoveContainerAsync(string containerId, CancellationToken ct);
    Task RenameContainerAsync(string containerId, string newName, CancellationToken ct);
    Task<bool> ExecHealthCheckAsync(string containerId, string command, int timeoutSeconds, CancellationToken ct);
    Task<int?> GetContainerHostPortAsync(string containerId, int internalPort, CancellationToken ct);
}

public sealed record StartContainerSpec(
    string Image,
    string Name,
    int InternalPort,
    Dictionary<string, string> Env,
    string[] Volumes,
    string NetworkMode);
