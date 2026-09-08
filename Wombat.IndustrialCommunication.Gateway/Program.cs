using System.Text;
using System.Data.Common;
using FreeSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Wombat.IndustrialCommunication.Gateway.Application;
using Wombat.IndustrialCommunication.Gateway.Infrastructure;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = AppContext.BaseDirectory });
var dbConfig = builder.Configuration.GetSection("SqlConfig:FreeSqlCollections").Get<List<DbConfig>>()?.Single(x => x.Key == "GatewayDb") ?? throw new InvalidOperationException("未找到 GatewayDb 数据库配置");
var jwtKey = builder.Configuration["Auth:JwtKey"] ?? throw new InvalidOperationException("必须通过配置或用户机密提供 Auth:JwtKey");
if ((DataType)dbConfig.DataType == DataType.Sqlite)
{
    var sqlite = new DbConnectionStringBuilder { ConnectionString = dbConfig.MasterConnetion };
    if (sqlite.TryGetValue("Data Source", out var source) && source is string path && !Path.IsPathRooted(path))
    {
        var fullPath = Path.Combine(builder.Environment.ContentRootPath, path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        sqlite["Data Source"] = fullPath;
        dbConfig.MasterConnetion = sqlite.ConnectionString;
    }
}
builder.Services.AddSingleton<IFreeSql>(_ => new FreeSqlBuilder().UseConnectionString((DataType)dbConfig.DataType, dbConfig.MasterConnetion).UseAutoSyncStructure(false).Build());
builder.Services.AddSingleton<GatewayDb>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<HistoryLogService>();
builder.Services.AddHostedService<BootstrapInitializer>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<HistoryLogService>());
builder.Services.AddSingleton<GatewayRuntimeService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<GatewayRuntimeService>());
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => { o.TokenValidationParameters = new TokenValidationParameters { ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true, ValidIssuer = builder.Configuration["Auth:Issuer"], ValidAudience = builder.Configuration["Auth:Audience"], IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)) }; o.Events = new JwtBearerEvents { OnTokenValidated = context => { var store = context.HttpContext.RequestServices.GetRequiredService<GatewayDb>(); var id = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value; var version = context.Principal?.FindFirst("token_version")?.Value; var user = long.TryParse(id, out var userId) ? store.Users.Where(x => x.Id == userId).First() : null; if (user is null || user.IsDisabled || version != user.TokenVersion.ToString()) context.Fail("令牌已失效"); return Task.CompletedTask; } }; });
builder.Services.AddAuthorization();
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, PermissionHandler>();
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider, PermissionPolicyProvider>();
var app = builder.Build();
app.UseAuthentication(); app.UseAuthorization();
app.MapPost("/api/auth/login", (LoginRequest r, HttpContext http, AuthService auth, HistoryLogService history) => { var u = auth.Find(r.UserName); var success = u is not null && !u.IsDisabled && auth.Verify(u, r.Password); history.Login(r.UserName, success, http.Connection.RemoteIpAddress?.ToString(), u?.Id, success ? null : "Invalid credentials"); return !success ? Results.Unauthorized() : Results.Ok(auth.Issue(u!)); });
app.MapPost("/api/auth/register", (RegisterRequest r, AuthService auth, IConfiguration c, HistoryLogService history) => { if (!c.GetValue("Auth:AllowSelfRegistration", false)) return Results.NotFound(); if (auth.Find(r.UserName) is not null) return Results.Conflict(); var user = auth.Create(r.UserName, r.DisplayName, r.Password); history.Operation(user.Id, "Register", "User", user.Id, "Self registration"); return Results.Ok(user); });
app.MapPost("/api/auth/refresh", (RefreshRequest r, AuthService auth) => auth.Refresh(r.RefreshToken) is { } result ? Results.Ok(result) : Results.Unauthorized());
app.MapPost("/api/auth/change-password", (ChangePasswordRequest r, HttpContext http, AuthService auth, HistoryLogService history) => { var u = auth.Find(http.User.Identity?.Name ?? ""); if (u is null || !auth.Verify(u, r.CurrentPassword)) return Results.Unauthorized(); auth.ChangePassword(u, r.NewPassword); history.Operation(u.Id, "ChangePassword", "User", u.Id, "Password changed"); return Results.NoContent(); }).RequireAuthorization("permission:profile:operate");
app.MapGet("/api/users", (GatewayDb db) => Results.Ok(db.Users.ToList())).RequireAuthorization("permission:user:view");
app.MapPost("/api/users", (CreateUserRequest r, AuthService auth, HistoryLogService history) => { if (auth.Find(r.UserName) is not null) return Results.Conflict(); var user = auth.Create(r.UserName, r.DisplayName, r.Password); history.Operation(null, "Create", "User", user.Id, user.UserName); return Results.Ok(user); }).RequireAuthorization("permission:user:create");
app.MapPut("/api/users/{id:long}/roles", (long id, SetRolesRequest r, GatewayDb db, AuthService auth) => { var user = db.Users.Where(x => x.Id == id).First(); if (user is null) return Results.NotFound(); auth.SetRoles(user, r.RoleIds); return Results.NoContent(); }).RequireAuthorization("permission:user:update");
app.MapPut("/api/users/{id:long}/disabled", (long id, SetDisabledRequest r, GatewayDb db, AuthService auth) => { var user = db.Users.Where(x => x.Id == id).First(); if (user is null) return Results.NotFound(); auth.SetDisabled(user, r.IsDisabled); return Results.NoContent(); }).RequireAuthorization("permission:user:update");
app.MapPost("/api/users/{id:long}/reset-password", (long id, ResetPasswordRequest r, GatewayDb db, AuthService auth) => { var user = db.Users.Where(x => x.Id == id).First(); if (user is null) return Results.NotFound(); auth.ChangePassword(user, r.NewPassword); return Results.NoContent(); }).RequireAuthorization("permission:user:update");
app.MapGet("/api/roles", (GatewayDb db) => Results.Ok(db.Roles.ToList())).RequireAuthorization("permission:role:view");
app.MapGet("/api/permissions", (GatewayDb db) => Results.Ok(db.Permissions.ToList())).RequireAuthorization("permission:permission:view");
app.MapGatewayEndpoints();
app.Run();
record LoginRequest(string UserName, string Password); record RegisterRequest(string UserName, string DisplayName, string Password); record RefreshRequest(string RefreshToken); record ChangePasswordRequest(string CurrentPassword, string NewPassword); record CreateUserRequest(string UserName, string DisplayName, string Password); record SetRolesRequest(long[] RoleIds); record SetDisabledRequest(bool IsDisabled); record ResetPasswordRequest(string NewPassword);
record DbConfig { public string Key { get; init; } = ""; public string MasterConnetion { get; set; } = ""; public int DataType { get; init; } }
