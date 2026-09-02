# Everlong.DI

**Everlong.DI** is a lightweight attribute-based DI library for .NET, built on source generators — no reflection overhead at runtime. It covers **two independent mechanisms**:

- **Member Injection** (`[Inject]` members; `IAutoInject`/`IInjectable` markers) — push services *into* an object after it exists.
- **Service Registration** (`[Singleton]` / `[Scoped]` / `[Transient]` / `[AlsoAs]` / `[ServiceRegistrar]`) — declare which types go *into* the container.

They solve different problems and never interact: registering a class does not inject its members, and injecting members does not register anything. Pick the parts you need.

> Release history: [`docs/CHANGELOG.md`](docs/CHANGELOG.md).
> ⚠️ Upgrading from ≤ v0.3.0? v0.4.0 (v2) removed `[Injectable]` and `Reinjectable` —
> `[Inject]` members are the anchor now; the migration table lives in `docs/CHANGELOG.md`.
> Working example: [`examples/Everlong.DI.Dogfood`](examples/Everlong.DI.Dogfood) (21 runtime checks).

```
dotnet add package Everlong.DI
```

---

## Two mechanisms, two scenarios

| | **Member Injection** | **Service Registration** |
|---|---|---|
| Problem | An object already exists and its dependencies are `null` | Types must be declared to the container at startup |
| Typical scene | Manually created objects, framework-hosted objects, lazy/optional wiring | Application composition root, plugin/batch registration |
| Core types | `[Inject]`, `IInjectable`, `IAutoInject`, `IInjectorServiceProvider` | `[Singleton<T>]`, `[Scoped<T>]`, `[Transient<T>]`, `[AlsoAs<T>]`, `[ServiceRegistrar]` |
| What the generator produces | `Inject(IServiceProvider)` bodies | `RegisterServices(IServiceCollection)` bodies |
| Runtime hook | Someone must call `Inject()` (manual, wrapper SP, or framework interceptor) | `services.AddServices(new MyRegistrar())` |

---

# Part A — Member Injection

## Quick start

```csharp
using Everlong.DI;

// v2: there is no class-level attribute — a partial class with [Inject] members gets an
// Inject() implementation generated on its own. IAutoInject (the v2 marker, derives from
// IInjectable) is implemented by every generated chain-starting class; declare it on a
// memberless framework base to opt the hierarchy in.
public partial class MyService : IAutoInject
{
    [Inject] private ILogger _logger;                       // field injection
    [Inject] public ISomeService Service { get; set; }      // property injection
}
```

The generator produces:

```csharp
public virtual void Inject(IServiceProvider services)
{
    _logger = services.GetRequiredService<ILogger>();
    Service = services.GetRequiredService<ISomeService>();
}
```

## Who calls `Inject()`?

| Pattern | Usage |
|---|---|
| **Manual** | `var svc = sp.GetRequiredService<MyService>(); svc.Inject(sp);` — console apps, workers, tests |
| **Auto-inject wrapper** | `services.AddInjector();` then resolve through `IInjectorServiceProvider` — every resolved `IInjectable` is injected automatically |
| **Framework interceptor** | Your own IoC integration calls `Inject()` during activation |

`AddInjector()` registers two services: `IInjectorServiceProvider` (scoped by default; pass `ServiceLifetime.Singleton` to change) and `IInjector` (forwarded to the wrapper). The wrapper implements `IKeyedServiceProvider`, so keyed resolves get injected too.

## Details worth knowing

