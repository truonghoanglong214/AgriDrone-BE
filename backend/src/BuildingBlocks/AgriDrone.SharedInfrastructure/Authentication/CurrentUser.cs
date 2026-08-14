using AgriDrone.SharedKernel.Application.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.SharedInfrastructure.Authentication
{
    public class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
    {
        private HttpContext? HttpContext =>
        httpContextAccessor.HttpContext;

        public bool IsAuthenticated =>
            HttpContext?.User.Identity?.IsAuthenticated ?? false;

        public Guid? UserId
        {
            get
            {
                string? value = HttpContext?.User
                    .FindFirst(JwtRegisteredClaimNames.Sub)?
                    .Value;

                return Guid.TryParse(value, out Guid userId)
                    ? userId
                    : null;
            }
        }

        public string? Email =>
            HttpContext?.User
                .FindFirst(JwtRegisteredClaimNames.Email)?
                .Value;

        public IReadOnlyCollection<string> Roles =>
            HttpContext?.User
                .FindAll("role")
                .Select(x => x.Value)
                .ToArray()
            ?? Array.Empty<string>();
    }
}
