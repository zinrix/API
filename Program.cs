using API.Application.Addresses.Commands;
using API.Application.Addresses.Queries;
using API.Application.CurrencyConversion;
using API.Application.Currencies.Commands;
using API.Application.Users.Commands;
using API.Application.Users.Queries;
using API.Infrastructure;
using API.Infrastructure.Entities;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddValidatorsFromAssemblyContaining<CreateUserValidator>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<UserCommandHandler>();
builder.Services.AddScoped<UserQueryHandler>();
builder.Services.AddScoped<AddressCommandHandler>();
builder.Services.AddScoped<AddressQueryHandler>();
builder.Services.AddScoped<CurrencyCommandHandler>();
builder.Services.AddScoped<CurrencyConversionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Description = "API Key enviada en el header X-API-KEY.",
        Name = "X-API-KEY",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("ApiKey", document, ""),
            new List<string>()
        }
    });
});

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.EnsureCreatedAsync();
    if (!await db.Currencies.AnyAsync())
    {
        db.Currencies.AddRange(
            new Currency { Code = "PYG", Name = "Guaraní paraguayo", RateToBase = 1 },
            new Currency { Code = "USD", Name = "Dólar estadounidense", RateToBase = 7300 });
        await db.SaveChangesAsync();
    }
}

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/swagger"))
    {
        await next();
        return;
    }

    var expected = app.Configuration["ApiKey"];
    if (string.IsNullOrWhiteSpace(expected) || !context.Request.Headers.TryGetValue("X-API-KEY", out var received) || !string.Equals(received, expected, StringComparison.Ordinal))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { error = "API Key inválida o ausente." });
        return;
    }
    await next();
});

app.UseSwagger();
app.UseSwaggerUI();

app.MapPost("/users", async (CreateUserRequest request, IValidator<CreateUserRequest> validator, UserCommandHandler handler, CancellationToken ct) =>
{
    var validation = await validator.ValidateAsync(request, ct);
    if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());
    try { var user = await handler.CreateAsync(request, ct); return Results.Created($"/users/{user.Id}", ToUserResponse(user)); }
    catch (DbUpdateException) { return Results.Conflict(new { error = "El email ya existe." }); }
});

app.MapGet("/users", async (bool? isActive, UserQueryHandler handler, CancellationToken ct) => Results.Ok((await handler.GetAllAsync(isActive, ct)).Select(ToUserResponse)));
app.MapGet("/users/{id:int}", async (int id, UserQueryHandler handler, CancellationToken ct) =>
{
    var user = await handler.GetAsync(id, ct);
    return user is null ? Results.NotFound() : Results.Ok(ToUserResponse(user));
});
app.MapPut("/users/{id:int}", async (int id, UpdateUserRequest request, IValidator<UpdateUserRequest> validator, UserCommandHandler handler, CancellationToken ct) =>
{
    var validation = await validator.ValidateAsync(request, ct);
    if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());
    try { var user = await handler.UpdateAsync(id, request, ct); return user is null ? Results.NotFound() : Results.Ok(ToUserResponse(user)); }
    catch (DbUpdateException) { return Results.Conflict(new { error = "El email ya existe." }); }
});
app.MapDelete("/users/{id:int}", async (int id, UserCommandHandler handler, CancellationToken ct) => await handler.DeleteAsync(id, ct) ? Results.NoContent() : Results.NotFound());
app.MapPost("/users/bulk", async (BulkUsersRequest request, IValidator<BulkUsersRequest> validator, UserCommandHandler handler, CancellationToken ct) =>
{
    var validation = await validator.ValidateAsync(request, ct);
    if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());
    return Results.Ok(await handler.BulkAsync(request.Users!, ct));
});

app.MapPost("/users/{userId:int}/addresses", async (int userId, CreateAddressRequest request, IValidator<CreateAddressRequest> validator, AddressCommandHandler handler, CancellationToken ct) =>
{
    var validation = await validator.ValidateAsync(request, ct);
    if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());
    var result = await handler.CreateAsync(userId, request, ct);
    return !result.UserFound ? Results.NotFound(new { error = "El usuario no existe." }) : Results.Created($"/addresses/{result.Address!.Id}", result.Address);
});
app.MapGet("/users/{userId:int}/addresses", async (int userId, AddressQueryHandler handler, CancellationToken ct) =>
{
    var result = await handler.GetByUserAsync(userId, ct);
    return result.UserFound ? Results.Ok(result.Addresses) : Results.NotFound();
});
app.MapPut("/addresses/{id:int}", async (int id, UpdateAddressRequest request, IValidator<UpdateAddressRequest> validator, AddressCommandHandler handler, CancellationToken ct) =>
{
    var validation = await validator.ValidateAsync(request, ct);
    if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());
    var address = await handler.UpdateAsync(id, request, ct);
    return address is null ? Results.NotFound() : Results.Ok(address);
});
app.MapDelete("/addresses/{id:int}", async (int id, AddressCommandHandler handler, CancellationToken ct) => await handler.DeleteAsync(id, ct) ? Results.NoContent() : Results.NotFound());

app.MapGet("/currencies", async (IDbContextFactory<AppDbContext> factory, CancellationToken ct) =>
{
    await using var db = await factory.CreateDbContextAsync(ct);
    return Results.Ok(await db.Currencies.AsNoTracking().OrderBy(x => x.Code).ToListAsync(ct));
});
app.MapPost("/currencies", async (CreateCurrencyRequest request, IValidator<CreateCurrencyRequest> validator, CurrencyCommandHandler handler, CancellationToken ct) =>
{
    var validation = await validator.ValidateAsync(request, ct);
    if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());
    var result = await handler.CreateAsync(request, ct);
    return result.Duplicate ? Results.Conflict(new { error = "El código ya existe." }) : Results.Created($"/currencies/{result.Currency!.Id}", result.Currency);
});
app.MapPost("/currency/convert", async (CurrencyConversionRequest request, IValidator<CurrencyConversionRequest> validator, CurrencyConversionHandler handler, CancellationToken ct) =>
{
    var validation = await validator.ValidateAsync(request, ct);
    if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());
    var result = await handler.ConvertAsync(request, ct);
    return result.Response is null ? Results.NotFound(new { error = result.Error }) : Results.Ok(result.Response);
});

app.Run();

static object ToUserResponse(User user) => new { user.Id, user.Name, user.Email, user.IsActive, Addresses = user.Addresses.Select(x => new { x.Id, x.UserId, x.Street, x.City, x.Country, x.ZipCode }) };