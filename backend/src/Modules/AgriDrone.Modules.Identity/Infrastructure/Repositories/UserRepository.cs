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

    public Task<User?> GetByEmailIncludingDeletedAsync(
        string email,
        CancellationToken cancellationToken = default) =>
        dbContext.Users.SingleOrDefaultAsync(
            user => user.Email == email,
            cancellationToken);

    public async Task<IReadOnlyCollection<string>> GetSystemRoleCodesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await (
            from userRole in dbContext.UserRoles
            join role in dbContext.Roles on userRole.RoleId equals role.Id
            where userRole.UserId == userId
            select role.Code)
            .ToArrayAsync(cancellationToken);
    }

    public Task<List<User>> GetAllAsync(CancellationToken cancellationToken = default)
        => dbContext.Users.Where(user => user.DeletedAt == null).ToListAsync(cancellationToken);

    public void Add(User user) => dbContext.Users.Add(user);

    public void Update(User user) => dbContext.Users.Update(user);
}
