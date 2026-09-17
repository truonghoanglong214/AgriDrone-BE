using AgriDrone.SharedKernel.Application.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Harvests.Application.Abstractions.Persistence
{
    internal interface IHarvestsUnitOfWork : IUnitOfWork
    {
        Task<T> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken = default);
    }
}
