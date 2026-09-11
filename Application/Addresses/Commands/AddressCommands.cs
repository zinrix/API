using API.Infrastructure;
using API.Infrastructure.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Addresses.Commands;

public record CreateAddressRequest(string? Street, string? City, string? Country, string? ZipCode);
public record UpdateAddressRequest(string? Street, string? City, string? Country, string? ZipCode);

public class CreateAddressValidator : AbstractValidator<CreateAddressRequest>
{
    public CreateAddressValidator()
    {
        RuleFor(x => x.Street).NotEmpty().MaximumLength(300);
        RuleFor(x => x.City).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Country).NotEmpty().MaximumLength(150);
        RuleFor(x => x.ZipCode).MaximumLength(30);
    }
}

public class UpdateAddressValidator : AbstractValidator<UpdateAddressRequest>
{
    public UpdateAddressValidator()
    {
        RuleFor(x => x.Street).NotEmpty().MaximumLength(300);
        RuleFor(x => x.City).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Country).NotEmpty().MaximumLength(150);
        RuleFor(x => x.ZipCode).MaximumLength(30);
    }
}

public class AddressCommandHandler(IDbContextFactory<AppDbContext> factory)
{
    public async Task<(Address? Address, bool UserFound)> CreateAsync(int userId, CreateAddressRequest request, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        if (!await db.Users.AnyAsync(x => x.Id == userId, cancellationToken)) return (null, false);
        var address = new Address { UserId = userId, Street = request.Street!.Trim(), City = request.City!.Trim(), Country = request.Country!.Trim(), ZipCode = request.ZipCode?.Trim() };
        db.Addresses.Add(address);
        await db.SaveChangesAsync(cancellationToken);
        return (address, true);
    }

    public async Task<Address?> UpdateAsync(int id, UpdateAddressRequest request, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var address = await db.Addresses.FindAsync([id], cancellationToken);
        if (address is null) return null;
        address.Street = request.Street!.Trim(); address.City = request.City!.Trim(); address.Country = request.Country!.Trim(); address.ZipCode = request.ZipCode?.Trim();
        await db.SaveChangesAsync(cancellationToken);
        return address;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var address = await db.Addresses.FindAsync([id], cancellationToken);
        if (address is null) return false;
        db.Remove(address);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
