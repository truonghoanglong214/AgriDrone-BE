using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.SharedKernel.Application.Abstractions
{
    public interface ICurrentUser
    {
        Guid? UserId { get; }

        string? Email { get; }

        bool IsAuthenticated { get; }

        IReadOnlyCollection<string> Roles { get; }
    }
}
