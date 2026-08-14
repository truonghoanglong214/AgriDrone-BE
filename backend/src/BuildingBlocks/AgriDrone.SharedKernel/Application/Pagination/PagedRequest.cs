using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.SharedKernel.Application.Pagination
{
    public sealed record PagedRequest(
        int PageNumber = 1,
        int PageSize = 20)
    {
        public int Skip => (PageNumber - 1) * PageSize;
    }
}
