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
- `tests/AssemblyA` (not in the solution): manual end-to-end project with `EmitCompilerGeneratedFiles` — inspect `obj/Debug/net8.0/generated/` to eyeball real generated code. It exercises generic `[Inject]`-member classes with constraints (no class-level marker).
- `tests/Everlong.DI.SmokeTest`: package-level consumer smoke (analyzers + buildTransitive + lib
  via NuGet, not ProjectReference). Keep its `PackageReference` in sync with `AppVersion`; its
  committed `NuGet.Config` lists nuget.org only (public-repo safe). Pre-release it validates the
  just-packed nupkg when a local feed is passed per restore (see Environment quirks); after a
  release it validates the published package with no extra config. Must print `All OK!`.

## Environment quirks

Committed docs must never anchor machine-local facts: no user paths, feed folders, or
environment-variable names belong in this public repo. Describe mechanisms, not local values.

- When a repacked version is not picked up by restore, the global-packages cache still holds the
  old copy. Locate the real cache with `dotnet nuget locals global-packages --list` and remove
  `everlong.di/<version>` under that folder (the path differs per machine/user).
- Pre-release pack-to-consume smoke (AppVersion not yet on nuget.org): push the nupkg to any
  local folder feed and pass it per restore — SmokeTest's `NuGet.Config` clears other sources:
  `dotnet run --project tests/Everlong.DI.SmokeTest -p:RestoreAdditionalProjectSources="<feed-path>"`.
- When a repacked version is not picked up by restore, the global-packages cache still holds the
  old copy. Locate the real cache with `dotnet nuget locals global-packages --list` and remove
  `everlong.di/<version>` under that folder (the path differs per machine/user).
- Generator/tests target Roslyn via NuGet (`Microsoft.CodeAnalysis.CSharp`); tests intentionally use `4.12.0` (partial properties bind only on Roslyn ≥ 4.9). Keep the generator project itself on `4.8.0` for host compatibility.
- `LangVersion preview` is required by consumers using `[Inject]` on partial properties (C# 13).

## Behavior contract & doc sync

- **Authoritative behavior contract**: `docs/skills/everlong-di-workflow/SKILL.md` (workflow rules, red lines, diagnostics, generated-code shapes). It is the how-to; `README.md` is the user-facing overview; this file only records where things live and what must stay in sync. Read SKILL.md before touching generator/attribute behavior.
- **One commit, three files**: any behavior/API change lands in the same commit as SKILL.md (rules + red lines + diagnostic table) and README.md (user-facing wording), plus tests. Docs drift is a release-blocker.
- **Diagnostic IDs**: allocated DIG0001–DIG0019 (DIG0009 was removed in v2, never reused; DIG0018/DIG0019 added), single source in `src/Everlong.DI.Generators/Constants/Diagnostics.cs`; new IDs start at DIG0020. Every ID appears in three places: Descriptors, SKILL.md §5.1, and generator tests. Semantic traps are Errors; style nudges are Info.
- **Attribute API shape**: registration attributes take constructor args `(key?, enumerable?)`; `IsEnumerable`/`Key` are get-only, so `IsEnumerable = true` in any doc/usage is always wrong. Keys are string/int/Type/enum — the same set as `[Inject]`, same key space at runtime.
- **Behavior changes are allowed in exactly two directions**: tighten (turn a semantic trap into a diagnostic — call it out in the commit) or unlock (rely on the TryAdd/Add container contract — state the contract in the commit). Never silently rewrite an existing attribute's emission semantics.

## Generator architecture notes

- `ServiceRegistrationGenerator`: per-attribute incremental providers → per-type aggregation in `Execute` (GroupBy ImplementationType); registration-rule diagnostics (DIG0011–0017) are reported there; AlsoAs type validation is a separate transform-time pipeline (needs symbols). `ServiceInfo` is a value record — duplicates are deduplicated with `Distinct()`.
- `MemberInjectionGenerator`: transform returns `Result<InjectionInfo?>` with diagnostics carried out and reported at the source-output stage.
