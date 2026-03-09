# CODEX_CONTEXT

Purpose: quick orientation for future Codex sessions in this repository.

## Repository Overview

- Stack: .NET 8, C#
- Solution: `AcadiaLogic.Dutchie.Library.slnx`
- Primary goal: sync Dutchie POS data into Sage Intacct on schedules
- Main runtime entry point: `src/AcadiaLogic.Dutchie.Worker`

Projects:
- `src/AcadiaLogic.Dutchie.Library`: typed Dutchie API client + models
- `src/AcadiaLogic.Dutchie.Integration`: ERP-neutral pipelines and state abstraction
- `src/AcadiaLogic.Dutchie.Intacct`: Intacct connector + config providers
- `src/AcadiaLogic.Dutchie.Worker`: hosted workers that execute sync loops

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
  - `src/AcadiaLogic.Dutchie.Worker/Program.cs`
  - `src/AcadiaLogic.Dutchie.Worker/Workers/ClosingReportWorker.cs`
  - `src/AcadiaLogic.Dutchie.Worker/Workers/TransactionSyncWorker.cs`
- Integration pipelines:
  - `src/AcadiaLogic.Dutchie.Integration/Pipeline/ClosingReportSyncPipeline.cs`
  - `src/AcadiaLogic.Dutchie.Integration/Pipeline/TransactionSyncPipeline.cs`
- Abstractions:
  - `src/AcadiaLogic.Dutchie.Integration/Abstractions/IErpConnector.cs`
  - `src/AcadiaLogic.Dutchie.Integration/Abstractions/IErpConfigProvider.cs`
  - `src/AcadiaLogic.Dutchie.Integration/Abstractions/ISyncStateStore.cs`
- Dutchie client:
  - `src/AcadiaLogic.Dutchie.Library/Clients/DutchieClientBase.cs`
  - `src/AcadiaLogic.Dutchie.Library/Clients/ReportingClient.cs`
  - `src/AcadiaLogic.Dutchie.Library/Models/Reporting/*`
- Intacct implementation:
  - `src/AcadiaLogic.Dutchie.Intacct/Connectors/IntacctErpConnector.cs`
  - `src/AcadiaLogic.Dutchie.Intacct/Configuration/*`

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
  - `src/AcadiaLogic.Dutchie.Library/Models/JsonExtensionDataExtensions.cs`
  - Use `TryGetAdditionalValue<T>(key, out value)`

## Common Commands

Build specific project:
```bash
dotnet build src/AcadiaLogic.Dutchie.Library/AcadiaLogic.Dutchie.Library.csproj -v minimal
```

Build worker (integration runtime):
```bash
dotnet build src/AcadiaLogic.Dutchie.Worker/AcadiaLogic.Dutchie.Worker.csproj -v minimal
```

Run worker locally:
```bash
cd src/AcadiaLogic.Dutchie.Worker
dotnet run
```

Run tests (if/when present):
```bash
dotnet test
```

## Config and Environment

- Example env file: `.env.example`
- App config: `src/AcadiaLogic.Dutchie.Worker/appsettings.json`
- Sync watermark file path is configurable (`Worker:SyncStateFile`)
- Platform App XML: `platform-app/DutchieIntegration.xml`

## Working Conventions

- Prefer adding/using mapping logic in Integration pipelines, not inside raw API models.
- Keep API models tolerant of schema drift; promote new fields to typed properties only when actively used.
- Keep ERP-specific logic behind `IErpConnector`/provider abstractions.
- Avoid edits to `bin/` and `obj/` artifacts.
