# CLAUDE.md

## Repo purpose

This repository contains a cross-framework gesture stack for .NET:

- `src/AppoMobi.Gestures`: framework-independent contracts and event/data models.
- `src/AppoMobi.Maui.Gestures`: .NET MAUI implementation.
- `src/AppoMobi.Blazor.Gestures`: Blazor implementation.
- `samples/MauiSample` and `samples/BlazorWasmSample`: usage samples.
- `dev/GesturesTester`: manual validation app for gesture behavior.

The repo name is still `AppoMobi.Maui.Gestures`, but the package family is broader and centers on `AppoMobi.Gestures`.

## Working rules

- Put shared gesture contracts, enums, pointer/touch data, and listener APIs in `src/AppoMobi.Gestures`.
- Put MAUI-specific effect, platform, and registration code in `src/AppoMobi.Maui.Gestures`.
- Put Blazor-specific interop and service registration code in `src/AppoMobi.Blazor.Gestures`.
- Prefer the smallest possible change in the project that actually owns the behavior.
- Keep public API changes deliberate because the packages are consumed externally.
- Use `dev/` for repo automation or helper scripts; do not introduce a new `eng/` folder.

## Build and validation

There are no dedicated test projects in this repo at the moment. Validate with targeted builds first.

Common commands:

```powershell
dotnet build .\AppoMobi.Maui.Gestures.sln
dotnet build .\src\AppoMobi.Gestures\AppoMobi.Gestures.csproj
dotnet build .\src\AppoMobi.Maui.Gestures\AppoMobi.Maui.Gestures.csproj
dotnet build .\src\AppoMobi.Blazor.Gestures\AppoMobi.Blazor.Gestures.csproj
```

Use the samples or `dev/GesturesTester` only when a change needs runtime gesture verification.

## Packaging notes

- NuGet packages are generated on build for all three source projects.
- When changing a NuGet package for local consumption, bump the prerelease version before packing.
- For local machine testing, copy generated `.nupkg` files to `C:\Nugets`.

## Versioning facts

- `AppoMobi.Gestures`: `netstandard2.0`
- `AppoMobi.Maui.Gestures`: multi-targets .NET 9 and .NET 10, including platform TFMs
- `AppoMobi.Blazor.Gestures`: `net9.0` and `net10.0`