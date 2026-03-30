# ── Stage 1: build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files first so the restore layer is cached
# independently of source code changes.
COPY DutchieLibrary.slnx ./
COPY src/DutchieLibrary/DutchieLibrary.csproj \
     src/DutchieLibrary/
COPY src/DutchieIntegration/DutchieIntegration.csproj \
     src/DutchieIntegration/
COPY src/DutchieIntacct/DutchieIntacct.csproj \
     src/DutchieIntacct/
COPY src/DutchieWorker/DutchieWorker.csproj \
     src/DutchieWorker/

RUN dotnet restore DutchieLibrary.slnx

# Copy remaining source and publish
COPY src/ src/
RUN dotnet publish src/DutchieWorker/DutchieWorker.csproj \
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

ENTRYPOINT ["dotnet", "DutchieWorker.dll"]
