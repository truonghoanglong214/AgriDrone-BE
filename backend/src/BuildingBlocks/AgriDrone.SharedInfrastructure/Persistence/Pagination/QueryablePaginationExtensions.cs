using AgriDrone.SharedKernel.Application.Pagination;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.SharedInfrastructure.Persistence.Pagination
{
    public static class QueryablePaginationExtensions
    {
        public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        PagedRequest pageRequest,
        CancellationToken cancellationToken = default)
        {
            var totalCount = await query.LongCountAsync(cancellationToken);

            var items = await query
                .Skip(pageRequest.Skip)
                .Take(pageRequest.PageSize)
                .ToListAsync(cancellationToken);

            return new PagedResult<T>(
                items,
                pageRequest.PageNumber,
                pageRequest.PageSize,
                totalCount);
        }
    }
}
