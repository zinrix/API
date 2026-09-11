# API de Usuarios, Direcciones y Divisas

API REST en .NET 10 con Minimal API, EF Core, SQLite, CQRS, FluentValidation, API Key y Swagger.

## Requisitos y ejecución

- .NET SDK 10.0
- Visual Studio 2026 o `dotnet` CLI

```powershell
dotnet restore
dotnet run
```

La aplicación crea `api.db` automáticamente al iniciar mediante `EnsureCreated`. No es necesario ejecutar migraciones para esta entrega. Para cambiar la ubicación de la base, modificar `ConnectionStrings:DefaultConnection` en `appsettings.json`.

## Seguridad

Todos los endpoints requieren el header:

```http
X-API-KEY: dev-api-key
```

La clave se configura en `ApiKey` dentro de `appsettings.json`; debe cambiarse en entornos reales. Sin la cabecera o con una clave incorrecta se devuelve `401 Unauthorized`.

Swagger está disponible en `/swagger` y también requiere la API Key para acceder.

## Ejemplos

Crear un usuario:

```powershell
$body = @{ name = "Juan"; email = "juan@test.com"; password = "UnaClaveSegura123!" } | ConvertTo-Json
curl.exe -X POST http://localhost:5000/users -H "Content-Type: application/json" -H "X-API-KEY: dev-api-key" -d $body
```

Carga masiva:

```powershell
curl.exe -X POST http://localhost:5000/users/bulk -H "Content-Type: application/json" -H "X-API-KEY: dev-api-key" --data-binary "@sample-data/bulk_users_test.json"
```

Conversión:

```powershell
$body = @{ fromCurrencyCode = "USD"; toCurrencyCode = "PYG"; amount = 100 } | ConvertTo-Json
curl.exe -X POST http://localhost:5000/currency/convert -H "Content-Type: application/json" -H "X-API-KEY: dev-api-key" -d $body
```

## Endpoints

- `POST`, `GET`, `GET /users/{id}`, `PUT`, `DELETE /users`
- `POST /users/bulk`
- `POST /users/{userId}/addresses`, `GET /users/{userId}/addresses`
- `PUT`, `DELETE /addresses/{id}`
- `GET`, `POST /currencies`
- `POST /currency/convert`

El bulk valida cada elemento, usa `Task.WhenAll` y crea un `DbContext` independiente mediante `IDbContextFactory` para cada operación concurrente. La conversión consulta las dos divisas en paralelo con contextos independientes.

## Arquitectura y alcance

- `Infrastructure`: entidades y `AppDbContext` con relaciones y claves únicas.
- `Application/Users`, `Application/Addresses`, `Application/Currencies` y `Application/CurrencyConversion`: comandos, consultas, handlers y validadores CQRS.
- API Key, CRUD de usuarios y direcciones, monedas, conversión, bulk, Swagger y SQLite están implementados.
- La contraseña es opcional en el contrato actual y, cuando se recibe, se almacena mediante `PasswordHasher<User>`; nunca se devuelve en las respuestas. Los usuarios creados sin contraseña mantienen ese campo nulo.
- No se generaron migraciones versionadas; la base inicial se crea con `EnsureCreated`, según se documenta arriba.
