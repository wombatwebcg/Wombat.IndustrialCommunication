using FreeSql;
using Wombat.IndustrialCommunication.Gateway.Domain;

namespace Wombat.IndustrialCommunication.Gateway.Application;

public sealed class BootstrapInitializer(IServiceProvider services, IConfiguration config, ILogger<BootstrapInitializer> log) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var db = services.GetRequiredService<Infrastructure.GatewayDb>().Db;
        db.CodeFirst.SyncStructure(Infrastructure.GatewayEntityTypes.All);
        var permissions = new[] { "system:view", "system:operate", "user:view", "user:create", "user:update", "user:delete", "role:view", "role:create", "role:update", "role:delete", "permission:view", "permission:create", "permission:update", "permission:delete", "profile:view", "profile:operate", "channel:view", "channel:create", "channel:update", "channel:delete", "channel:operate", "channel:export", "history:view", "history:update", "log:view", "log:delete", "mqtt:view", "mqtt:create", "mqtt:update", "mqtt:delete", "mqtt:operate" };
        foreach (var code in permissions) if (db.Select<Permission>().Where(x => x.Code == code).First() is null) db.Insert(new Permission { Code = code }).ExecuteAffrows();
        var admin = db.Select<Role>().Where(x => x.Name == "Admin").First() ?? new Role { Name = "Admin", Description = "全部权限" }; if (admin.Id == 0) admin.Id = db.Insert(admin).ExecuteIdentity();
        foreach (var p in db.Select<Permission>().ToList()) if (db.Select<RolePermission>().Where(x => x.RoleId == admin.Id && x.PermissionId == p.Id).First() is null) db.Insert(new RolePermission { RoleId = admin.Id, PermissionId = p.Id }).ExecuteAffrows();
        var operatorRole = db.Select<Role>().Where(x => x.Name == "Operator").First() ?? new Role { Name = "Operator" }; if (operatorRole.Id == 0) db.Insert(operatorRole).ExecuteAffrows();
        var viewer = db.Select<Role>().Where(x => x.Name == "Viewer").First() ?? new Role { Name = "Viewer" }; if (viewer.Id == 0) db.Insert(viewer).ExecuteAffrows();
        if (db.Select<User>().Any() == false && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BOOTSTRAP_ADMIN_PASSWORD"))) { var auth = services.GetRequiredService<AuthService>(); var u = auth.Create(config["BootstrapAdmin:UserName"] ?? "admin", config["BootstrapAdmin:DisplayName"] ?? "Administrator", Environment.GetEnvironmentVariable("BOOTSTRAP_ADMIN_PASSWORD")!); db.Insert(new UserRole { UserId = u.Id, RoleId = admin.Id }).ExecuteAffrows(); log.LogInformation("Bootstrap admin created: {User}", u.UserName); }
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
