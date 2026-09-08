using System.Collections.Concurrent;
using System.IO.Ports;
using System.Text.Json;
using FreeSql;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.Channels;
using Wombat.IndustrialCommunication.Core;
using Wombat.IndustrialCommunication.Gateway.Domain;
using Wombat.IndustrialCommunication.Gateway.Infrastructure;
using Wombat.IndustrialCommunication.Modbus;
using Wombat.IndustrialCommunication.Models;
using Wombat.IndustrialCommunication.PLC;
using Wombat.IndustrialCommunication.Server;

namespace Wombat.IndustrialCommunication.Gateway.Application;

public sealed class PointDataUpdatedEventArgs(long pointId, object? value, string quality, DateTimeOffset atUtc) : EventArgs
{
    public long PointId { get; } = pointId;
    public object? Value { get; } = value;
    public string Quality { get; } = quality;
    public DateTimeOffset AtUtc { get; } = atUtc;
}

public sealed class GatewayRuntimeService(GatewayDb db, HistoryLogService history, ILogger<GatewayRuntimeService> log) : BackgroundService
{
    readonly ChannelManager channels = new();
    readonly ConcurrentDictionary<long, ServerHost> servers = new();
    readonly SemaphoreSlim forwardingLock = new(1, 1);
    readonly ConcurrentDictionary<long, DateTimeOffset> nextPoll = new();
    readonly ConcurrentDictionary<long, string> suppressedValues = new();
    readonly TaskCompletionSource<bool> ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly ConcurrentDictionary<string, DateTimeOffset> nextLog = new();

