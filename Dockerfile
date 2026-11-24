# ============================================================
# Finanzas-TWI — Dockerfile multi-stage (.NET 10)
# ============================================================
# Stage 1: Restaurar y compilar
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiar csproj y restaurar
COPY src/FinanzasPersonales.Api/FinanzasPersonales.Api.csproj ./FinanzasPersonales.Api/
RUN dotnet restore ./FinanzasPersonales.Api/FinanzasPersonales.Api.csproj

# Copiar código fuente restante (bin y obj excluidos por .dockerignore)
COPY src/FinanzasPersonales.Api/ ./FinanzasPersonales.Api/

# Publicar en modo Release
RUN dotnet publish ./FinanzasPersonales.Api/FinanzasPersonales.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ============================================================
# Stage 2: Runtime mínimo (aspnet, NO sdk)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# .NET 10 aspnet image ya incluye el usuario no-root 'app' (UID 1654)
# Solo creamos el directorio de DataProtection y ajustamos permisos
RUN mkdir -p /app/dataprotection-keys \
    && chown -R app:app /app/dataprotection-keys

USER app

# Copiar artefactos publicados
COPY --chown=app:app --from=build /app/publish .

# Variables de entorno predeterminadas (pueden sobreescribirse en Compose o en run)
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DISABLE_HTTPS_REDIRECTION=true
ENV USE_HTTP_COOKIES=true
ENV DATA_PROTECTION_KEYS_PATH=/app/dataprotection-keys

EXPOSE 8080

ENTRYPOINT ["dotnet", "FinanzasPersonales.Api.dll"]
