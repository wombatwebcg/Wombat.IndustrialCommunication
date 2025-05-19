using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace Wombat.IndustrialCommunication.Gateway.Server.Services
{
    public class CustomAuthStateProvider : AuthenticationStateProvider
    {
        private ClaimsPrincipal _anonymous = new ClaimsPrincipal(new ClaimsIdentity());
        private ClaimsPrincipal? _authenticated;

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            return Task.FromResult(new AuthenticationState(_authenticated ?? _anonymous));
        }

        public void NotifyAuthenticationStateChanged(ClaimsPrincipal? principal)
        {
            _authenticated = principal;
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }
    }
}