using System.Text.Json;
using Wombat.IndustrialCommunication.Gateway.Domain;
using Wombat.IndustrialCommunication.Gateway.Infrastructure;

namespace Wombat.IndustrialCommunication.Gateway.Application;

public sealed class HistoryLogService(GatewayDb db, IConfiguration config, ILogger<HistoryLogService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { while (!stoppingToken.IsCancellationRequested) { Cleanup(); await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); } }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    public void Sample(long pointId, object? value, string quality, DateTimeOffset atUtc)
    {
        try
        {
            var point = db.Points.Where(x => x.Id == pointId).First();
            var policy = db.HistoryPolicies.Where(x => x.ChannelPointId == pointId && x.Enabled).First();
            if (point is null || policy is null) return;
            var now = atUtc.UtcDateTime;
            if (policy.EffectiveFrom is { } from && now < from || policy.EffectiveTo is { } to && now > to) return;
            db.Db.Insert(new HistorySample { ChannelPointId = point.Id, ChannelId = point.ChannelId, ValueJson = JsonSerializer.Serialize(value), Quality = quality, DataType = point.DataType, SampledAt = now }).ExecuteAffrows();
            if (policy.AggregateIntervalSeconds > 0 && double.TryParse(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture), out var number))
            {
                var seconds = policy.AggregateIntervalSeconds; var ticks = now.Ticks - now.Ticks % TimeSpan.FromSeconds(seconds).Ticks;
                var bucket = new DateTime(ticks, DateTimeKind.Utc);
                var a = db.HistoryAggregates.Where(x => x.ChannelPointId == point.Id && x.BucketStart == bucket).First();
                if (a is null) db.Db.Insert(new HistorySampleAggregate { ChannelPointId = point.Id, ChannelId = point.ChannelId, BucketStart = bucket, SampleCount = 1, Average = number, Minimum = number, Maximum = number, Quality = quality }).ExecuteAffrows();
                else { a.SampleCount++; a.Average = ((a.Average ?? 0) * (a.SampleCount - 1) + number) / a.SampleCount; a.Minimum = Math.Min(a.Minimum ?? number, number); a.Maximum = Math.Max(a.Maximum ?? number, number); a.Quality = quality; db.Db.Update<HistorySampleAggregate>().SetSource(a).ExecuteAffrows(); }
            }
        }
        catch (Exception ex) { log.LogError(ex, "History sample failed for point {PointId}", pointId); }
    }

    void Cleanup()
    {
        var now = DateTime.UtcNow;
        foreach (var p in db.HistoryPolicies.ToList())
        {
            if (p.RawRetentionDays > 0) db.Db.Delete<HistorySample>().Where(x => x.ChannelPointId == p.ChannelPointId && x.SampledAt < now.AddDays(-p.RawRetentionDays)).ExecuteAffrows();
            if (p.AggregateRetentionDays > 0) db.Db.Delete<HistorySampleAggregate>().Where(x => x.ChannelPointId == p.ChannelPointId && x.BucketStart < now.AddDays(-p.AggregateRetentionDays)).ExecuteAffrows();
        }
        var logDays = config.GetValue("History:LogRetentionDays", 90);
        db.Db.Delete<CollectionLog>().Where(x => x.OccurredAt < now.AddDays(-logDays)).ExecuteAffrows();
        db.Db.Delete<OperationLog>().Where(x => x.OccurredAt < now.AddDays(-logDays)).ExecuteAffrows();
        db.Db.Delete<LoginLog>().Where(x => x.OccurredAt < now.AddDays(-logDays)).ExecuteAffrows();
    }

    public void Collection(string level, string type, string message, Exception? ex = null, long? channelId = null, long? pointId = null) => db.Db.Insert(new CollectionLog { Level = level, OperationType = type, Message = message, Exception = ex?.ToString(), ChannelId = channelId, ChannelPointId = pointId }).ExecuteAffrows();
    public void Operation(long? userId, string type, string target, long? targetId, string message) => db.Db.Insert(new OperationLog { UserId = userId, OperationType = type, TargetType = target, TargetId = targetId, Message = message }).ExecuteAffrows();
    public void Login(string userName, bool success, string? ip, long? userId = null, string? message = null) => db.Db.Insert(new LoginLog { UserName = userName, Success = success, RemoteIp = ip, UserId = userId, Message = message }).ExecuteAffrows();
}
