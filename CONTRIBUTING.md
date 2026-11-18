# Contributing

- One public type per file, in the folder for its area: `Database/`, `Results/`, `Values/`, `Errors/`, `Data/` (ADO.NET), `Interop/` (native, internal).
- Namespaces stay `Ambystech.Relate` and `Ambystech.Relate.Data`; folders are for navigation only.
- Tests mirror the source folders under `tests/Ambystech.Relate.Tests/`; shared fixtures live in `Support/`.
- Before sending a change: `scripts/fetch-natives.sh`, then `dotnet build -c Release` and `dotnet test -c Release` (warnings are errors).
