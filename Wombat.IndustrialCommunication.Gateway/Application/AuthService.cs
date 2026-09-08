using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FreeSql;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Wombat.IndustrialCommunication.Gateway.Domain;
using Wombat.IndustrialCommunication.Gateway.Infrastructure;

namespace Wombat.IndustrialCommunication.Gateway.Application;

public sealed record TokenResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt);
public sealed class AuthService(GatewayDb store, IConfiguration config)
{
    readonly PasswordHasher<User> hasher = new();
    public User? Find(string name) => store.Users.Where(x => x.UserName == name).First();
    public bool Verify(User user, string password) => hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;
    public TokenResponse Issue(User user)
    {
        var roles = store.Db.Select<UserRole>().Where(x => x.UserId == user.Id).ToList().Join(store.Db.Select<Role>().ToList(), x => x.RoleId, x => x.Id, (_, r) => r.Name).ToArray();
        var permissions = store.Db.Select<UserRole>().Where(x => x.UserId == user.Id).ToList().Join(store.Db.Select<RolePermission>().ToList(), x => x.RoleId, x => x.RoleId, (_, rp) => rp.PermissionId).Join(store.Db.Select<Permission>().ToList(), x => x, x => x.Id, (_, p) => p.Code).Distinct();
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, user.Id.ToString()), new(ClaimTypes.Name, user.UserName), new("token_version", user.TokenVersion.ToString()) };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        var expires = DateTime.UtcNow.AddHours(8); var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Auth:JwtKey"]!));
        var token = new JwtSecurityToken(config["Auth:Issuer"], config["Auth:Audience"], claims, expires: expires, signingCredentials: new(key, SecurityAlgorithms.HmacSha256));
        var refresh = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        store.Db.Insert(new RefreshToken { UserId = user.Id, TokenHash = Hash(refresh), ExpiresAt = DateTime.UtcNow.AddDays(30) }).ExecuteAffrows();
        return new(new JwtSecurityTokenHandler().WriteToken(token), refresh, expires);
    }
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public User Create(string name, string displayName, string password) { var u = new User { UserName = name, DisplayName = displayName }; u.PasswordHash = hasher.HashPassword(u, password); u.Id = store.Db.Insert(u).ExecuteIdentity(); return u; }
    public void ChangePassword(User user, string password) { user.PasswordHash = hasher.HashPassword(user, password); user.TokenVersion++; store.Db.Update<User>().SetSource(user).ExecuteAffrows(); }
    public TokenResponse? Refresh(string token)
    {
        var refresh = store.Db.Select<RefreshToken>().Where(x => x.TokenHash == Hash(token) && x.RevokedAt == null && x.ExpiresAt > DateTime.UtcNow).First();
        if (refresh is null) return null;
        refresh.RevokedAt = DateTime.UtcNow; store.Db.Update<RefreshToken>().SetSource(refresh).ExecuteAffrows();
        var user = store.Users.Where(x => x.Id == refresh.UserId && !x.IsDisabled).First();
        return user is null ? null : Issue(user);
    }
    public void SetRoles(User user, IEnumerable<long> roleIds) { store.Db.Delete<UserRole>().Where(x => x.UserId == user.Id).ExecuteAffrows(); foreach (var roleId in roleIds.Distinct()) store.Db.Insert(new UserRole { UserId = user.Id, RoleId = roleId }).ExecuteAffrows(); user.TokenVersion++; store.Db.Update<User>().SetSource(user).ExecuteAffrows(); }
    public void SetDisabled(User user, bool disabled) { user.IsDisabled = disabled; user.TokenVersion++; store.Db.Update<User>().SetSource(user).ExecuteAffrows(); }
}
