using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.SharedKernel.Application.Abstractions
{
    public interface IUnitOfWork
    {
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
