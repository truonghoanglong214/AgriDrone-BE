using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL.Query.Expressions.Internal;

namespace AgriDrone.Modules.Identity.Infrastructure.Repositories;

internal sealed class UserRepository(IdentityDbContext dbContext) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) 
        =>dbContext.Users.SingleOrDefaultAsync(user => user.Id == id && user.DeletedAt == null,cancellationToken);

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) 
        =>dbContext.Users.SingleOrDefaultAsync(user => user.Email == email && user.DeletedAt == null,cancellationToken);

    public Task<List<User>> GetAllAsync(CancellationToken cancellationToken = default)
        => dbContext.Users.Where(user => user.DeletedAt == null).ToListAsync(cancellationToken);

    public void Add(User user) => dbContext.Users.Add(user);
}
