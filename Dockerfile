# ── Stage 1: build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files first so the restore layer is cached
# independently of source code changes.
COPY AcadiaLogic.Dutchie.Library.slnx ./
COPY src/AcadiaLogic.Dutchie.Library/AcadiaLogic.Dutchie.Library.csproj \
     src/AcadiaLogic.Dutchie.Library/
COPY src/AcadiaLogic.Dutchie.Integration/AcadiaLogic.Dutchie.Integration.csproj \
     src/AcadiaLogic.Dutchie.Integration/
COPY src/AcadiaLogic.Dutchie.Intacct/AcadiaLogic.Dutchie.Intacct.csproj \
     src/AcadiaLogic.Dutchie.Intacct/
COPY src/AcadiaLogic.Dutchie.Worker/AcadiaLogic.Dutchie.Worker.csproj \
     src/AcadiaLogic.Dutchie.Worker/

RUN dotnet restore AcadiaLogic.Dutchie.Library.slnx

# Copy remaining source and publish
COPY src/ src/
RUN dotnet publish src/AcadiaLogic.Dutchie.Worker/AcadiaLogic.Dutchie.Worker.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

# ── Stage 2: runtime ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app

# Create directories that will be bind-mounted or used at runtime.
# Running as non-root requires the process to own these paths.
RUN mkdir -p logs

COPY --from=build /app/publish ./

# Non-root user for security
RUN addgroup --system dutchie && adduser --system --ingroup dutchie dutchie
RUN chown -R dutchie:dutchie /app
USER dutchie

ENTRYPOINT ["dotnet", "AcadiaLogic.Dutchie.Worker.dll"]
