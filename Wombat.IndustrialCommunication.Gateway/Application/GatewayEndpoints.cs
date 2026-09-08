using System.Text.Json;
using Wombat.IndustrialCommunication.Gateway.Domain;
using Wombat.IndustrialCommunication.Gateway.Infrastructure;

namespace Wombat.IndustrialCommunication.Gateway.Application;

public static class GatewayEndpoints
{
    public static void MapGatewayEndpoints(this WebApplication app)
    {
        app.MapGet("/api/channels", (GatewayDb db) => Results.Ok(db.Channels.ToList())).RequireAuthorization("permission:channel:view");
        app.MapGet("/api/channels/{id:long}", (long id, GatewayDb db, GatewayRuntimeService runtime) =>
            db.Channels.Where(x => x.Id == id).First() is { } channel ? Results.Ok(new { channel, Snapshot = runtime.GetSnapshotAsync(id).GetAwaiter().GetResult() }) : Results.NotFound()).RequireAuthorization("permission:channel:view");
        app.MapPost("/api/channels", async (ChannelRequest request, GatewayDb db, GatewayRuntimeService runtime, HistoryLogService history, CancellationToken ct) =>
        {
            var channel = ToChannel(request);
            channel.Id = db.Db.Insert(channel).ExecuteIdentity();
            try { await runtime.SyncChannelAsync(channel, ct); history.Operation(null, "Create", "Channel", channel.Id, channel.Name); return Results.Created($"/api/channels/{channel.Id}", channel); }
            catch (Exception ex) { return Results.Problem(ex.Message); }
        }).RequireAuthorization("permission:channel:create");
        app.MapPut("/api/channels/{id:long}", async (long id, ChannelRequest request, GatewayDb db, GatewayRuntimeService runtime, HistoryLogService history, CancellationToken ct) =>
        {
            var channel = db.Channels.Where(x => x.Id == id).First();
            if (channel is null) return Results.NotFound();
            Apply(channel, request); db.Db.Update<Channel>().SetSource(channel).ExecuteAffrows();
            try { await runtime.SyncChannelAsync(channel, ct); history.Operation(null, "Update", "Channel", channel.Id, channel.Name); return Results.Ok(channel); }
            catch (Exception ex) { return Results.Problem(ex.Message); }
        }).RequireAuthorization("permission:channel:update");
        app.MapDelete("/api/channels/{id:long}", async (long id, GatewayDb db, GatewayRuntimeService runtime, HistoryLogService history) =>
        {
            if (db.Channels.Where(x => x.Id == id).First() is null) return Results.NotFound();
            var pointIds = db.Points.Where(x => x.ChannelId == id).ToList().Select(x => x.Id).ToArray();
            var forwardingIds = db.Forwardings.Where(x => pointIds.Contains(x.SourcePointId) || pointIds.Contains(x.TargetPointId)).ToList().Select(x => x.Id).ToArray();
            foreach (var forwardingId in forwardingIds) runtime.RemoveForwarding(forwardingId);
            foreach (var pointId in pointIds) db.Db.Delete<ChannelForwarding>().Where(x => x.SourcePointId == pointId || x.TargetPointId == pointId).ExecuteAffrows();
            db.Db.Delete<ChannelPoint>().Where(x => x.ChannelId == id).ExecuteAffrows();
            db.Db.Delete<Channel>().Where(x => x.Id == id).ExecuteAffrows();
            foreach (var pointId in pointIds) runtime.RemovePoint(pointId);
            await runtime.RemoveChannelAsync(id); history.Operation(null, "Delete", "Channel", id, "Channel deleted"); return Results.NoContent();
        }).RequireAuthorization("permission:channel:delete");
        app.MapPost("/api/channels/{id:long}/start", async (long id, GatewayDb db, GatewayRuntimeService runtime, CancellationToken ct) =>
        {
            var channel = db.Channels.Where(x => x.Id == id).First(); if (channel is null) return Results.NotFound();
            channel.Enabled = true; db.Db.Update<Channel>().SetSource(channel).ExecuteAffrows(); await runtime.SyncChannelAsync(channel, ct); return Results.Ok();
        }).RequireAuthorization("permission:channel:operate");
        app.MapPost("/api/channels/{id:long}/stop", async (long id, GatewayDb db, GatewayRuntimeService runtime) =>
        {
            var channel = db.Channels.Where(x => x.Id == id).First();
            if (channel is null) return Results.NotFound();
            channel.Enabled = false; db.Db.Update<Channel>().SetSource(channel).ExecuteAffrows();
            await runtime.StopChannelAsync(id); return Results.Ok();
        }).RequireAuthorization("permission:channel:operate");
        app.MapGet("/api/channels/{id:long}/snapshot", async (long id, GatewayRuntimeService runtime) => Results.Ok(await runtime.GetSnapshotAsync(id))).RequireAuthorization("permission:channel:view");

        app.MapGet("/api/points", (long? channelId, GatewayDb db) => Results.Ok((channelId is { } id ? db.Points.Where(x => x.ChannelId == id) : db.Points).ToList())).RequireAuthorization("permission:channel:view");
        app.MapGet("/api/points/{id:long}", (long id, GatewayDb db) => db.Points.Where(x => x.Id == id).First() is { } point ? Results.Ok(point) : Results.NotFound()).RequireAuthorization("permission:channel:view");
        app.MapPost("/api/channels/{channelId:long}/points", (long channelId, PointRequest request, GatewayDb db, HistoryLogService history) =>
        {
            if (db.Channels.Where(x => x.Id == channelId).First() is null) return Results.NotFound();
            var point = ToPoint(channelId, request); point.Id = db.Db.Insert(point).ExecuteIdentity(); history.Operation(null, "Create", "ChannelPoint", point.Id, point.Name); return Results.Created($"/api/points/{point.Id}", point);
        }).RequireAuthorization("permission:channel:create");
        app.MapPut("/api/points/{id:long}", (long id, PointRequest request, GatewayDb db, GatewayRuntimeService runtime, HistoryLogService history) =>
        {
            var point = db.Points.Where(x => x.Id == id).First(); if (point is null) return Results.NotFound(); Apply(point, request); db.Db.Update<ChannelPoint>().SetSource(point).ExecuteAffrows(); runtime.RemovePoint(id); history.Operation(null, "Update", "ChannelPoint", id, point.Name); return Results.Ok(point);
        }).RequireAuthorization("permission:channel:update");
        app.MapDelete("/api/points/{id:long}", (long id, GatewayDb db, GatewayRuntimeService runtime, HistoryLogService history) =>
        {
            if (db.Points.Where(x => x.Id == id).First() is null) return Results.NotFound();
            var forwardingIds = db.Forwardings.Where(x => x.SourcePointId == id || x.TargetPointId == id).ToList().Select(x => x.Id).ToArray();
            foreach (var forwardingId in forwardingIds) runtime.RemoveForwarding(forwardingId);
            db.Db.Delete<ChannelForwarding>().Where(x => x.SourcePointId == id || x.TargetPointId == id).ExecuteAffrows(); db.Db.Delete<ChannelPoint>().Where(x => x.Id == id).ExecuteAffrows(); runtime.RemovePoint(id); history.Operation(null, "Delete", "ChannelPoint", id, "Point deleted"); return Results.NoContent();
        }).RequireAuthorization("permission:channel:delete");
        app.MapPost("/api/points/{id:long}/read", async (long id, GatewayRuntimeService runtime, CancellationToken ct) => Results.Ok(await runtime.ReadPointAsync(id, ct))).RequireAuthorization("permission:channel:view");
        app.MapPost("/api/points/{id:long}/write", async (long id, PointWriteRequest request, GatewayRuntimeService runtime, CancellationToken ct) => Results.Ok(await runtime.WritePointAsync(id, request.Value, ct))).RequireAuthorization("permission:channel:operate");
        app.MapPost("/api/points/read", async (PointBatchReadRequest request, GatewayRuntimeService runtime, CancellationToken ct) => Results.Ok(await Task.WhenAll(request.PointIds.Distinct().Select(async id => new { Id = id, Result = await runtime.ReadPointAsync(id, ct) })))).RequireAuthorization("permission:channel:view");
        app.MapPost("/api/points/write", async (PointBatchWriteRequest request, GatewayRuntimeService runtime, CancellationToken ct) => Results.Ok(await Task.WhenAll(request.Values.Select(async x => new { Id = x.Key, Result = await runtime.WritePointAsync(x.Key, x.Value, ct) })))).RequireAuthorization("permission:channel:operate");

        app.MapGet("/api/forwardings", (GatewayDb db) => Results.Ok(db.Forwardings.ToList())).RequireAuthorization("permission:channel:view");
        app.MapPost("/api/forwardings", (ForwardingRequest request, GatewayDb db, HistoryLogService history) => { var rule = ToForwarding(request); rule.Id = db.Db.Insert(rule).ExecuteIdentity(); history.Operation(null, "Create", "ChannelForwarding", rule.Id, "Forwarding created"); return Results.Created($"/api/forwardings/{rule.Id}", rule); }).RequireAuthorization("permission:channel:create");
        app.MapPut("/api/forwardings/{id:long}", (long id, ForwardingRequest request, GatewayDb db, GatewayRuntimeService runtime, HistoryLogService history) => { var rule = db.Forwardings.Where(x => x.Id == id).First(); if (rule is null) return Results.NotFound(); Apply(rule, request); db.Db.Update<ChannelForwarding>().SetSource(rule).ExecuteAffrows(); runtime.RemoveForwarding(id); history.Operation(null, "Update", "ChannelForwarding", id, "Forwarding updated"); return Results.Ok(rule); }).RequireAuthorization("permission:channel:update");
        app.MapDelete("/api/forwardings/{id:long}", (long id, GatewayDb db, GatewayRuntimeService runtime) => { if (db.Forwardings.Where(x => x.Id == id).First() is null) return Results.NotFound(); db.Db.Delete<ChannelForwarding>().Where(x => x.Id == id).ExecuteAffrows(); runtime.RemoveForwarding(id); return Results.NoContent(); }).RequireAuthorization("permission:channel:delete");

        app.MapGet("/api/history/policies", (long? pointId, GatewayDb db) => Results.Ok((pointId is { } id ? db.HistoryPolicies.Where(x => x.ChannelPointId == id) : db.HistoryPolicies).ToList())).RequireAuthorization("permission:history:view");
        app.MapPut("/api/history/policies/{pointId:long}", (long pointId, HistoryPolicyRequest r, GatewayDb db, HistoryLogService history) =>
        {
            if (db.Points.Where(x => x.Id == pointId).First() is null) return Results.NotFound();
            var p = db.HistoryPolicies.Where(x => x.ChannelPointId == pointId).First() ?? new HistoryPolicy { ChannelPointId = pointId };
            p.Enabled = r.Enabled; p.AggregateIntervalSeconds = r.AggregateIntervalSeconds; p.EffectiveFrom = r.EffectiveFrom; p.EffectiveTo = r.EffectiveTo; p.RawRetentionDays = r.RawRetentionDays; p.AggregateRetentionDays = r.AggregateRetentionDays;
            if (p.Id == 0) p.Id = db.Db.Insert(p).ExecuteIdentity(); else db.Db.Update<HistoryPolicy>().SetSource(p).ExecuteAffrows();
            history.Operation(null, "HistoryPolicyUpdate", "ChannelPoint", pointId, "History policy updated");
            return Results.Ok(p);
        }).RequireAuthorization("permission:history:update");
        app.MapGet("/api/history/samples", (long pointId, DateTime? from, DateTime? to, int? limit, GatewayDb db) => Results.Ok(db.HistorySamples.Where(x => x.ChannelPointId == pointId && (from == null || x.SampledAt >= from) && (to == null || x.SampledAt <= to)).OrderByDescending(x => x.SampledAt).Take(Math.Clamp(limit ?? 1000, 1, 10000)).ToList())).RequireAuthorization("permission:history:view");
        app.MapGet("/api/history/aggregates", (long pointId, DateTime? from, DateTime? to, int? limit, GatewayDb db) => Results.Ok(db.HistoryAggregates.Where(x => x.ChannelPointId == pointId && (from == null || x.BucketStart >= from) && (to == null || x.BucketStart <= to)).OrderByDescending(x => x.BucketStart).Take(Math.Clamp(limit ?? 1000, 1, 10000)).ToList())).RequireAuthorization("permission:history:view");
        app.MapGet("/api/logs/collection", (long? channelId, long? pointId, string? level, string? operationType, DateTime? from, DateTime? to, int? limit, GatewayDb db) => Results.Ok(db.CollectionLogs.Where(x => (channelId == null || x.ChannelId == channelId) && (pointId == null || x.ChannelPointId == pointId) && (level == null || x.Level == level) && (operationType == null || x.OperationType == operationType) && (from == null || x.OccurredAt >= from) && (to == null || x.OccurredAt <= to)).OrderByDescending(x => x.OccurredAt).Take(Math.Clamp(limit ?? 1000, 1, 10000)).ToList())).RequireAuthorization("permission:log:view");
        app.MapGet("/api/logs/operation", (string? operationType, long? userId, DateTime? from, DateTime? to, int? limit, GatewayDb db) => Results.Ok(db.OperationLogs.Where(x => (operationType == null || x.OperationType == operationType) && (userId == null || x.UserId == userId) && (from == null || x.OccurredAt >= from) && (to == null || x.OccurredAt <= to)).OrderByDescending(x => x.OccurredAt).Take(Math.Clamp(limit ?? 1000, 1, 10000)).ToList())).RequireAuthorization("permission:log:view");
        app.MapGet("/api/logs/login", (string? userName, bool? success, DateTime? from, DateTime? to, int? limit, GatewayDb db) => Results.Ok(db.LoginLogs.Where(x => (userName == null || x.UserName == userName) && (success == null || x.Success == success) && (from == null || x.OccurredAt >= from) && (to == null || x.OccurredAt <= to)).OrderByDescending(x => x.OccurredAt).Take(Math.Clamp(limit ?? 1000, 1, 10000)).ToList())).RequireAuthorization("permission:log:view");
        app.MapGet("/api/logs/collection/export", (GatewayDb db) => Results.Text("OccurredAt,Level,OperationType,Message\n" + string.Join("\n", db.CollectionLogs.ToList().Select(x => $"{x.OccurredAt:O},{x.Level},{x.OperationType},\"{x.Message.Replace("\"", "\"\"")}\"")), "text/csv")).RequireAuthorization("permission:log:view");
        app.MapDelete("/api/logs", (DateTime? before, GatewayDb db) => { var end = before ?? DateTime.UtcNow; db.Db.Delete<CollectionLog>().Where(x => x.OccurredAt < end).ExecuteAffrows(); db.Db.Delete<OperationLog>().Where(x => x.OccurredAt < end).ExecuteAffrows(); db.Db.Delete<LoginLog>().Where(x => x.OccurredAt < end).ExecuteAffrows(); return Results.NoContent(); }).RequireAuthorization("permission:log:delete");
    }

    static Channel ToChannel(ChannelRequest r) { var c = new Channel(); Apply(c, r); return c; }
    static void Apply(Channel c, ChannelRequest r) { c.Name = r.Name; c.Protocol = r.Protocol; c.Role = r.Role; c.Enabled = r.Enabled; c.ConfigurationJson = r.Configuration.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null ? "{}" : r.Configuration.GetRawText(); c.MaxConcurrency = r.MaxConcurrency; c.ConnectTimeoutMs = r.ConnectTimeoutMs; c.OperationTimeoutMs = r.OperationTimeoutMs; }
    static ChannelPoint ToPoint(long channelId, PointRequest r) { var p = new ChannelPoint { ChannelId = channelId }; Apply(p, r); return p; }
    static void Apply(ChannelPoint p, PointRequest r) { p.Name = r.Name; p.Address = r.Address; p.DataType = r.DataType; p.Writable = r.Writable; p.Enabled = r.Enabled; p.ScanIntervalMs = r.ScanIntervalMs; p.ValueTransformJson = r.ValueTransformJson; }
    static ChannelForwarding ToForwarding(ForwardingRequest r) { var f = new ChannelForwarding(); Apply(f, r); return f; }
    static void Apply(ChannelForwarding f, ForwardingRequest r) { f.SourcePointId = r.SourcePointId; f.TargetPointId = r.TargetPointId; f.Mode = r.Mode; f.IntervalMs = r.IntervalMs; f.RetryCount = r.RetryCount; f.Enabled = r.Enabled; }
}

public sealed class PointWriteRequest { public JsonElement Value { get; set; } }
public sealed class PointBatchReadRequest { public long[] PointIds { get; set; } = []; }
public sealed class PointBatchWriteRequest { public Dictionary<long, JsonElement> Values { get; set; } = []; }
public sealed record HistoryPolicyRequest(bool Enabled = true, int AggregateIntervalSeconds = 60, DateTime? EffectiveFrom = null, DateTime? EffectiveTo = null, int RawRetentionDays = 30, int AggregateRetentionDays = 365);
