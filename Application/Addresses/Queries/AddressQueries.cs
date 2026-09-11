using API.Infrastructure;
using API.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Addresses.Queries;

public class AddressQueryHandler(IDbContextFactory<AppDbContext> factory)
{
    public async Task<(bool UserFound, List<Address> Addresses)> GetByUserAsync(int userId, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var userFound = await db.Users.AnyAsync(x => x.Id == userId, cancellationToken);
        var addresses = userFound ? await db.Addresses.AsNoTracking().Where(x => x.UserId == userId).OrderBy(x => x.Id).ToListAsync(cancellationToken) : [];
        return (userFound, addresses);
    }
}
