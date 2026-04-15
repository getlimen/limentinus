using System.Diagnostics;
using System.Text;
using Limen.Contracts.AgentMessages;
using Limentinus.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Limentinus.Infrastructure.Tunnel;

public sealed class WgQuickClient : IWireGuardClient
{
    private readonly ILogger<WgQuickClient> _log;
    private readonly string _iface;
    private readonly string _configPath;

    public WgQuickClient(ILogger<WgQuickClient> log, string iface = "wg0")
    {
        _log = log;
        _iface = iface;
        _configPath = $"/etc/wireguard/{iface}.conf";
    }

    public bool IsAvailable
    {
        get
        {
            if (!OperatingSystem.IsLinux())
            {
                return false;
            }

            try
            {
                var psi = new ProcessStartInfo("sh", "-c \"command -v wg-quick > /dev/null && command -v wg > /dev/null\"")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };
                using var p = Process.Start(psi)!;
                p.WaitForExit();
                return p.ExitCode == 0;
            }
            catch { return false; }
        }
    }

    public async Task BringUpAsync(WireGuardConfig cfg, CancellationToken ct)
    {
        if (!IsAvailable)
        {
            _log.LogWarning("wg-quick not available; skipping tunnel setup (running in clear-WS dev mode)");
            return;
        }

        var content = RenderConfig(cfg);
        await File.WriteAllTextAsync(_configPath, content, ct);
        await File.WriteAllTextAsync("/tmp/wg0-permissions-hint", "chmod 600 applied by wg-quick", ct).ConfigureAwait(false);

        if (OperatingSystem.IsLinux())
        {
            try { File.SetUnixFileMode(_configPath, UnixFileMode.UserRead | UnixFileMode.UserWrite); } catch { /* best effort */ }
        }

        // Bring down previous instance if any
        await RunAsync("wg-quick", $"down {_iface}", throwOnError: false, ct);
        await RunAsync("wg-quick", $"up {_iface}", throwOnError: true, ct);

        _log.LogInformation("WireGuard interface {Iface} up with address {Addr}", _iface, cfg.InterfaceAddress);
    }

    public async Task TearDownAsync(CancellationToken ct)
    {
        if (!IsAvailable)
        {
            return;
        }

        await RunAsync("wg-quick", $"down {_iface}", throwOnError: false, ct);
    }

    private static string RenderConfig(WireGuardConfig cfg)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[Interface]");
        sb.AppendLine($"PrivateKey = {cfg.PrivateKey}");
        sb.AppendLine($"Address = {cfg.InterfaceAddress}");
        sb.AppendLine();
        sb.AppendLine("[Peer]");
        sb.AppendLine($"PublicKey = {cfg.ServerPublicKey}");
        sb.AppendLine($"Endpoint = {cfg.ServerEndpoint}");
        sb.AppendLine("AllowedIPs = 10.42.0.0/24");
        if (cfg.KeepaliveSeconds > 0)
        {
            sb.AppendLine($"PersistentKeepalive = {cfg.KeepaliveSeconds}");
        }

        return sb.ToString();
    }

    private async Task RunAsync(string cmd, string args, bool throwOnError, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(cmd, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException($"could not start {cmd}");
        var stderrTask = p.StandardError.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct);
        if (p.ExitCode != 0)
        {
            var err = await stderrTask;
            var msg = $"`{cmd} {args}` failed: {err}";
            if (throwOnError)
            {
                throw new InvalidOperationException(msg);
            }

            _log.LogWarning("{Message}", msg);
        }
    }
}
