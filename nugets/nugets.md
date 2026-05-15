# NuGet CI/CD

This repo has a GitHub action at `.github/workflows/nuget-release.yml`.

## What The Workflow Does

The workflow is manual and runs from the GitHub Actions UI with `workflow_dispatch`.

It has three jobs:

1. `Pack NuGet packages`
2. `Publish to NuGet.org` when `publish_nuget = true`
3. `Publish to GitHub Packages` when `publish_github = true`

The pack job creates the artifact once, and both publish jobs reuse it.

## Package List

The workflow packs these package projects:

- `src/AppoMobi.Gestures/AppoMobi.Gestures.csproj`
- `src/AppoMobi.Maui.Gestures/AppoMobi.Maui.Gestures.csproj`
- `src/AppoMobi.Blazor.Gestures/AppoMobi.Blazor.Gestures.csproj`

Shared NuGet metadata lives in `src/NuGet.props`.

Package-specific title, description, tags, and MAUI readme packaging stay in each project file.

Important runtime detail:

- `AppoMobi.Maui.Gestures` targets both `.NET 9` and `.NET 10`
- `AppoMobi.Blazor.Gestures` targets both `.NET 9` and `.NET 10`
- the workflow pins SDK `10.0.300` in `global.json` so `dotnet pack` can produce packages for both target generations on the runner

## Produced Artifacts

The pack job collects these files from `bin/Release` outputs under `src`:

- `*.nupkg`
- `*.snupkg`

Those files are uploaded as one workflow artifact named `nuget-packages`.

## Required Repository Secrets

Create this repository secret before publishing to NuGet.org:

- `NUGET_API_KEY`: NuGet.org API key for package publishing

GitHub Packages publishing uses the built-in `secrets.GITHUB_TOKEN` and does not require a custom package secret.

## GitHub Settings To Verify

Before the first run, verify:

1. GitHub Actions is enabled for the repository.
2. Manual workflow runs are allowed.
3. The repository token can write packages.
4. Existing GitHub Packages, if any, are connected to this repository.

The GitHub Packages feed URL used by the workflow is:

- `https://nuget.pkg.github.com/<repository-owner>/index.json`

## How To Run The Workflow

1. Open the repository `Actions` tab.
2. Select `Publish NuGets`.
3. Click `Run workflow`.
4. Choose whether to publish to NuGet.org.
5. Choose whether to publish to GitHub Packages.
6. Start the run.

Recommended first validation:

1. Run with both publish toggles set to `false`.
2. Inspect the `nuget-packages` artifact.
3. Confirm all expected `.nupkg` and `.snupkg` files are present.

## Job Behavior Details

### Pack Job

This job:

- checks out the repository
- installs the SDK from `global.json`
- installs the `maui` workload
- builds the three NuGet projects explicitly
- collects produced packages into one artifact

### NuGet.org Publish Job

This job:

- downloads the `nuget-packages` artifact
- requires `NUGET_API_KEY`
- pushes every `.nupkg` with `-SkipDuplicate`

### GitHub Packages Publish Job

This job:

- downloads the same artifact
- configures the GitHub Packages feed with the built-in token
- pushes `.nupkg` files only
- uses `packages: write`

## Failure Triage

If packing fails:

- check the SDK selected from `global.json`
- check MAUI workload installation logs
- check the explicit project paths in the workflow

If NuGet.org publishing fails:

- check `NUGET_API_KEY`
- check whether the package version already exists

If GitHub Packages publishing fails:

- check package write permission for `GITHUB_TOKEN`
- check package repository linkage in GitHub Packages
- check package metadata points to this repository

## Maintenance Notes

If you add or remove a package project, update the explicit project list in `.github/workflows/nuget-release.yml`.

If package versions change, the workflow does not need filename updates because it discovers packages dynamically.