    public event EventHandler<PointDataUpdatedEventArgs>? PointDataUpdated;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        channels.StateChanged += (_, e) => log.LogInformation("Channel {ChannelId}: {Previous} -> {Current}", e.ChannelId, e.Previous, e.Current);
        try
        {
            foreach (var channel in db.Channels.Where(x => x.Enabled).ToList())
            {
                try { await SyncChannelCoreAsync(channel, stoppingToken); }
                catch (Exception ex) { LogFailure("channel-start", "Channel", ex, channel.Id); }
            }
            ready.TrySetResult(true);
            while (!stoppingToken.IsCancellationRequested)
            {
                foreach (var point in db.Points.Where(x => x.Enabled).ToList())
                {
                    var due = nextPoll.GetOrAdd(point.Id, DateTimeOffset.MinValue);
                    if (due > DateTimeOffset.UtcNow) continue;
                    nextPoll[point.Id] = DateTimeOffset.UtcNow.AddMilliseconds(Math.Max(100, point.ScanIntervalMs));
                    try { await ReadPointAsync(point.Id, stoppingToken); }
                    catch (Exception ex) { LogFailure("point-read", "Read", ex, point.ChannelId, point.Id); }
                }
                await RunTimedForwardingsAsync(stoppingToken);
                await Task.Delay(100, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally
        {
            await channels.DisposeAsync();
            foreach (var server in servers.Values) await server.DisposeAsync();
        }
    }

    public async Task WaitReadyAsync(CancellationToken cancellationToken = default) => await ready.Task.WaitAsync(cancellationToken);

    public async Task SyncChannelAsync(Channel channel, CancellationToken cancellationToken = default)
    {
        await WaitReadyIfRunningAsync(cancellationToken);
        await SyncChannelCoreAsync(channel, cancellationToken);
    }

    async Task SyncChannelCoreAsync(Channel channel, CancellationToken cancellationToken)
    {
        var id = RuntimeId(channel.Id);
        if (!channel.Enabled) { await RemoveChannelAsync(channel.Id); return; }
        if (string.Equals(channel.Role, "Server", StringComparison.OrdinalIgnoreCase))
        {
            await channels.RemoveAsync(id);
            await SyncServerAsync(channel, cancellationToken);
            return;
        }
        await RemoveServerAsync(channel.Id);
        var options = BuildOptions(channel);
        if (channels.TryGetSnapshot(id, out _)) await channels.RestartAsync(id, options, cancellationToken);
        else await channels.AddAsync(options, cancellationToken);
    }

    public async Task RemoveChannelAsync(long channelId)
    {
        await channels.RemoveAsync(RuntimeId(channelId));
        await RemoveServerAsync(channelId);
    }

    public async Task StopChannelAsync(long channelId) => await RemoveChannelAsync(channelId);

    public void RemovePoint(long pointId)
    {
        nextPoll.TryRemove(pointId, out _);
        suppressedValues.TryRemove(pointId, out _);
    }

    public void RemoveForwarding(long forwardingId) => nextForward.TryRemove(forwardingId, out _);

    public async Task<object?> GetSnapshotAsync(long channelId)
    {
        await WaitReadyAsync();
        if (channels.TryGetSnapshot(RuntimeId(channelId), out var snapshot)) return snapshot;
        return servers.TryGetValue(channelId, out var server) ? new { server.Id, State = server.IsRunning ? "Online" : "Stopped" } : null;
    }

    public async Task<OperationResult<object?>> ReadPointAsync(long pointId, CancellationToken cancellationToken = default)
    {
        await WaitReadyIfRunningAsync(cancellationToken);
        var point = db.Points.Where(x => x.Id == pointId).First() ?? throw new KeyNotFoundException("点位不存在");
        var channel = db.Channels.Where(x => x.Id == point.ChannelId).First() ?? throw new KeyNotFoundException("通道不存在");
        if (!channel.Enabled || !point.Enabled) return OperationResult.CreateFailedResult<object?>("通道或点位已禁用");
        if (!Enum.TryParse<DataTypeEnums>(point.DataType, true, out var dataType)) return OperationResult.CreateFailedResult<object?>("不支持的数据类型");
        OperationResult<object?> result = await ReadAsync(channel, dataType, point.Address, cancellationToken);
        if (!result.IsSuccess) { point.Quality = "Bad"; db.Db.Update<ChannelPoint>().SetSource(point).ExecuteAffrows(); LogFailure("point-read", "Read", null, point.ChannelId, point.Id, result.Message); return result; }
        var value = ApplyTransform(result.ResultValue, point.ValueTransformJson, false);
        var valueJson = JsonSerializer.Serialize(value);
        var changed = !string.Equals(point.ValueJson, valueJson, StringComparison.Ordinal);
        point.ValueJson = valueJson; point.Quality = "Good"; point.LastUpdatedAt = DateTime.UtcNow;
        db.Db.Update<ChannelPoint>().SetSource(point).ExecuteAffrows();
        PointDataUpdated?.Invoke(this, new PointDataUpdatedEventArgs(point.Id, value, point.Quality, DateTimeOffset.UtcNow));
        history.Sample(point.Id, value, point.Quality, DateTimeOffset.UtcNow);
        if (suppressedValues.TryGetValue(point.Id, out var suppressed) && suppressed == valueJson) suppressedValues.TryRemove(point.Id, out _);
        else await ForwardAsync(point, changed, cancellationToken);
        return OperationResult.CreateSuccessResult<object?>(value);
    }

    public Task<OperationResult> WritePointAsync(long pointId, JsonElement rawValue, CancellationToken cancellationToken = default) => WritePointAsync(pointId, rawValue, false, cancellationToken);

    async Task<OperationResult> WritePointAsync(long pointId, JsonElement rawValue, bool suppressForwarding, CancellationToken cancellationToken)
    {
        await WaitReadyIfRunningAsync(cancellationToken);
        var point = db.Points.Where(x => x.Id == pointId).First() ?? throw new KeyNotFoundException("点位不存在");
        var channel = db.Channels.Where(x => x.Id == point.ChannelId).First() ?? throw new KeyNotFoundException("通道不存在");
        if (!point.Writable) return OperationResult.CreateFailedResult("点位只读");
        if (!Enum.TryParse<DataTypeEnums>(point.DataType, true, out var dataType)) return OperationResult.CreateFailedResult("不支持的数据类型");
        var value = ConvertValue(rawValue, point.DataType);
        value = ApplyTransform(value, point.ValueTransformJson, true);
        var result = await WriteAsync(channel, dataType, point.Address, value, cancellationToken);
        if (!result.IsSuccess) { point.Quality = "Bad"; db.Db.Update<ChannelPoint>().SetSource(point).ExecuteAffrows(); LogFailure("point-write", "Write", null, point.ChannelId, point.Id, result.Message); return result; }
        var visibleValue = ApplyTransform(value, point.ValueTransformJson, false);
        point.ValueJson = JsonSerializer.Serialize(visibleValue); point.Quality = "Good"; point.LastUpdatedAt = DateTime.UtcNow;
        db.Db.Update<ChannelPoint>().SetSource(point).ExecuteAffrows();
        PointDataUpdated?.Invoke(this, new PointDataUpdatedEventArgs(point.Id, visibleValue, point.Quality, DateTimeOffset.UtcNow));
        history.Sample(point.Id, visibleValue, point.Quality, DateTimeOffset.UtcNow);
        history.Collection("Information", suppressForwarding ? "ForwardWrite" : "Write", "Point write succeeded", null, point.ChannelId, point.Id);
        if (!suppressForwarding)
            await ForwardAsync(point, changed: true, cancellationToken);
        return result;
    }

    async Task ForwardAsync(ChannelPoint source, bool changed, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var rules = db.Forwardings.Where(x => x.Enabled && (x.SourcePointId == source.Id || x.Mode == ForwardingMode.Bidirectional && x.TargetPointId == source.Id)).ToList();
        foreach (var rule in rules)
        {
            var reverse = rule.SourcePointId != source.Id;
            if (rule.Mode != ForwardingMode.Timed && !changed) continue;
            if (rule.Mode == ForwardingMode.Timed && rule.IntervalMs > 0 && nextForward.TryGetValue(rule.Id, out var next) && next > now) continue;
            nextForward[rule.Id] = now.AddMilliseconds(Math.Max(100, rule.IntervalMs));
            var targetId = reverse ? rule.SourcePointId : rule.TargetPointId;
            var target = db.Points.Where(x => x.Id == targetId).First();
            if (target is null || !target.Writable || source.ValueJson is null) continue;
            using var document = JsonDocument.Parse(source.ValueJson);
            suppressedValues[target.Id] = source.ValueJson;
            await forwardingLock.WaitAsync(cancellationToken);
            try
            {
                for (var attempt = 0; attempt < Math.Max(1, rule.RetryCount); attempt++)
                {
                    var result = await WritePointAsync(target.Id, document.RootElement, true, cancellationToken);
                    if (result.IsSuccess) { history.Collection("Information", "Forward", $"Rule {rule.Id} succeeded", null, source.ChannelId, source.Id); break; }
                    if (attempt + 1 == rule.RetryCount) LogFailure("forwarding", "Forward", null, null, source.Id, $"Rule {rule.Id}: {result.Message}");
                }
            }
            finally { forwardingLock.Release(); }
        }
    }

    readonly ConcurrentDictionary<long, DateTimeOffset> nextForward = new();

    void LogFailure(string key, string operation, Exception? ex, long? channelId = null, long? pointId = null, string? message = null)
    {
        var now = DateTimeOffset.UtcNow;
        if (nextLog.TryGetValue(key + ":" + (pointId ?? channelId ?? 0), out var next) && next > now) return;
        nextLog[key + ":" + (pointId ?? channelId ?? 0)] = now.AddMinutes(1);
        log.LogWarning(ex, "{Operation} failed for channel {ChannelId}, point {PointId}: {Message}", operation, channelId, pointId, message ?? ex?.Message);
        history.Collection("Warning", operation, message ?? ex?.Message ?? "Operation failed", ex, channelId, pointId);
    }

    async Task RunTimedForwardingsAsync(CancellationToken cancellationToken)
    {
        foreach (var rule in db.Forwardings.Where(x => x.Enabled && x.Mode == ForwardingMode.Timed).ToList())
        {
            var now = DateTimeOffset.UtcNow;
            if (nextForward.TryGetValue(rule.Id, out var next) && next > now) continue;
            var source = db.Points.Where(x => x.Id == rule.SourcePointId && x.Enabled).First();
            if (source?.ValueJson is null) continue;
            await ForwardAsync(source, false, cancellationToken);
        }
    }

    async Task SyncServerAsync(Channel channel, CancellationToken cancellationToken)
    {
        await RemoveServerAsync(channel.Id);
        var server = new ServerHost(RuntimeId(channel.Id), BuildServer(channel));
        var result = await server.StartAsync(cancellationToken);
        if (!result.IsSuccess)
        {
            await server.DisposeAsync();
            throw new InvalidOperationException(result.Message);
        }
        servers[channel.Id] = server;
    }

    async Task RemoveServerAsync(long channelId)
    {
        if (servers.TryRemove(channelId, out var server)) await server.DisposeAsync();
    }

    async ValueTask<OperationResult<object?>> ReadAsync(Channel channel, DataTypeEnums dataType, string address, CancellationToken cancellationToken)
    {
        if (servers.TryGetValue(channel.Id, out var server)) return await server.Server.ReadAsync(dataType, address, cancellationToken);
        return await channels.ExecuteAsync(RuntimeId(channel.Id), (client, token) => ((DeviceDataReaderWriterBase)client).ReadAsync(dataType, address, token), cancellationToken);
    }

    async Task<OperationResult> WriteAsync(Channel channel, DataTypeEnums dataType, string address, object? value, CancellationToken cancellationToken)
    {
        if (servers.TryGetValue(channel.Id, out var server)) return await server.Server.WriteAsync(dataType, address, value!, cancellationToken);
        return await channels.ExecuteAsync<OperationResult>(RuntimeId(channel.Id), (client, token) => new ValueTask<OperationResult>(((DeviceDataReaderWriterBase)client).WriteAsync(dataType, address, value!, token)), cancellationToken);
    }

    static string RuntimeId(long id) => $"gateway:{id}";

    static ChannelOptions BuildOptions(Channel channel)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(channel.ConfigurationJson) ? "{}" : channel.ConfigurationJson);
        var c = doc.RootElement;
        string S(string name, string fallback = "") => c.TryGetProperty(name, out var p) ? p.ToString() : fallback;
        int I(string name, int fallback) => c.TryGetProperty(name, out var p) && p.TryGetInt32(out var value) ? value : fallback;
        ChannelOptions options = channel.Protocol.ToLowerInvariant() switch
        {
            "modbustcp" or "modbus-tcp" => new ModbusTcpChannelOptions(RuntimeId(channel.Id), S("host", "127.0.0.1"), I("port", 502)),
            "modbusrtu" or "modbus-rtu" => new ModbusRtuChannelOptions(RuntimeId(channel.Id), S("portName", "COM1")) { BaudRate = I("baudRate", 9600), DataBits = I("dataBits", 8), Parity = Enum.TryParse<Parity>(S("parity", "None"), true, out var parity) ? parity : Parity.None, StopBits = Enum.TryParse<StopBits>(S("stopBits", "One"), true, out var stopBits) ? stopBits : StopBits.One, Handshake = Enum.TryParse<Handshake>(S("handshake", "None"), true, out var handshake) ? handshake : Handshake.None },
            "s7" or "siemenss7" => new SiemensS7ChannelOptions(RuntimeId(channel.Id), S("host", "127.0.0.1"), Enum.TryParse<SiemensVersion>(S("version", "S7_1200"), true, out var version) ? version : SiemensVersion.S7_1200, I("port", 102)) { Rack = (byte)I("rack", 0), Slot = (byte)I("slot", 0) },
            "finstcp" or "fins-tcp" or "fins" => new FinsTcpChannelOptions(RuntimeId(channel.Id), S("host", "127.0.0.1"), I("port", 9600)),
            _ => throw new InvalidOperationException($"不支持的协议: {channel.Protocol}")
        };
        options.MaxConcurrency = Math.Max(1, channel.MaxConcurrency);
        options.ConnectTimeout = TimeSpan.FromMilliseconds(Math.Max(100, channel.ConnectTimeoutMs));
        options.OperationTimeout = TimeSpan.FromMilliseconds(Math.Max(100, channel.OperationTimeoutMs));
        return options;
    }

