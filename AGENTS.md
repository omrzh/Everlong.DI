# AGENTS.md — Everlong.DI

Solo-maintained repo (`github.com/omrzh/Everlong.DI`). Keep processes light.

## Branching & releases

- **Single trunk (`master`), commit directly.** No dev branch. PRs are optional and only for big changes — do not invent a PR flow.
- **Releases are tag-driven**: bump `AppVersion` in `EverlongDI.props` → `git tag v<AppVersion>` → `git push origin v<AppVersion>`. The `release` GitHub Actions workflow builds, tests, packs and pushes to nuget.org automatically.
- **No API keys anywhere.** nuget.org uses Trusted Publishing (OIDC): `NuGet/login@v1` exchanges a GitHub OIDC token for a short-lived key. The `user` input comes from the `NUGET_USER` Actions **variable** in the `production` environment — the nuget.org account that *created* the trust policy (not the package owner). It is a Variable, not a Secret, and not a credential. Never ask for or invent a NuGet API key.
- Tag must match `AppVersion` (e.g. `v0.1.0` ↔ `0.1.0`).

## CI/CD

- `.github/workflows/ci.yml` — build + `dotnet test` on every push/PR (ubuntu, .NET 8 + 10 SDKs).
- `.github/workflows/release.yml` — on `v*` tag: Release build → test → `dotnet pack src/Everlong.DI` → `NuGet/login@v1` → push with `--skip-duplicate`.
- `publish.ps1` is the local alternative: builds + packs into `./publish/` only (no push).

## Testing & verification

- `dotnet test tests/Everlong.DI.Tests` — unit + generator + snapshot (Verify) tests. Keep them green; they are the release gate.
- Changing generated output means updating `tests/.../Snapshots/*.verified.txt` (promote `.received.txt` after reviewing the diff).
- `tests/AssemblyA` (not in the solution): manual end-to-end project with `EmitCompilerGeneratedFiles` — inspect `obj/Debug/net8.0/generated/` to eyeball real generated code. It exercises generic `[Injectable]` partial classes with constraints.
- `tests/Everlong.DI.SmokeTest`: consumes the *packed* package from `./publish` (see its `NuGet.Config`); must print `All OK!`. After repacking, clear the package cache or restore picks up a stale copy.

## Environment quirks

- NuGet global packages folder is **`D:\AppData\.nuget\packages`**, not `%USERPROFILE%\.nuget` — clear that path when a repacked version is not picked up.
- Generator/tests target Roslyn via NuGet (`Microsoft.CodeAnalysis.CSharp`); tests intentionally use `4.12.0` (partial properties bind only on Roslyn ≥ 4.9). Keep the generator project itself on `4.8.0` for host compatibility.
- `LangVersion preview` is required by consumers using `[Inject]` on partial properties (C# 13).

## Design red lines (from docs/skills/everlong-di-workflow)

- `[Injectable]` + `partial` + `IInjectable` are all required; generated `Inject()` is never called automatically — someone must call it (manual, `AddInjector()`, or a framework interceptor).
- Never `[Inject]` a `readonly` field (generator skips the class silently).
- Default `Inject()` is idempotent (`Reinjectable = false`); transient re-injection needs `Reinjectable = true`.
