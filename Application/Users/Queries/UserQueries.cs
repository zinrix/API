using API.Infrastructure;
using API.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Users.Queries;

public class UserQueryHandler(IDbContextFactory<AppDbContext> factory)
{
    public async Task<List<User>> GetAllAsync(bool? isActive, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var query = db.Users.AsNoTracking().AsQueryable();
        if (isActive.HasValue) query = query.Where(x => x.IsActive == isActive.Value);
        return await query.OrderBy(x => x.Id).ToListAsync(cancellationToken);
    }

    public async Task<User?> GetAsync(int id, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Users.AsNoTracking().Include(x => x.Addresses).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }
}