    static IDeviceServer BuildServer(Channel channel)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(channel.ConfigurationJson) ? "{}" : channel.ConfigurationJson);
        var c = doc.RootElement;
        string S(string name, string fallback = "") => c.TryGetProperty(name, out var p) ? p.ToString() : fallback;
        int I(string name, int fallback) => c.TryGetProperty(name, out var p) && p.TryGetInt32(out var value) ? value : fallback;
        IDeviceServer server = channel.Protocol.ToLowerInvariant() switch
        {
            "modbustcp" or "modbus-tcp" => new ModbusTcpServer(S("host", "0.0.0.0"), I("port", 502)),
            "modbusrtu" or "modbus-rtu" => new ModbusRtuServer(S("portName", "COM1"), I("baudRate", 9600), I("dataBits", 8), Enum.TryParse<StopBits>(S("stopBits", "One"), true, out var stopBits) ? stopBits : StopBits.One, Enum.TryParse<Parity>(S("parity", "None"), true, out var parity) ? parity : Parity.None, Enum.TryParse<Handshake>(S("handshake", "None"), true, out var handshake) ? handshake : Handshake.None),
            "s7" or "siemenss7" => new S7TcpServer(S("host", "0.0.0.0"), I("port", 102)),
            _ => throw new InvalidOperationException($"不支持的服务端协议: {channel.Protocol}")
        };
        server.ConnectTimeout = TimeSpan.FromMilliseconds(Math.Max(100, channel.ConnectTimeoutMs));
        server.ReceiveTimeout = server.SendTimeout = TimeSpan.FromMilliseconds(Math.Max(100, channel.OperationTimeoutMs));
        return server;
    }

    static object? ConvertValue(JsonElement value, string dataType) => dataType.ToLowerInvariant() switch
    {
        "boolean" or "bool" => value.GetBoolean(), "byte" => value.GetByte(), "int16" or "short" => value.GetInt16(), "uint16" or "ushort" => value.GetUInt16(),
        "int32" or "int" => value.GetInt32(), "uint32" or "uint" => value.GetUInt32(), "int64" or "long" => value.GetInt64(), "uint64" or "ulong" => value.GetUInt64(),
        "single" or "float" => value.GetSingle(), "double" => value.GetDouble(), "string" => value.GetString() ?? "", _ => value.ToString()
    };

    static object? ApplyTransform(object? value, string? transformJson, bool inverse)
    {
        if (value is null || string.IsNullOrWhiteSpace(transformJson) || value is not IConvertible) return value;
        try
        {
            using var doc = JsonDocument.Parse(transformJson); var root = doc.RootElement;
            var scale = root.TryGetProperty("scale", out var s) ? s.GetDouble() : 1d;
            var offset = root.TryGetProperty("offset", out var o) ? o.GetDouble() : 0d;
            var number = Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
            var transformed = inverse ? (number - offset) / scale : number * scale + offset;
            return value switch { float => (float)transformed, double => transformed, decimal => (decimal)transformed, int => (int)transformed, long => (long)transformed, uint => (uint)transformed, short => (short)transformed, ushort => (ushort)transformed, _ => transformed };
        }
        catch { return value; }
    }

    async Task WaitReadyIfRunningAsync(CancellationToken cancellationToken)
    {
        if (!ready.Task.IsCompleted) await WaitReadyAsync(cancellationToken);
    }
}

public sealed class ChannelRequest
{
    public string Name { get; set; } = "";
    public string Protocol { get; set; } = "ModbusTcp";
    public string Role { get; set; } = "Client";
    public bool Enabled { get; set; } = true;
    public JsonElement Configuration { get; set; }
    public int MaxConcurrency { get; set; } = 1;
    public int ConnectTimeoutMs { get; set; } = 3000;
    public int OperationTimeoutMs { get; set; } = 3000;
}

public sealed class PointRequest
{
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string DataType { get; set; } = "UInt16";
    public bool Writable { get; set; }
    public bool Enabled { get; set; } = true;
    public int ScanIntervalMs { get; set; } = 1000;
    public string? ValueTransformJson { get; set; }
}

public sealed class ForwardingRequest
{
    public long SourcePointId { get; set; }
    public long TargetPointId { get; set; }
    public ForwardingMode Mode { get; set; } = ForwardingMode.OnChange;
    public int IntervalMs { get; set; } = 1000;
    public int RetryCount { get; set; } = 3;
    public bool Enabled { get; set; } = true;
}
