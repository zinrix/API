using API.Infrastructure;
using API.Infrastructure.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace API.Application.Users.Commands;

public record CreateUserRequest(string? Name, string? Email, bool IsActive = true, string? Password = null);
public record UpdateUserRequest(string? Name, string? Email, bool IsActive);
public record BulkUsersRequest(List<CreateUserRequest>? Users);
public record BulkUserFailure(string? Email, string Reason);
public record BulkUsersResult(int Created, IReadOnlyList<BulkUserFailure> Failed);

public class CreateUserValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.Password).MinimumLength(8).When(x => x.Password is not null);
    }
}

public class UpdateUserValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
    }
}

public class BulkUsersValidator : AbstractValidator<BulkUsersRequest>
{
    public BulkUsersValidator(IValidator<CreateUserRequest> userValidator)
    {
        RuleFor(x => x.Users).NotNull().NotEmpty().Must(x => x!.Count <= 1000);
        RuleForEach(x => x.Users).SetValidator((IValidator<CreateUserRequest>)userValidator);
    }
}

public class UserCommandHandler(IDbContextFactory<AppDbContext> factory, IPasswordHasher<User> passwordHasher)
{
    public async Task<User> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var user = new User { Name = request.Name!.Trim(), Email = request.Email!.Trim().ToLowerInvariant(), IsActive = request.IsActive };
        user.Password = HashPassword(user, request.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task<User?> UpdateAsync(int id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.FindAsync([id], cancellationToken);
        if (user is null) return null;
        user.Name = request.Name!.Trim();
        user.Email = request.Email!.Trim().ToLowerInvariant();
        user.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.FindAsync([id], cancellationToken);
        if (user is null) return false;
        db.Users.Remove(user);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<BulkUsersResult> BulkAsync(IEnumerable<CreateUserRequest> requests, CancellationToken cancellationToken)
    {
        var results = await Task.WhenAll(requests.Select(request => CreateOneBulkAsync(request, cancellationToken)));
        return new BulkUsersResult(results.Count(x => x.Created), results.Where(x => !x.Created).Select(x => new BulkUserFailure(x.Email, x.Reason)).ToList());
    }

    private async Task<(bool Created, string? Email, string Reason)> CreateOneBulkAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync(cancellationToken);
            var email = request.Email!.Trim().ToLowerInvariant();
            if (await db.Users.AnyAsync(x => x.Email == email, cancellationToken)) return (false, email, "El email ya existe.");
            var user = new User { Name = request.Name!.Trim(), Email = email, IsActive = request.IsActive };
            user.Password = HashPassword(user, request.Password);
            db.Users.Add(user);
            await db.SaveChangesAsync(cancellationToken);
            return (true, email, string.Empty);
        }
        catch (DbUpdateException)
        {
            return (false, request.Email, "No se pudo guardar el usuario (posible email duplicado).");
        }
        catch (Exception ex)
        {
            return (false, request.Email, ex.Message);
        }
    }

    private string? HashPassword(User user, string? password) =>
        string.IsNullOrWhiteSpace(password) ? null : passwordHasher.HashPassword(user, password);
}
