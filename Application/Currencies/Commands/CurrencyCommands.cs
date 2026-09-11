using API.Infrastructure;
using API.Infrastructure.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Currencies.Commands;

public record CreateCurrencyRequest(string? Code, string? Name, decimal RateToBase);

public class CreateCurrencyValidator : AbstractValidator<CreateCurrencyRequest>
{
    public CreateCurrencyValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(10);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.RateToBase).GreaterThan(0);
    }
}

public class CurrencyCommandHandler(IDbContextFactory<AppDbContext> factory)
{
    public async Task<(Currency? Currency, bool Duplicate)> CreateAsync(CreateCurrencyRequest request, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var code = request.Code!.Trim().ToUpperInvariant();
        if (await db.Currencies.AnyAsync(x => x.Code == code, cancellationToken)) return (null, true);
        var currency = new Currency { Code = code, Name = request.Name!.Trim(), RateToBase = request.RateToBase };
        db.Currencies.Add(currency);
        await db.SaveChangesAsync(cancellationToken);
        return (currency, false);
    }
}
