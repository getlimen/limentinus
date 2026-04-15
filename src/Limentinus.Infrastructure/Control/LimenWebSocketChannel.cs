using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Limen.Contracts.AgentMessages;
using Limen.Contracts.Common;
using Limentinus.Application.Common.Interfaces;
using Limentinus.Application.Deploy;
using Limentinus.Domain.Node;
using Microsoft.Extensions.Logging;

namespace Limentinus.Infrastructure.Control;

public sealed class LimenWebSocketChannel : ILimenControlClient, IDeployReporter
{
    private readonly Uri _serverUri;
    private readonly ILogger<LimenWebSocketChannel> _log;
    private readonly Func<DeployPipeline> _pipelineFactory;

    private ClientWebSocket? _currentWs;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly object _wsLock = new();

    public LimenWebSocketChannel(
        Uri serverUri,
        ILogger<LimenWebSocketChannel> log,
        Func<DeployPipeline> pipelineFactory)
    {
        _serverUri = serverUri;
        _log = log;
        _pipelineFactory = pipelineFactory;
    }

    public async Task<EnrollmentOutcome> EnrollAsync(string key, string hostname, string[] roles, string platform, string version, CancellationToken ct)
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

        return new EnrollmentOutcome(
            new NodeIdentity { AgentId = resp.AgentId, Secret = resp.PermanentSecret },
            resp.Wireguard);
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

                lock (_wsLock) { _currentWs = ws; }

                try
                {
                    await SendFrameAsync(ws, AgentMessageTypes.Heartbeat,
                        new Heartbeat(DateTimeOffset.UtcNow, Array.Empty<string>()), ct);

                    var heartbeatTask = HeartbeatLoopAsync(ws, ct);
                    var receiveTask = ReceiveLoopAsync(ws, ct);

                    await Task.WhenAny(heartbeatTask, receiveTask);
                }
                finally
                {
                    lock (_wsLock) { _currentWs = null; }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                lock (_wsLock) { _currentWs = null; }
                _log.LogWarning(ex, "WS connection lost; reconnecting in {Backoff}", backoff);
                try { await Task.Delay(backoff, ct); } catch (OperationCanceledException) { break; }
                backoff = TimeSpan.FromSeconds(Math.Min(60, backoff.TotalSeconds * 2));
            }
        }
    }

    public async Task ReportProgressAsync(Guid deploymentId, string stage, string message, int? percentComplete, CancellationToken ct)
    {
        ClientWebSocket? ws;
        lock (_wsLock) { ws = _currentWs; }

        if (ws is null || ws.State != WebSocketState.Open)
        {
            _log.LogWarning("Cannot send deploy progress: WS not connected (deployment {DeploymentId})", deploymentId);
            return;
        }

        try
        {
            var progress = new DeployProgress(deploymentId, stage, message, percentComplete);
            await SendFrameAsync(ws, AgentMessageTypes.DeployProgress, progress, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to send deploy progress for {DeploymentId}", deploymentId);
        }
    }

    private async Task HeartbeatLoopAsync(ClientWebSocket ws, CancellationToken ct)
    {
        while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
            if (ws.State != WebSocketState.Open)
            {
                break;
            }
            await SendFrameAsync(ws, AgentMessageTypes.Heartbeat,
                new Heartbeat(DateTimeOffset.UtcNow, Array.Empty<string>()), ct);
        }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket ws, CancellationToken ct)
    {
        var buf = new byte[64 * 1024];
        while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            WebSocketReceiveResult result;
            try
            {
                result = await ws.ReceiveAsync(buf, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (WebSocketException ex)
            {
                _log.LogWarning(ex, "WebSocket error during receive");
                break;
            }

            if (result.MessageType == WebSocketMessageType.Close)
            {
                _log.LogInformation("WS closed by server");
                break;
            }

            if (result.MessageType != WebSocketMessageType.Text)
            {
                continue;
            }

            HandleTextFrame(buf, result.Count, ct);
        }
    }

    private void HandleTextFrame(byte[] buf, int count, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(buf.AsMemory(0, count));
            var root = doc.RootElement;

            if (!root.TryGetProperty("Type", out var typeProp))
            {
                _log.LogWarning("Received WS frame without Type property");
                return;
            }

            var type = typeProp.GetString();

            switch (type)
            {
                case AgentMessageTypes.Deploy:
                    var cmd = root.GetProperty("Payload").Deserialize<DeployCommand>()
                        ?? throw new InvalidOperationException("Deploy payload was null");
                    _ = HandleDeployAsync(cmd, ct);
                    break;

                case AgentMessageTypes.HeartbeatAck:
                    _log.LogTrace("HeartbeatAck received");
                    break;

                default:
                    _log.LogInformation("Ignoring unknown WS message type: {Type}", type);
                    break;
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to parse incoming WS frame");
        }
    }

    private async Task HandleDeployAsync(DeployCommand cmd, CancellationToken ct)
    {
        try
        {
            var pipeline = _pipelineFactory();
            var deployResult = await pipeline.RunAsync(cmd, ct);

            ClientWebSocket? ws;
            lock (_wsLock) { ws = _currentWs; }

            if (ws is { } activeWs && activeWs.State == WebSocketState.Open)
            {
                await SendFrameAsync(activeWs, AgentMessageTypes.DeployResult, deployResult, ct);
            }
            else
            {
                _log.LogWarning("Could not send deploy result for {DeploymentId}: WS not connected", cmd.DeploymentId);
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Unhandled error during deploy {DeploymentId}", cmd.DeploymentId);
        }
    }

    private async Task SendFrameAsync<T>(ClientWebSocket ws, string type, T payload, CancellationToken ct)
    {
        var env = new Envelope<T>(type, ConfigVersion.Zero, payload);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(env);

        await _sendLock.WaitAsync(ct);
        try
        {
            await ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
        }
        finally
        {
            _sendLock.Release();
        }
    }
}
