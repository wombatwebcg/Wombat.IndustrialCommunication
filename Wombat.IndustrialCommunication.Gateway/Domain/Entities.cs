using FreeSql.DataAnnotations;
namespace Wombat.IndustrialCommunication.Gateway.Domain;

public abstract class Entity { [Column(IsPrimary = true, IsIdentity = true)] public long Id { get; set; } public DateTime CreatedAt { get; set; } = DateTime.UtcNow; }
public sealed class User : Entity { public string UserName { get; set; } = ""; public string DisplayName { get; set; } = ""; public string PasswordHash { get; set; } = ""; public bool IsDisabled { get; set; } public int TokenVersion { get; set; } }
public sealed class Role : Entity { public string Name { get; set; } = ""; public string Description { get; set; } = ""; }
public sealed class Permission : Entity { public string Code { get; set; } = ""; public string Description { get; set; } = ""; }
public sealed class UserRole : Entity { public long UserId { get; set; } public long RoleId { get; set; } }
public sealed class RolePermission : Entity { public long RoleId { get; set; } public long PermissionId { get; set; } }
public sealed class RefreshToken : Entity { public long UserId { get; set; } public string TokenHash { get; set; } = ""; public DateTime ExpiresAt { get; set; } public DateTime? RevokedAt { get; set; } }

public enum ForwardingMode { Timed, OnChange, Bidirectional }

public sealed class Channel : Entity
{
    public string Name { get; set; } = "";
    public string Protocol { get; set; } = "ModbusTcp";
    public string Role { get; set; } = "Client";
    public bool Enabled { get; set; } = true;
    public string ConfigurationJson { get; set; } = "{}";
    public int MaxConcurrency { get; set; } = 1;
    public int ConnectTimeoutMs { get; set; } = 3000;
    public int OperationTimeoutMs { get; set; } = 3000;
}

public sealed class ChannelPoint : Entity
{
    public long ChannelId { get; set; }
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string DataType { get; set; } = "UInt16";
    public bool Writable { get; set; }
    public bool Enabled { get; set; } = true;
    public int ScanIntervalMs { get; set; } = 1000;
    public string? ValueTransformJson { get; set; }
    public string? ValueJson { get; set; }
    public string Quality { get; set; } = "Unknown";
    public DateTime? LastUpdatedAt { get; set; }
}

public sealed class ChannelForwarding : Entity
{
    public long SourcePointId { get; set; }
    public long TargetPointId { get; set; }
    public ForwardingMode Mode { get; set; } = ForwardingMode.OnChange;
    public int IntervalMs { get; set; } = 1000;
    public int RetryCount { get; set; } = 3;
    public bool Enabled { get; set; } = true;
}

public sealed class HistoryPolicy : Entity { public long ChannelPointId { get; set; } public bool Enabled { get; set; } = true; public int AggregateIntervalSeconds { get; set; } = 60; public DateTime? EffectiveFrom { get; set; } public DateTime? EffectiveTo { get; set; } public int RawRetentionDays { get; set; } = 30; public int AggregateRetentionDays { get; set; } = 365; }
public sealed class HistorySample : Entity { public long ChannelPointId { get; set; } public long ChannelId { get; set; } public string ValueJson { get; set; } = "null"; public string Quality { get; set; } = "Unknown"; public string DataType { get; set; } = ""; public DateTime SampledAt { get; set; } = DateTime.UtcNow; }
public sealed class HistorySampleAggregate : Entity { public long ChannelPointId { get; set; } public long ChannelId { get; set; } public DateTime BucketStart { get; set; } public int SampleCount { get; set; } public double? Average { get; set; } public double? Minimum { get; set; } public double? Maximum { get; set; } public string Quality { get; set; } = "Unknown"; }
public sealed class CollectionLog : Entity { public long? ChannelId { get; set; } public long? ChannelPointId { get; set; } public string Level { get; set; } = "Information"; public string OperationType { get; set; } = ""; public string Message { get; set; } = ""; public string? Exception { get; set; } public DateTime OccurredAt { get; set; } = DateTime.UtcNow; }
public sealed class OperationLog : Entity { public long? UserId { get; set; } public string OperationType { get; set; } = ""; public string TargetType { get; set; } = ""; public long? TargetId { get; set; } public string Message { get; set; } = ""; public DateTime OccurredAt { get; set; } = DateTime.UtcNow; }
public sealed class LoginLog : Entity { public long? UserId { get; set; } public string UserName { get; set; } = ""; public bool Success { get; set; } public string? RemoteIp { get; set; } public string? Message { get; set; } public DateTime OccurredAt { get; set; } = DateTime.UtcNow; }
