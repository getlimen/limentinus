using System.Text.Json;
using Limentinus.Application.Common.Interfaces;
using Limentinus.Domain.Node;

namespace Limentinus.Infrastructure.Persistence;

public sealed class FileIdentityStore : IIdentityStore
{
    private readonly string _path;
    public FileIdentityStore(string path) { _path = path; }

    public async Task<NodeIdentity?> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(_path))
        {
            return null;
        }

        var txt = await File.ReadAllTextAsync(_path, ct);
        return JsonSerializer.Deserialize<NodeIdentity>(txt);
    }

    public async Task SaveAsync(NodeIdentity id, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await File.WriteAllTextAsync(_path, JsonSerializer.Serialize(id), ct);
        if (!OperatingSystem.IsWindows())
        {
            try { File.SetUnixFileMode(_path, UnixFileMode.UserRead | UnixFileMode.UserWrite); }
            catch { /* best effort on non-windows */ }
        }
    }
}
