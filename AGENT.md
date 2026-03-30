# CODEX_CONTEXT

Purpose: quick orientation for future Codex sessions in this repository.

## Repository Overview

- Stack: .NET 8, C#
- Solution: `DutchieLibrary.slnx`
- Primary goal: sync Dutchie POS data into Sage Intacct on schedules
- Main runtime entry point: `src/DutchieWorker`

Projects:
- `src/DutchieLibrary`: typed Dutchie API client + models
- `src/DutchieIntegration`: ERP-neutral pipelines and state abstraction
- `src/DutchieIntacct`: Intacct connector + config providers
- `src/DutchieWorker`: hosted workers that execute sync loops

## High-Value Flows

Closing report sync:
1. Worker triggers `ClosingReportSyncPipeline`
2. Pipeline pulls `IReportingClient.GetClosingReportAsync`
3. Maps report -> `JournalEntryPayload`
4. Posts via `IErpConnector.PostJournalEntryAsync`
5. Persists watermark via `ISyncStateStore`

Transaction sync:
1. Worker triggers `TransactionSyncPipeline`
2. Reads last watermark
3. Pulls incremental transactions
4. Maps each transaction -> `SalesTransactionPayload`
5. Posts via `IErpConnector.PostSalesTransactionAsync`
6. Advances watermark when run succeeds

## Important Files

- Worker wiring:
  - `src/DutchieWorker/Program.cs`
  - `src/DutchieWorker/Workers/ClosingReportWorker.cs`
  - `src/DutchieWorker/Workers/TransactionSyncWorker.cs`
- Integration pipelines:
  - `src/DutchieIntegration/Pipeline/ClosingReportSyncPipeline.cs`
  - `src/DutchieIntegration/Pipeline/TransactionSyncPipeline.cs`
- Abstractions:
  - `src/DutchieIntegration/Abstractions/IErpConnector.cs`
  - `src/DutchieIntegration/Abstractions/IErpConfigProvider.cs`
  - `src/DutchieIntegration/Abstractions/ISyncStateStore.cs`
- Dutchie client:
  - `src/DutchieLibrary/Clients/DutchieClientBase.cs`
  - `src/DutchieLibrary/Clients/ReportingClient.cs`
  - `src/DutchieLibrary/Models/Reporting/*`
- Intacct implementation:
  - `src/DutchieIntacct/Connectors/IntacctErpConnector.cs`
  - `src/DutchieIntacct/Configuration/*`

## Serialization Notes

- JSON library: `Newtonsoft.Json`
- Shared deserialization settings in `DutchieClientBase`:
  - camelCase contract resolver
  - nulls ignored
  - string enum converter
- Unknown response fields are now captured in extension bags:
  - `ClosingReport.AdditionalData`
  - `Transaction.AdditionalData`
- Helper for consuming unknown fields:
  - `src/DutchieLibrary/Models/JsonExtensionDataExtensions.cs`
  - Use `TryGetAdditionalValue<T>(key, out value)`

## Common Commands

Build specific project:
```bash
dotnet build src/DutchieLibrary/DutchieLibrary.csproj -v minimal
```

Build worker (integration runtime):
```bash
dotnet build src/DutchieWorker/DutchieWorker.csproj -v minimal
```

Run worker locally:
```bash
cd src/DutchieWorker
dotnet run
```

Run tests (if/when present):
```bash
dotnet test
```

## Config and Environment

- Example env file: `.env.example`
- App config: `src/DutchieWorker/appsettings.json`
- Sync watermark file path is configurable (`Worker:SyncStateFile`)
- Platform App XML: `PlatformApp/DutchieIntegration.xml`

## Working Conventions

- Prefer adding/using mapping logic in Integration pipelines, not inside raw API models.
- Keep API models tolerant of schema drift; promote new fields to typed properties only when actively used.
- Keep ERP-specific logic behind `IErpConnector`/provider abstractions.
- Avoid edits to `bin/` and `obj/` artifacts.
