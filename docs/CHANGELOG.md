# Changelog

All notable changes to Everlong.DI. Format: one entry per release, newest first.
Versions are tagged `v<AppVersion>`; the release workflow builds/tests/packs/pushes
automatically. Breaking changes are flagged with ⚠️.

## v0.4.1 — 2026-09-03

- Generated public members now carry `/// <inheritdoc/>`: `Inject()` on `[Inject]` /
  `IAutoInject` classes and `RegisterServices()` on `[ServiceRegistrar]` classes inherit
  their XML docs from the `IInjectable.Inject` / `IServiceRegistrar.RegisterServices`
  contracts. Consumers that enable `GenerateDocumentationFile` no longer get CS1591 on
  Everlong.DI-generated files — those members are only declared in generated code, so
  they could not be documented from the consumer side.

## v0.4.0 — 2026-09-03 (v2 — member-injection redesign)

⚠️ Breaking — the class-level attribute era ends. The `[Injectable]` attribute and the
`Reinjectable` option are **removed**; `DIG0009` no longer exists.

### Member injection is now member-anchored

- `[Inject]` members alone anchor generation: any partial class that declares at least one
  gets an `Inject(IServiceProvider)` implementation — no class-level marker required.
- New `IAutoInject : IInjectable` marker interface replaces the attribute form. It is
  implemented by every generated chain-starting partial; a memberless class may declare it
  in source to opt its hierarchy in (virtual root `Inject` + `OnInjected` hook).
- `IInjectable` stays the resolution contract used by `AddInjector()` /
  `IInjectorServiceProvider`; generated types satisfy it via `IAutoInject`.
- Re-listing `IAutoInject` on a derived class (legal C#) gives that level its own
  chain-through `override` (own guard + hook); without it a memberless level is transparent.

### Injection chains fixed (memberless intermediate bug)

- `Inject()` chains through the whole base chain. Intermediate levels with no `[Inject]`
  members and no `IAutoInject` are transparent: they emit nothing, and derived classes
  override the nearest generated ancestor `Inject` through them (`base.Inject` reaches the
  ancestor's wiring). Previously such levels broke the chain with CS0114 and silently
  skipped the ancestor's injections.
- Sealed semantics documented: sealed chain start → plain non-virtual `public void Inject`
  (`virtual` is forbidden in sealed classes, CS0549); sealed chained class → still
  `override`.

### Idempotency is unconditional

- `Inject()` wires an instance exactly once (per-level guard field); there is no opt-out.
  Re-wiring the same instance across scopes would capture scoped services into a
  long-lived instance (DI anti-pattern) — create a fresh instance per scope.

### Removed / changed

- ⚠️ `InjectableAttribute` removed — delete `[Injectable]` usages (members anchor now) or
  switch memberless roots to `: IAutoInject`.
- ⚠️ `Reinjectable` removed — `[Injectable(Reinjectable = true)]` no longer compiles
  (CS0617); injection is always idempotent.
- ⚠️ `DIG0009` (missing class-level opt-in) removed; `[Inject]` outside a marked hierarchy
  is now a valid chain start. Diagnostic IDs are never reused.
- Generator entry moved from attribute anchoring to a syntax-driven candidate predicate
  (`[Inject]` member / `IAutoInject` base list), with deterministic canonical-part dedupe.
- Fix: nullable partial `[Inject]` properties now emit nullability-preserving backing
  field/accessor declarations (previously the generated accessor dropped `?` → CS9256/
  CS8601).
- Fix: the `Δinjected` guard now has commit semantics and each level is all-or-nothing —
  members are resolved into buffer locals first and assigned only when every resolution on
  the level succeeded. A throwing `Inject()` (fail-fast) leaves that level untouched and
  re-injectable, so the same instance can be retried after the provider is fixed
  (previously the guard was set first / members were assigned one-by-one, leaving a
  half-wired instance on failure).

- Generator-reserved members now use the `Δ` prefix (U+0394): the idempotency guard
  `Δinjected` and partial-property backing fields `Δinjected_*`. User partial code can
  neither collide with nor accidentally reference them (generated files state this in a
  header comment); the `__` prefix is free for user conventions.

- New diagnostic DIG0018 (Error) + codefix: a hand-written, non-virtual
  `IInjectable.Inject` on a non-sealed class — an open class declares it may be derived, so
  it must not block its derived classes' injection channel. The codefix adds `virtual`
  ("Make Inject virtual"). Sealed classes, abstract, virtual, and override
  implementations are exempt.

- New diagnostic DIG0019 (Error) + codefix: `IInjectable.Inject` **explicitly**
  implemented on a non-sealed class — explicit implementations can never be overridden, so
  they strangle derived `[Inject]` classes exactly like non-virtual implicit ones (DIG0018).
  The codefix converts to an implicit `public virtual Inject` ("Convert to implicit virtual
  Inject"). Sealed classes are exempt.

### Examples

- `examples/Everlong.DI.Dogfood` — dogfood console app with 21 runtime checks across
  member-injection chains (transparent memberless middle), sealed shapes, re-listed
  `IAutoInject`, nullable/keyed injection, registration attributes, and scope detection.
  `EmitCompilerGeneratedFiles` is on so generated sources can be inspected.

### Migrating (v0.3.0 → v2)

| v0.3.0 | v2 |
|---|---|
| `[Injectable] public partial class C { [Inject] … }` | drop the attribute line — identical output |
| `[Injectable]` on a memberless class | drop it → transparent; or `: IAutoInject` → empty virtual root + hook |
| `[Injectable(Reinjectable = true)]` | remove (CS0617) |
| memberless `: IInjectable`, no members | add `IAutoInject`/members or implement `Inject()` by hand (CS0535) |
| `DIG0009` suppression in analyzer config | remove the entry (rule gone) |

## v0.3.0 — 2026-08-28

- `IScopeMarker` scope detection: `services.AddScopeMarker()` + `IsScoped()` /
  `IsRootScope` on the service provider (root vs child scope, `ValidateScopes`-safe).
- `AddInjector()` composite (`IInjectorServiceProvider` + `IInjector`) documented; wrapper
  requires `IKeyedServiceProvider`.
- SKILL diagnostic table completed (DIG0001/DIG0002 registration).

## v0.2.1 — 2026-08-12

- Fix: multi-partial types no longer depend on file path ordering — canonical selection
  no longer sorts by `SyntaxTree.FilePath` (a relative/absolute path mix could silently
  skip generation, CS9248).

## v0.2.0 — 2026-08-08

- Keyed service registration for `[Singleton]` / `[Scoped]` / `[Transient]`
  (string / int / Type / enum keys), keyed `[Inject]` resolution.
- `[AlsoAs<T>]` shared-instance forwarding (keyed + enumerable variants).
- Registration-rule diagnostics DIG0011–DIG0017 (also-as shape validation,
  lifetime/self-vs-generic mixing rules).
- Docs split: member injection vs service registration; SKILL/README/AGENTS aligned.

## v0.1.0 — 2026-08-04

- Initial release. Member injection (`[Injectable]`/`[Inject]`, `IInjectable`, partial
  properties, `OnInjected`, `Reinjectable`) and service registration
  (`[Singleton]`/`[Scoped]`/`[Transient]`/`[ServiceRegistrar]`, `AddInjector` wrapper),
  all AOT-safe source-generation.
