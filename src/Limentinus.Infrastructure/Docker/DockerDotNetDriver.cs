using Docker.DotNet;
using Docker.DotNet.Models;
using Limentinus.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Limentinus.Infrastructure.Docker;

public sealed class DockerDotNetDriver : IDockerDriver, IDisposable
{
    private readonly DockerClient _client;
    private readonly ILogger<DockerDotNetDriver> _log;

    public DockerDotNetDriver(ILogger<DockerDotNetDriver> log)
    {
        _log = log;
        var endpoint = OperatingSystem.IsWindows()
            ? new Uri("npipe://./pipe/docker_engine")
            : new Uri("unix:///var/run/docker.sock");
        _client = new DockerClientConfiguration(endpoint).CreateClient();
    }

    public async Task<string> PullImageAsync(string image, IProgress<string>? progress, CancellationToken ct)
    {
        var colonIdx = image.LastIndexOf(':');
        string repo, tag;
        if (colonIdx > 0)
        {
            repo = image[..colonIdx];
            tag = image[(colonIdx + 1)..];
        }
        else
        {
            repo = image;
            tag = "latest";
        }

        IProgress<JSONMessage>? dockerProgress = progress is null
            ? null
            : new Progress<JSONMessage>(m =>
            {
                try
                {
                    var line = $"{m.Status} {m.Progress}".Trim();
                    progress.Report(line);
                }
                catch
                {
                    // swallow
                }
            });

        await _client.Images.CreateImageAsync(
            new ImagesCreateParameters { FromImage = repo, Tag = tag },
            null,
            dockerProgress,
            ct);

        var inspect = await _client.Images.InspectImageAsync(image, ct);
        return inspect.ID;
    }

    public async Task<string?> FindContainerIdByNameAsync(string name, CancellationToken ct)
    {
        var containers = await _client.Containers.ListContainersAsync(
            new ContainersListParameters
            {
                All = true,
                Filters = new Dictionary<string, IDictionary<string, bool>>
                {
                    ["name"] = new Dictionary<string, bool> { [name] = true }
                }
            }, ct);

        var match = containers.FirstOrDefault(c =>
            c.Names.Any(n => n == "/" + name || n == name));
        return match?.ID;
    }

    public async Task<string> StartContainerAsync(StartContainerSpec spec, CancellationToken ct)
    {
        var portBindings = new Dictionary<string, IList<PortBinding>>
        {
            [$"{spec.InternalPort}/tcp"] = new List<PortBinding>
            {
                new PortBinding { HostPort = "" }
            }
        };

        var binds = new List<string>(spec.Volumes);

        var env = spec.Env
            .Select(kv => $"{kv.Key}={kv.Value}")
            .ToList();

        var createParams = new CreateContainerParameters
        {
            Image = spec.Image,
            Name = spec.Name,
            Env = env,
            HostConfig = new HostConfig
            {
                PortBindings = portBindings,
                Binds = binds,
                NetworkMode = string.IsNullOrEmpty(spec.NetworkMode) ? null : spec.NetworkMode
            }
        };

        var created = await _client.Containers.CreateContainerAsync(createParams, ct);
        var started = await _client.Containers.StartContainerAsync(created.ID, new ContainerStartParameters(), ct);
        if (!started)
        {
            _log.LogWarning("Container {Name} ({Id}) reported already started", spec.Name, created.ID);
        }

        return created.ID;
    }

    public async Task StartContainerByIdAsync(string containerId, CancellationToken ct)
    {
        await _client.Containers.StartContainerAsync(containerId, new ContainerStartParameters(), ct);
    }

    public async Task StopContainerAsync(string containerId, CancellationToken ct)
    {
        await _client.Containers.StopContainerAsync(
            containerId,
            new ContainerStopParameters { WaitBeforeKillSeconds = 30u },
            ct);
    }

    public async Task RemoveContainerAsync(string containerId, CancellationToken ct)
    {
        await _client.Containers.RemoveContainerAsync(
            containerId,
            new ContainerRemoveParameters { Force = true },
            ct);
    }

    public async Task RenameContainerAsync(string containerId, string newName, CancellationToken ct)
    {
        await _client.Containers.RenameContainerAsync(
            containerId,
            new ContainerRenameParameters { NewName = newName },
            ct);
    }

    public async Task<bool> ExecHealthCheckAsync(string containerId, string command, int timeoutSeconds, CancellationToken ct)
    {
        var exec = await _client.Exec.ExecCreateContainerAsync(
            containerId,
            new ContainerExecCreateParameters
            {
                Cmd = new[] { "sh", "-c", command },
                AttachStdout = true,
                AttachStderr = true
            },
            ct);

        await _client.Exec.StartContainerExecAsync(exec.ID, ct);

        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            var inspect = await _client.Exec.InspectContainerExecAsync(exec.ID, ct);
            if (!inspect.Running)
            {
                return inspect.ExitCode == 0;
            }

            await Task.Delay(250, ct);
        }

        return false;
    }

    public async Task<int?> GetContainerHostPortAsync(string containerId, int internalPort, CancellationToken ct)
    {
        var inspect = await _client.Containers.InspectContainerAsync(containerId, ct);
        var key = $"{internalPort}/tcp";
        if (inspect.NetworkSettings?.Ports?.TryGetValue(key, out var bindings) == true
            && bindings is { Count: > 0 }
            && int.TryParse(bindings[0].HostPort, out var port))
        {
            return port;
        }

        return null;
    }

    public void Dispose() => _client.Dispose();
}
