# syntax=docker/dockerfile:1

# --- build: restore and publish the host only, not the whole .slnx ---------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .

RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore src/Bootstrap/Finmy.Api/Finmy.Api.csproj

RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish src/Bootstrap/Finmy.Api/Finmy.Api.csproj \
      -c Release -o /app/publish --no-restore

# --- migrator: same SDK image, applies EF migrations for the three modules -
# dotnet ef does its own design-time build at container runtime, when no cache
# mount is available, so both the tool and the four projects it touches need a
# real (non-cache-mounted) restore landing in this layer's packages folder.
FROM build AS migrator
WORKDIR /src

RUN dotnet tool restore
RUN for p in src/Bootstrap/Finmy.Api/Finmy.Api.csproj \
      src/Modules/Identity/Finmy.Identity.Infrastructure/Finmy.Identity.Infrastructure.csproj \
      src/Modules/Budgeting/Finmy.Budgeting.Infrastructure/Finmy.Budgeting.Infrastructure.csproj \
      src/Modules/Ledger/Finmy.Ledger.Infrastructure/Finmy.Ledger.Infrastructure.csproj; \
    do dotnet restore "$p" || exit 1; done

ENTRYPOINT ["/bin/sh", "-c", "set -e; for m in Identity Budgeting Ledger; do \
  dotnet ef database update \
    -p src/Modules/$m/Finmy.$m.Infrastructure \
    -s src/Bootstrap/Finmy.Api; \
  done"]

# --- final: aspnet runtime, non-root -----------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app

# Auto codegen mode writes generated handler wrappers to ./Internal on first
# use so it does not recompile them on every cold start; the app user needs
# write access to the /app directory itself, not just read access to the
# files in it -- COPY --chown only covers the files it creates.
RUN chown app:app /app
COPY --chown=app:app --from=build /app/publish .

USER app

ENTRYPOINT ["dotnet", "Finmy.Api.dll"]
