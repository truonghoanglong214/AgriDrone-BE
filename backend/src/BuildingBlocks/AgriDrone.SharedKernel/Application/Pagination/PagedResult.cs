using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.SharedKernel.Application.Pagination
{
    public sealed record PagedResult<T>(
        IReadOnlyList<T> Items,
        int PageNumber,
        int PageSize,
        long TotalCount)
    {

        public long TotalPages => (long)Math.Ceiling(TotalCount / (double)PageSize);

        public bool HasPreviousPage => PageNumber > 1;

        public bool HasNextPage => PageNumber < TotalPages;
    }
}
