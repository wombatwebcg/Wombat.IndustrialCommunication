using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Wombat.IndustrialCommunication.Gateway.Application;

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;
public sealed class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.HasClaim("permission", requirement.Permission)) context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public override Task<AuthorizationPolicy?> GetPolicyAsync(string name) => name.StartsWith("permission:", StringComparison.Ordinal) ? Task.FromResult<AuthorizationPolicy?>(new AuthorizationPolicyBuilder().AddRequirements(new PermissionRequirement(name[11..])).Build()) : base.GetPolicyAsync(name);
}
