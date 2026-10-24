## Aplicar Migraciones
dotnet ef database update \
  --project apps/backend/src/Situ.Infrastructure/Situ.Infrastructure.csproj \
  --startup-project apps/backend/src/Situ.Api/Situ.Api.csproj