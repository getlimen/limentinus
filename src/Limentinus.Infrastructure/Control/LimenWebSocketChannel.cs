using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Limen.Contracts.AgentMessages;
using Limen.Contracts.Common;
using Limentinus.Application.Common.Interfaces;
using Limentinus.Domain.Node;
using Microsoft.Extensions.Logging;

namespace Limentinus.Infrastructure.Control;

public sealed class LimenWebSocketChannel : ILimenControlClient
{
    private readonly Uri _serverUri;
    private readonly ILogger<LimenWebSocketChannel> _log;

    public LimenWebSocketChannel(Uri serverUri, ILogger<LimenWebSocketChannel> log) { _serverUri = serverUri; _log = log; }

    public async Task<NodeIdentity> EnrollAsync(string key, string hostname, string[] roles, string platform, string version, CancellationToken ct)
    {
        using var ws = new ClientWebSocket();
        var uri = new Uri(_serverUri, "/api/agents/ws");
        await ws.ConnectAsync(uri, ct);

        var env = new Envelope<EnrollRequest>(AgentMessageTypes.Enroll, ConfigVersion.Zero,
            new EnrollRequest(key, hostname, roles, platform, version));
        var bytes = JsonSerializer.SerializeToUtf8Bytes(env);
        await ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct);

        var buf = new byte[16 * 1024];
        var r = await ws.ReceiveAsync(buf, ct);
        if (r.MessageType != WebSocketMessageType.Text)
        {
            throw new InvalidOperationException("Unexpected non-text response to enroll");
        }

        using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(buf, 0, r.Count));
        var resp = doc.RootElement.GetProperty("Payload").Deserialize<EnrollResponse>()
            ?? throw new InvalidOperationException("Enroll response payload was null");

        if (ws.State == WebSocketState.Open)
        {
            try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "enroll-complete", ct); }
            catch { /* ignore close races */ }
        }

        return new NodeIdentity { AgentId = resp.AgentId, Secret = resp.PermanentSecret };
    }

    public async Task RunAsync(NodeIdentity id, CancellationToken ct)
    {
        var backoff = TimeSpan.FromSeconds(1);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var ws = new ClientWebSocket();
                ws.Options.SetRequestHeader("Authorization", $"Bearer {id.AgentId}:{id.Secret}");
                var uri = new Uri(_serverUri, "/api/agents/ws");
                await ws.ConnectAsync(uri, ct);
                _log.LogInformation("Control WS connected to {Uri}", uri);
                backoff = TimeSpan.FromSeconds(1);

                await SendAsync(ws, AgentMessageTypes.Heartbeat,
                    new Heartbeat(DateTimeOffset.UtcNow, Array.Empty<string>()), ct);

                while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), ct);
                    if (ws.State != WebSocketState.Open)
                    {
                        break;
                    }

                    await SendAsync(ws, AgentMessageTypes.Heartbeat,
                        new Heartbeat(DateTimeOffset.UtcNow, Array.Empty<string>()), ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "WS connection lost; reconnecting in {Backoff}", backoff);
                try { await Task.Delay(backoff, ct); } catch (OperationCanceledException) { break; }
                backoff = TimeSpan.FromSeconds(Math.Min(60, backoff.TotalSeconds * 2));
            }
        }
    }

    private static async Task SendAsync<T>(ClientWebSocket ws, string type, T payload, CancellationToken ct)
    {
        var env = new Envelope<T>(type, ConfigVersion.Zero, payload);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(env);
        await ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
    }
}
