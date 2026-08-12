using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.SharedKernel.Application
{
    public enum ErrorType
    {
        Failure,
        Validation,
        NotFound,
        Conflict,
        Unauthorized,
        Forbidden
    }
}
