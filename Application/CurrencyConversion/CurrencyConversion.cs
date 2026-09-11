using API.Infrastructure;
using API.Infrastructure.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace API.Application.CurrencyConversion;

public record CurrencyConversionRequest(string? FromCurrencyCode, string? ToCurrencyCode, decimal Amount);
public record CurrencyConversionResponse(string FromCurrency, string ToCurrency, decimal OriginalAmount, decimal ConvertedAmount);

public class CurrencyConversionValidator : AbstractValidator<CurrencyConversionRequest>
{
    public CurrencyConversionValidator()
    {
        RuleFor(x => x.FromCurrencyCode).NotEmpty().MaximumLength(10);
        RuleFor(x => x.ToCurrencyCode).NotEmpty().MaximumLength(10);
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}

public class CurrencyConversionHandler(IDbContextFactory<AppDbContext> factory)
{
    public async Task<(CurrencyConversionResponse? Response, string? Error)> ConvertAsync(CurrencyConversionRequest request, CancellationToken cancellationToken)
    {
        var fromTask = FindAsync(request.FromCurrencyCode!, cancellationToken);
        var toTask = FindAsync(request.ToCurrencyCode!, cancellationToken);
        var currencies = await Task.WhenAll(fromTask, toTask);
        if (currencies[0] is null) return (null, $"La moneda de origen '{request.FromCurrencyCode}' no existe.");
        if (currencies[1] is null) return (null, $"La moneda destino '{request.ToCurrencyCode}' no existe.");
        var converted = decimal.Round(request.Amount * currencies[0]!.RateToBase / currencies[1]!.RateToBase, 6);
        return (new CurrencyConversionResponse(currencies[0].Code, currencies[1].Code, request.Amount, converted), null);
    }

    private async Task<Currency?> FindAsync(string code, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Currencies.AsNoTracking().FirstOrDefaultAsync(x => x.Code == code.Trim().ToUpper(), cancellationToken);
    }
}
