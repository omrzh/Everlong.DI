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

## Testing & verification

- `dotnet test tests/Everlong.DI.Tests` — unit + generator + snapshot (Verify) tests. Keep them green; they are the release gate.
- Changing generated output means updating `tests/.../Snapshots/*.verified.txt` (promote `.received.txt` after reviewing the diff).
- `tests/AssemblyA` (not in the solution): manual end-to-end project with `EmitCompilerGeneratedFiles` — inspect `obj/Debug/net8.0/generated/` to eyeball real generated code. It exercises generic `[Injectable]` partial classes with constraints.
- `tests/Everlong.DI.SmokeTest`: consumes the published package from nuget.org (see its `NuGet.Config`); must print `All OK!`.

## Environment quirks

- NuGet global packages folder is **`D:\AppData\.nuget\packages`**, not `%USERPROFILE%\.nuget` — clear that path when a repacked version is not picked up.
- Generator/tests target Roslyn via NuGet (`Microsoft.CodeAnalysis.CSharp`); tests intentionally use `4.12.0` (partial properties bind only on Roslyn ≥ 4.9). Keep the generator project itself on `4.8.0` for host compatibility.
- `LangVersion preview` is required by consumers using `[Inject]` on partial properties (C# 13).

## Design red lines (from docs/skills/everlong-di-workflow)

### Member injection

- `[Injectable]` + `partial` are required; `IInjectable` is recommended (the generator appends it to the partial itself). Generated `Inject()` is never called automatically — someone must call it (manual, `AddInjector()`, or a framework interceptor).
- Never `[Inject]` a `readonly` field (compile-time error DIG0008; the generator also skips the class).
- Default `Inject()` is idempotent (`Reinjectable = false`); transient re-injection needs `Reinjectable = true`.
- Exactly one `[ServiceRegistrar]` per assembly (DIG0003); the registrar class must be `partial`.

### Service registration

- **One lifetime per type** — cross-lifetime mixes (`[Singleton]` + `[Scoped]`, `[Singleton<IFoo>]` + `[Scoped<IBar>]`) are DIG0016.
- **Self × generic mutually exclusive within a lifetime** — `[Singleton]` + `[Singleton<IFoo>]` is DIG0015. Shared instance → `[Singleton]` + `[AlsoAs<IFoo>]`; independent instances → multiple `[Singleton<T>]` (generic attributes are `AllowMultiple`).
- **`[AlsoAs<T>]` needs exactly one non-transient, non-enumerable main registration** (DIG0011–0014) and `T` must be an interface the class implements (DIG0017). Transient has no shareable instance; enumerable mains have no single instance.
- **Duplicate registrations are allowed, not errors** (`TryAdd` first-wins, `Add` accumulates) — the semantic traps are the *combinations* above, which the generator rejects.
- **Keyed registrations** (`[Singleton<T>("key")]`, `[AlsoAs<T>("key")]`) share the key space with `[Inject("key")]`; keyed and unkeyed registrations are independent.
- `isEnumerable` / `key` are **constructor arguments** (`(key?, enumerable?)`), never named property assignments — `IsEnumerable` is get-only.