- **Nullable members are optional**: `[Inject] ILogger? _logger` → `GetService<T>()` (returns `null`); non-nullable → `GetRequiredService<T>()` (throws). Fail fast by default.
- **Keyed injection**: `[Inject("cache")]`, `[Inject(42)]`, `[Inject(typeof(TKey))]`, `[Inject(SomeEnum.X)]` → `GetRequiredKeyedService<T>(key)` (requires .NET 8+ container).
- **Idempotency is unconditional**: `Inject()` wires an instance exactly once (guard field); later calls are no-ops. There is no opt-out — re-wiring the same instance across scopes would capture scoped services into a long-lived instance (a DI anti-pattern).
- **`partial void OnInjected()`**: runs after every successful injection. It only exists on generated classes — implement `IAutoInject` on a memberless base if you want a hook with no members.
- **Inheritance**: `Inject()` chains through the base chain (`override` + `base.Inject(services)`). Intermediate levels that declare no `[Inject]` members are transparent — no marker needed on them. **Sealed**: a sealed chain start gets a plain non-virtual `public void Inject` (nothing could override it — and C# forbids `virtual` in sealed classes); a sealed class in the middle of a chain still emits `override`.
- **Partial properties**: `[Inject] public partial ILogger Logger { get; }` generates a backing field — requires `<LangVersion>preview</LangVersion>`.
- **Split partial classes**: a type may be split across several partial files. `[Inject]` members may live on any part; generation is driven by members (or an `IAutoInject` marker), never by file path ordering.
- **XML docs on generated code**: generated `Inject()` / `RegisterServices()` carry `/// <inheritdoc/>` that inherits the contract docs from `IInjectable` / `IServiceRegistrar` — projects enabling `GenerateDocumentationFile` get no CS1591 on Everlong.DI-generated files.
- **Scope detection**: `services.AddScopeMarker();` registers `IScopeMarker` (scoped). `provider.IsScoped()` tells you whether a provider is a child scope — `false` for the root provider, for providers without the marker, and under `ValidateScopes`; the marker itself exposes `IsRootScope`.

---

# Part B — Service Registration

## Quick start

```csharp
using Everlong.DI;

[Singleton]                        // register the class itself
public partial class CacheService;

[Singleton<IOrderService>]         // register as a service type
public partial class OrderService : IOrderService;

[Scoped<IRepository>("tenant:eu")] // keyed registration
public partial class Repository : IRepository;

[ServiceRegistrar]                 // one per assembly — generator fills RegisterServices()
public partial class MyRegistrar : IServiceRegistrar;
```

Wire it up:

```csharp
var services = new ServiceCollection();
services.AddServices(new MyRegistrar());
```

The generator emits for each annotated type a `TryAdd`/`Add` + `ServiceDescriptor` call, guarded by `ServiceRegistrarHelper` validation (AOT-safe, see below).

## Registration attributes

| Attribute | Meaning |
|---|---|
| `[Singleton]` / `[Scoped]` / `[Transient]` | Register the class itself (self) |
| `[Singleton<T>]` / `[Scoped<T>]` / `[Transient<T>]` | Register as service type `T` — repeatable, each registration gets its own instance |
| `[AlsoAs<T>]` | Add a **shared view** of the single instance registered by the main `[Singleton]`/`[Scoped]` registration |
| `[ServiceRegistrar]` | Marks the partial class that hosts the generated `RegisterServices()` |

Constructor arguments, on every attribute: `(key?, enumerable?)`.

```csharp
[Singleton<IFoo>(isEnumerable: true)]        // multiple implementations, resolved as IEnumerable<IFoo>
[Singleton<IFoo>("tenant:eu")]               // keyed — resolve with GetRequiredKeyedService<IFoo>("tenant:eu")
[Singleton<IFoo>("tenant:eu", isEnumerable: true)]
```

Keyed and unkeyed registrations are independent spaces — `[Singleton<IFoo>]` + `[Singleton<IFoo>("k")]` on one class is legal.

## `[AlsoAs<T>]` — one instance, several faces

```csharp
[Singleton]                    // main registration — the single instance
[AlsoAs<ICache>]               // ICache resolves to the same instance
[AlsoAs<IMetric>("metrics")]   // keyed view of the same instance
public partial class Cache : ICache, IMetric;

var cache = sp.GetRequiredService<ICache>();
var metric = sp.GetRequiredKeyedService<IMetric>("metrics");
ReferenceEquals(cache, metric);  // true
```

The main registration can be `[Singleton]`, `[Scoped]`, or a single generic variant (`[Singleton<T>]`, `[Scoped<T>]`). Lifetime follows the main.

## Composition rules (enforced by the generator)

| Rule | Error |
|---|---|
| One lifetime per type — no cross-lifetime mixes | DIG0016 |
| Self and generic registrations are mutually exclusive within a lifetime — share via `[AlsoAs]`, get independence via multiple `[Singleton<T>]` | DIG0015 |
| `[AlsoAs]` needs exactly one non-transient, non-enumerable main registration | DIG0011–0014 |
| `[AlsoAs]` type must be an interface the class implements | DIG0017 |
| Duplicate registrations are allowed (`TryAdd` first-wins, `Add` accumulates) | — |

See `docs/skills/everlong-di-workflow/SKILL.md` for the full workflow, diagnostics reference, and red lines.

---

## Requirements

- .NET 8+
- `<LangVersion>preview</LangVersion>` in the consuming project (partial-property injection only; field injection works on any modern LangVersion):

  ```xml
  <PropertyGroup>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>
  ```

- The source generator itself targets `netstandard2.0` and works with any modern Roslyn version.

---

## AOT / Trimming

`ServiceRegistrarHelper.EnsureConcreteType<T>()` / `VerifyImplementation<TService, TImpl>()` carry `[DynamicallyAccessedMembers]` so constructors survive trimming. Generated injection resolves via direct `GetRequiredService<T>()` calls — no reflection in the hot path.

---

## Build & Pack

```bash
dotnet build -c Release          # MUST build Release first — the nupkg embeds the
                                 # Generators/CodeFixers DLLs from bin/Release via loose
                                 # file includes; `dotnet pack` alone would pack stale ones
dotnet pack src/Everlong.DI -c Release --no-build
```

Package is produced under `src/Everlong.DI/bin/Release/`.

**Verify the packed artifact** (package-level regression net): push the nupkg to any local folder
feed, then run the SmokeTest — it consumes Everlong.DI as a real NuGet package (not a
ProjectReference) and must print `All OK!`:

```bash
dotnet nuget push src/Everlong.DI/bin/Release/Everlong.DI.<version>.nupkg --source <feed-path>
rm -rf "$HOME/.nuget/packages/everlong.di"   # drop the cached copy first
# SmokeTest's committed NuGet.Config lists nuget.org only and clears other sources, so the
# pre-release feed is passed per restore (no config file is modified or committed):
cd tests/Everlong.DI.SmokeTest
dotnet run -p:RestoreAdditionalProjectSources="<feed-path>"   # -> All OK!
```

After a release, SmokeTest verifies the published nuget.org package with no extra config.

---

## Project Structure

```
Everlong.DI/
├── EverlongDI.props
├── README.md
├── docs/
│   └── skills/                          # AI coding assistant skill definitions
├── src/
│   ├── Everlong.DI/                     # Contracts, attributes, service provider (packable)
│   ├── Everlong.DI.Generators/          # Source generator + analyzers (embedded)
│   └── Everlong.DI.CodeFixers/          # Roslyn code fixers (embedded)
└── tests/
    ├── Everlong.DI.Tests/               # Unit tests + snapshot tests (Verify)
    ├── AssemblyA/                       # Manual end-to-end verification project
    └── Everlong.DI.SmokeTest/           # Package-level consumer smoke (pack → consume)
```

---

## License

MIT
