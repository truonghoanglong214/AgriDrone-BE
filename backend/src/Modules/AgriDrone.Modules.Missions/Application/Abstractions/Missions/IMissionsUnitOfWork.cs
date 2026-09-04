using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Missions.Application.Abstractions.Missions
{
    internal interface IMissionsUnitOfWork : IUnitOfWork, IAuditLogSink;
}
