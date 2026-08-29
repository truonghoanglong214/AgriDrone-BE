using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Farms.Domain.Farms
{
    internal interface IFarmRepository
    {
        Task<Farm?> GetByIdAsync(
            Guid tenantId,
            Guid farmId,
            CancellationToken cancellationToken = default);

        Task<Farm?> GetByCodeAsync(
            Guid tenantId,
            string code,
            CancellationToken cancellationToken = default);

        Task<bool> ActiveCodeExistsAsync(
            Guid tenantId,
            string code,
            Guid? excludingFarmId = null,
            CancellationToken cancellationToken = default);

        void Add(Farm farm);
        void Update(Farm farm);
    }
}
