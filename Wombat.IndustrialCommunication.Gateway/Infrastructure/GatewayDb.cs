using FreeSql;
using Wombat.IndustrialCommunication.Gateway.Domain;

namespace Wombat.IndustrialCommunication.Gateway.Infrastructure;

public static class GatewayEntityTypes { public static readonly Type[] All = [typeof(User), typeof(Role), typeof(Permission), typeof(UserRole), typeof(RolePermission), typeof(RefreshToken), typeof(Channel), typeof(ChannelPoint), typeof(ChannelForwarding), typeof(HistoryPolicy), typeof(HistorySample), typeof(HistorySampleAggregate), typeof(CollectionLog), typeof(OperationLog), typeof(LoginLog)]; }
public sealed class GatewayDb(IFreeSql db)
{
    public IFreeSql Db { get; } = db;
    public ISelect<User> Users => Db.Select<User>();
    public ISelect<Role> Roles => Db.Select<Role>();
    public ISelect<Permission> Permissions => Db.Select<Permission>();
    public ISelect<Channel> Channels => Db.Select<Channel>();
    public ISelect<ChannelPoint> Points => Db.Select<ChannelPoint>();
    public ISelect<ChannelForwarding> Forwardings => Db.Select<ChannelForwarding>();
    public ISelect<HistoryPolicy> HistoryPolicies => Db.Select<HistoryPolicy>();
    public ISelect<HistorySample> HistorySamples => Db.Select<HistorySample>();
    public ISelect<HistorySampleAggregate> HistoryAggregates => Db.Select<HistorySampleAggregate>();
    public ISelect<CollectionLog> CollectionLogs => Db.Select<CollectionLog>();
    public ISelect<OperationLog> OperationLogs => Db.Select<OperationLog>();
    public ISelect<LoginLog> LoginLogs => Db.Select<LoginLog>();
}
