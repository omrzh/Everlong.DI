---
name: everlong-di-workflow
description: Use Everlong.DI correctly — member injection ([Inject] members / IAutoInject chains, keyed members, auto-inject via AddInjector) and attribute-based service registration ([Singleton]/[Scoped]/[Transient]/[AlsoAs]/[ServiceRegistrar], keyed and enumerable variants). Avoid null-injected-member surprises, torn-lifetime registration traps, and mis-registered services.
---

## 0. Know the Contract Before You `[Inject]`

Everlong.DI generates an `Inject(IServiceProvider)` method at compile time — it does **not** hook into `BuildServiceProvider()` or any DI container's resolution pipeline automatically. Someone has to **call** `Inject()` before the injected members are usable.

| Resolution pattern | Who calls `Inject()` | Typical usage |
|---|---|---|
| **Manual** | You, after `GetService<T>()` / `new()` | Console apps, workers, tests, any place without a framework interceptor |
| **Wrapper SP** | Built-in `InjectorServiceProvider` registered via `services.AddInjector()` | When you want auto-injection on every resolve without manual `Inject()` calls |
| **Framework interceptor** | An IoC container extension or base class that hooks into activation | ASP.NET, Avalonia, WPF with custom infrastructure |

**Before writing a class with `[Inject]` members (optionally implementing `IAutoInject`)**, decide which caller will invoke `Inject()`. If none, every injected member stays `null` at runtime and the code compiles fine — silent failure.

---

## 1. Core Rules (Member Injection)

> Every shape below is pinned by unit tests in
> `tests/Everlong.DI.Tests/DI/MemberInjectionGeneratorTests.cs`.

### 1.1 `[Inject]` members anchor generation; `IAutoInject` is the only class-level marker.

```csharp
public partial class MyService : IAutoInject   // IAutoInject = the v2 anchor (derives from IInjectable)
{
    [Inject] private ILogger _logger;
}

public partial class DerivedService : MyService // NO marker needed — [Inject] members are the anchor
{
    [Inject] private IHttpClientFactory _http;
}
```

Rules:

| Concern | Rule |
|---|---|
| **Anchor** | Any partial class that declares `[Inject]` members is a generation target. Chain-starting generated partials implement `IAutoInject` (hence `IInjectable`). |
| **`IAutoInject`** | Marker interface (derives from `IInjectable`). Declare it on a class — typically a memberless framework base — to give it a generated `Inject()` and make it a valid chain root even with zero members. Interfaces, unlike a base class, compose with any inheritance, so no attribute form exists. Re-listing it on a derived class (legal C# — only the base *class* cannot repeat, CS0263) opts that level into its own chain-through `override` (own guard + `OnInjected`); without the re-listing a memberless derived level is transparent (§1.8). |
| **`partial`** | Required on every injection target (the generator emits a partial declaration). Missing `partial` → the analyzer flags it (DIG0007) for property injection, and classes implementing `IAutoInject`/`IInjectable` without a generated implementation fail with CS0535. |
| **`IInjectable`** | The resolution contract: `Inject(IServiceProvider)`. The injector wrappers check this interface; generated types satisfy it via `IAutoInject`. Declaring it explicitly is optional — it documents the contract. Declaring `: IInjectable` **alone** (no `[Inject]` members, no `IAutoInject`) anchors nothing: the interface stays unimplemented → CS0535. |

`[Inject]` members may live on any partial part. Generation is driven by members / the `IAutoInject` marker, never by file path ordering.

### 1.2 `LangVersion` must be `preview` for partial properties.

Partial properties (`[Inject] public partial ILogger Logger { get; }`) require a C# feature that is only available with `<LangVersion>preview</LangVersion>` in the consuming project. Without it, the compiler rejects the partial property declaration.

If you cannot use preview language version, stick to field injection (`[Inject] private ILogger _logger;`) — that works on any LangVersion that supports attributes.

### 1.3 Injectable types must be resolved through the caller that calls `Inject()`.

```csharp
var sp = services.BuildServiceProvider();

// ❌ Wrong: Inject() is never called
var svc = sp.GetRequiredService<MyService>();
svc.Run();  // _logger is null → NullReferenceException

// ✅ Correct: call Inject() after resolve
var svc = sp.GetRequiredService<MyService>();
svc.Inject(sp);
svc.Run();

// ✅ Or: use the built-in auto-inject wrapper
services.AddInjector();  // registers IInjectorServiceProvider + IInjector (scoped by default)
var injector = services.BuildServiceProvider().GetRequiredService<IInjectorServiceProvider>();
var svc = injector.GetRequiredService<MyService>();  // Inject() called automatically
```

`AddInjector()` registers two services: `IInjectorServiceProvider` (a composite of `IKeyedServiceProvider` + `IInjector`, scoped by default — pass `ServiceLifetime.Singleton` to change) and `IInjector`. The wrapper requires the underlying provider to implement `IKeyedServiceProvider` (the standard MS container does since .NET 8) and throws `ArgumentException` otherwise. Every `IInjectable` resolved through the wrapper — keyed or not — gets `Inject()` called automatically.

### 1.4 `[Inject]` on a field generates direct assignment; on a partial property generates a backing field.

```csharp
[Inject] private ILogger _logger;
// → this._logger = services.GetRequiredService<ILogger>();

[Inject] public partial ILogger Logger { get; }
// → Δinjected_Logger = services.GetRequiredService<ILogger>();
// → public partial ILogger Logger => Δinjected_Logger;
```

Partial properties are preferred for read-only public surface. Fields are simpler and work on any LangVersion.

### 1.5 Nullable members use `GetService<T>()` (safe); non-nullable use `GetRequiredService<T>()`.

```csharp
[Inject] private ILogger? _logger;   // → GetService<ILogger>() — returns null if not registered
[Inject] private ILogger _logger;    // → GetRequiredService<ILogger>() — throws if not registered
```

Mark a member as nullable when the service is optional. Otherwise, let `GetRequiredService` fail fast on misconfiguration.

### 1.6 `Inject()` is idempotent — unconditionally.

The first call assigns all members; subsequent calls return immediately (guard field
`Δinjected`). There is no opt-out (v2 removed `Reinjectable`): an instance is wired
exactly once per lifetime. Re-wiring the same instance across scopes would capture
scoped services into a long-lived instance — a DI anti-pattern — so the correct move
for a fresh scope is a fresh instance.

Generated internal members (the guard field, partial-property backing fields like
`Δinjected_Service`) carry the reserved `Δ` prefix (U+0394, Greek capital Delta): user
partial code cannot collide with them or reference them by accident (typing Δ requires
copying the generated name), and the ordinary `__` prefix stays free for your own
conventions. You never need to read them — `OnInjected()` only runs after the level
committed, so the guard is always `true` there.

The guard has **commit semantics**, and each level is **all-or-nothing**: members are
first resolved into buffer locals; only when every resolution on the level succeeded are
the members assigned and `Δinjected` set. If the first `GetRequiredService` succeeds and
the second throws, **nothing at that level has been assigned** — the instance is exactly
as it was, and the same instance can be `Inject()`-ed again after the provider is fixed.

```csharp
public partial class MySingleton : IAutoInject
{
    [Inject] private ILogger _logger;
}
// Generated Inject() body:
// if (Δinjected) return;
// global::…ILogger __inject_value_0 = services.GetRequiredService<ILogger>();  // may throw → nothing assigned
// this._logger = __inject_value_0;
// Δinjected = true;                            // commit only after every resolution succeeded
// OnInjected();                                 // re-entrant Inject() here is a no-op
```

Note: earlier chain levels that already committed stay wired if a later level fails — each
level commits independently (per-level, not whole-object, atomicity).

### 1.7 `OnInjected()` — partial hook called after all members are assigned.

Every generated `Inject()` method ends with a call to `partial void OnInjected()`. Implement it in your class to run custom logic after injection:

```csharp
public partial class MyService : IAutoInject
{
    [Inject] private ILogger _logger;

    partial void OnInjected()
    {
        _logger.LogInformation("Injection complete");
    }
}
```

`OnInjected()` is only called when injection actually runs — the idempotency guard short-circuits before it, so the hook fires exactly once, on the first `Inject()` call. A memberless class gets a usable hook only when it opts in with `IAutoInject`: marking it generates an empty `Inject` whose only job is the hook.

Types may be split across multiple partial files (e.g. shared logic in one file, platform-specific members in another). `[Inject]` members may live on any part. Generation is driven by `[Inject]` members / the `IAutoInject` marker, never by file path ordering.

### 1.8 Inheritance — `Inject()` chains through the base chain.

If the base chain (starting at the direct base, walking to `System.Object`) exposes an
`Inject` — an ancestor that implements `IAutoInject`/`IInjectable`, or declares its own
`[Inject]` members — the generated method is an `override` that
calls `base.Inject(services)` first:

```csharp
public partial class BaseService : IAutoInject
{
    [Inject] private ILogger _logger;
}

public partial class DerivedService : BaseService     // no marker of its own
{
    [Inject] private IHttpClientFactory _http;
}
// Generated DerivedService.Inject():
// public override void Inject(IServiceProvider services)
// {
//     if (Δinjected) return;
//     Δinjected = true;
//     base.Inject(services);
//     this._http = services.GetRequiredService<IHttpClientFactory>();
//     OnInjected();
// }
```

Key behaviors:

- **Intermediate levels are transparent.** A class between two injectable levels that has
  no `[Inject]` members and no `IAutoInject` generates nothing; derived classes override
  the nearest generated ancestor `Inject` *through* it and `base.Inject` reaches the
  ancestor's wiring. This is what fixes memberless-chain breaks: no per-level marker
  bookkeeping.
- **Marked memberless levels still get their own level.** A memberless class declaring
  `IAutoInject` with an injectable ancestor emits a
  chain-through `public override Inject` (guard → `base.Inject` → `OnInjected`) so its own
  `OnInjected()` hook fires.
- **Chain starts** (no injectable ancestor) emit `public virtual Inject` and the generated
  partial declares `: IAutoInject`. A memberless chain start emits an empty virtual
  `Inject` (guard + `OnInjected`), giving the marker meaning even with no members.
- **Sealed** classes:
  - sealed + chain start → plain `public void Inject(...)` — **not virtual**, because a
    sealed class cannot be derived, so a virtual method could never be overridden; C# also
    forbids `virtual` in sealed classes (CS0549).
  - sealed + existing chain → still `public override` (overriding is legal in sealed
    classes; only *declaring* `virtual` is not). Virtual-ness dies with the sealed class.
- **Compiled ancestors** carry the generated interface and a virtual `Inject` in metadata, so chains are also discovered across assembly boundaries (no same-compilation visibility problem there).
- **Hand-written `IInjectable` bases** interop on two conditions: the manual `Inject` must be
  declared `virtual` (or `abstract`) — a plain `public void Inject` is non-virtual, and the
  generated derived `override` then fails with CS0506 — and the class must stay on
  `IInjectable`, **not** `IAutoInject` (declaring the anchor while hand-implementing `Inject`
  makes the generator emit a duplicate root → CS0111). Manual levels get none of the
  machinery (no `Δ` guard, no two-phase buffering, no `OnInjected`) — that is the trade for
  not using the generator. If a manual override is itself chained, remember to call
  `base.Inject(services)` or the generated ancestor's wiring is skipped. The analyzer
  flags the non-virtual case (DIG0018) with a one-click "Make Inject virtual" codefix.
- Each class in the hierarchy gets its own idempotency guard and its own `OnInjected()`
  call (§1.6/§1.7).

### 1.9 Scope detection — `AddScopeMarker` / `IsScoped`.

Register the marker to make scope-ness observable at runtime:

```csharp
services.AddScopeMarker();              // registers IScopeMarker as scoped
var root = services.BuildServiceProvider();
root.IsScoped();                        // false — root provider
using var scope = root.CreateScope();
scope.ServiceProvider.IsScoped();       // true — child scope
```

`IsScoped()` returns `false` for the root provider, for providers without the marker registered, and when scoped resolution from the root throws under `ValidateScopes`. The marker exposes `IsRootScope` for the raw signal. Detection relies on MS DI resolving `IServiceScopeFactory` against the root scope and `IServiceProvider` against the current scope — keep the default MS container and register the marker only via `AddScopeMarker()`.

---

## 2. Service Registration Attributes

### 2.1 Registration is a separate mechanism from injection.

`[Singleton]` / `[Transient]` / `[Scoped]` (and their generic, keyed, and `[AlsoAs]` variants) declare *registration*: they are pure metadata until a `[ServiceRegistrar]` partial class turns them into `RegisterServices()` calls. Registration and injection never interact — a class can use either mechanism, both, or neither. Resolving through a raw `IServiceProvider` does **not** call `Inject()` (§0); registering a class does **not** make its members injected.

### 2.2 `[ServiceRegistrar]` is the single entry point per assembly.

Exactly one `[ServiceRegistrar]` partial class per assembly (DIG0003). The generator implements `RegisterServices(IServiceCollection)` and every annotated type in the same assembly is registered:

```csharp
[Singleton<IMyService>]
public partial class MyService : IMyService;

[ServiceRegistrar]
public partial class MyRegistrar : IServiceRegistrar;

// Usage:
services.AddServices(new MyRegistrar());
```

### 2.3 Generic vs non-generic variants.

```csharp
[Singleton]                  // → typeof(MyService) → typeof(MyService)   (self)
[Singleton<IMyService>]      // → typeof(IMyService) → typeof(MyService)  (service type)
```

Choose the generic form when callers depend on the interface. Use the non-generic form when only the concrete class is resolved directly. A class may carry several generic registrations (`[Singleton<IFoo>]` + `[Singleton<IBar>]`) — each gets its own independent instance.

### 2.4 `isEnumerable` and `key` are constructor arguments — not named properties.

```csharp
[Singleton<IMyService>(isEnumerable: true)]            // ✅ constructor argument
[Singleton<IMyService>(true)]                          // ✅ positional
[Singleton<IMyService>(IsEnumerable = true)]           // ❌ IsEnumerable is get-only
```

`isEnumerable: true` switches `TryAdd` to `Add`, allowing several implementations of the same service type, resolved as `IEnumerable<T>`.

Keyed registrations take a key as the first constructor argument (string, int, Type, or enum — the same key set as `[Inject]`):

```csharp
[Singleton<ICache>("tenant:eu")]                              // keyed
[Singleton("self-key")]                                       // keyed self registration
[Singleton<ICache>("tenant:eu", isEnumerable: true)]          // keyed + enumerable
```

Keyed and unkeyed registrations live in separate spaces: `[Singleton<IFoo>]` + `[Singleton<IFoo>("k")]` on one class is legal and both resolve. Resolve keyed services with `GetRequiredKeyedService<T>(key)` / `GetKeyedServices<T>(key)`.

### 2.5 One type = one registration family.

A class may only carry registrations from a **single lifetime**:

| Mix | Result |
|---|---|
| `[Singleton]` + `[Scoped]`, `[Singleton<IFoo>]` + `[Scoped<IBar>]`, any cross-lifetime mix | Error DIG0016 |
| `[Singleton]` + `[Singleton<IFoo>]` (self + generic in the same lifetime) | Error DIG0015 |

The intent behind `[Singleton]` + `[Singleton<IFoo>]` is almost always "one instance, two faces" — which is exactly what `[AlsoAs]` expresses (§2.7). For genuinely independent instances, use multiple generic registrations.

Duplicate registrations (e.g. writing `[Singleton<IFoo>]` twice) are **allowed and not errors**: `TryAdd` keeps the first, `Add` accumulates. The generic attributes are `AllowMultiple`; the non-generic ones are not.

### 2.6 `[AlsoAs<T>]` — shared views of one instance.

`[AlsoAs<T>]` adds a shared view of the single instance created by the **main registration**. Exactly one main registration is required: a self or generic `[Singleton]` / `[Scoped]`.

```csharp
[Singleton]                 // main: self registration (concrete class)
[AlsoAs<IFoo>]              // IFoo resolves to the same instance
[AlsoAs<IBar>]              // IBar too
public partial class Foo : IFoo, IBar { … }

// Generated:
// services.TryAddSingleton<Foo>();
// services.TryAddSingleton<IFoo>(sp => sp.GetRequiredService<Foo>());
// services.TryAddSingleton<IBar>(sp => sp.GetRequiredService<Foo>());
```

The main may also be a generic registration; the forward then resolves the main service and defensively verifies it is the AlsoAs type (TryAdd claim races make a bare cast unsafe):

```csharp
[Singleton<IFoo>]           // main: IFoo registration
[AlsoAs<IBar>]
// generated: sp => { var s = sp.GetRequiredService<IFoo>(); return s is IBar b ? b : throw …; }
```

Rules:

| Rule | Diagnostics |
|---|---|
| `[AlsoAs]` without any main registration | DIG0011 |
| Main registration is `[Transient]` / `[Transient<T>]` (transient has no shareable instance) | DIG0012 |
| More than one main registration (e.g. `[Singleton<T1>]` + `[Singleton<T2>]` + `[AlsoAs]`) | DIG0013 |
| Main registration is enumerable (no single instance to resolve) | DIG0014 |
| AlsoAs type is not an interface implemented by the class | DIG0017 |

Lifetime follows the main registration (Singleton or Scoped). Each `[AlsoAs]` takes its own `(key?, enumerable?)`, so keyed and enumerable views compose:

```csharp
[Scoped]
[AlsoAs<ICache>("tenant:eu")]                     // keyed view
[AlsoAs<IMetric>(enumerable: true)]               // enumerable view, all entries = the same instance
public partial class Cache : ICache, IMetric { … }
```

---

## 3. Keyed Services

### 3.1 `[Inject]` accepts string, int, Type, or enum keys.

```csharp
[Inject("cache")]
[Inject(42)]
[Inject(typeof(MyKey))]
[Inject(SomeEnum.Fast)]
```

These generate `GetKeyedService<T>(key)` / `GetRequiredKeyedService<T>(key)` calls.

The DI container must support keyed services (`IKeyedServiceProvider`). The standard `Microsoft.Extensions.DependencyInjection` container supports keyed services starting from .NET 8.

### 3.2 A keyed inject on a nullable member uses `GetKeyedService` (returns null if not found); non-nullable uses `GetRequiredKeyedService` (throws).

Same rule as §1.5 — nullable → safe, non-nullable → fail fast.

### 3.3 Registration-side keys are declared on the attributes; they resolve in the same keyed space.

`[Singleton<ICache>("k")]` pairs with `[Inject("k")]` on the consuming side — the key is a plain object, so both sides just need to agree on the value. See §2.4 for the registration forms.

---

## 4. AOT / Trimming

### 4.1 `ServiceRegistrarHelper.EnsureConcreteType<T>()` and `VerifyImplementation<TService, TImpl>()` carry `[DynamicallyAccessedMembers]`.

These methods preserve public constructors during trimming / AOT compilation. The source generator emits calls to these helpers before every `ServiceDescriptor` registration. This ensures the linker sees the types as "used" and does not strip their constructors.

### 4.2 The generated `Inject()` body calls `GetRequiredService<T>()` directly — no reflection, no `MakeGenericMethod`.

Every injected member is resolved via a direct generic call emitted as source code. This is AOT-safe by construction.

---

## 5. Red Lines (Never Do This)

| Forbidden | Why |
|-----------|-----|
| `[Inject]` on a `readonly` field | Compile-time error DIG0008; the generator skips the whole class (no `Inject` at all — other valid members are not injected either). If the class declares `IAutoInject`/`IInjectable`, the missing implementation additionally fails with CS0535. Use partial property injection instead.
| Re-injecting the same instance into a different scope | Impossible by design: `Inject()` is unconditionally idempotent (§1.6). Create a fresh instance for the new scope instead of re-wiring a long-lived one — re-wiring would capture scoped services (DI anti-pattern). |
| An injection target is not `partial` (declares `[Inject]` members, or implements `IAutoInject`) | The generator cannot emit its partial declaration → nothing is generated. Property injection is flagged at IDE time (DIG0007); a source-declared `IAutoInject`/`IInjectable` without generated code fails with CS0535. Keep every target partial. |
| `IAutoInject` on a class that is never derived and has no `[Inject]` members | Generates an empty virtual root `Inject` (guard + `OnInjected()`). Harmless, but if you only wanted a silent marker, drop it — a memberless class without `IAutoInject` is transparent. |
| Calling `Inject()` before the service provider is ready | If the injected members depend on services that haven't been registered yet, `GetRequiredService<T>()` throws at injection time, not at usage time. That is correct behavior — fail fast. |
| Holding a scoped service in a field injected once into a singleton | The `[Inject]` is called once. If the target is a singleton, its injected scoped services are captured for the entire application lifetime. This is a DI anti-pattern — same as constructor-injecting a scoped service into a singleton. |
| Forgetting to register `IInjectorServiceProvider` when auto-inject is expected | `services.AddInjector()` must be called; otherwise `IInjectorServiceProvider` is not in the container and no auto-injection happens. |
| Mixing lifetimes on one class (`[Singleton]` + `[Scoped]`, `[Singleton<IFoo>]` + `[Scoped<IBar>]`) | Error DIG0016. One class = one lifetime; split the class or use `[AlsoAs]` for shared views. |
| Mixing self and generic registrations in one lifetime (`[Singleton]` + `[Singleton<IFoo>]`) | Error DIG0015. Shared instance → `[Singleton]` + `[AlsoAs<IFoo>]`; independent instances → multiple `[Singleton<T>]`. |
| `[AlsoAs<T>]` without a main, on a transient main, with several mains, on an enumerable main, or with a type the class does not implement | Errors DIG0011–0014, DIG0017. Exactly one non-transient, non-enumerable `[Singleton]`/`[Scoped]` main; `T` must be an implemented interface. |
| `[ServiceRegistrar]` on a non-partial class | The generator skips it silently; `RegisterServices()` is never implemented (CS0535 if you declared `IServiceRegistrar`). Keep the registrar class partial. |
| More than one `[ServiceRegistrar]` class per assembly | Generator error DIG0003 — exactly one registrar is allowed per assembly. |
| Re-registering `IScopeMarker` yourself instead of `AddScopeMarker()` | `IsScoped()` relies on the marker being registered exactly as `AddScopeMarker()` does (scoped, default MS container). A custom registration silently breaks the root-vs-scope signal. |
| Editing generated `*.Inject.g.cs` files | They're overwritten on every build. Change the source attributes instead. |

### 5.1 Diagnostic quick reference

| ID | Severity | Meaning |
|----|----------|---------|
| DIG0001 | Error | Generator internal error at transform phase (input, message, stack) — file a bug |
| DIG0002 | Error | Source Generator Exception at execution phase (input, message, stack) — file a bug |
| DIG0003 | Error | More than one `[ServiceRegistrar]` per assembly |
| DIG0004 | Error | Non-partial `[Inject]` property has no setter |
| DIG0005 | Error | `[Inject]` member is `static` |
| DIG0006 | Error | `init`-only `[Inject]` property is not `partial` |
| DIG0007 | Error | Class with `[Inject]` members is not `partial` |
| DIG0008 | Error | `[Inject]` on a `readonly` field |
| DIG0010 | Info | Prefer partial property over field injection |

> DIG0009 (`[Inject]` members without a class-level opt-in) was **removed in v2**: the
> class-level `[Injectable]` attribute no longer exists (`IAutoInject` is the marker
> interface), and `[Inject]` members alone are a generation target. Diagnostic IDs are
> never reused.
| DIG0011 | Error | `[AlsoAs]` without a main registration |
| DIG0012 | Error | `[AlsoAs]` on a transient main registration |
| DIG0013 | Error | `[AlsoAs]` with multiple main registrations |
| DIG0014 | Error | `[AlsoAs]` on an enumerable main registration |
| DIG0015 | Error | Self and generic registrations mixed in one lifetime |
| DIG0016 | Error | Multiple lifetimes on one type |
| DIG0017 | Error | AlsoAs type is not an interface implemented by the class |
| DIG0018 | Error | Hand-written `IInjectable.Inject` is not `virtual` on a non-sealed class — an open class must not block its derived classes' injection channel (codefix: make it virtual). Sealed classes are exempt |
| DIG0019 | Error | `IInjectable.Inject` explicitly implemented on a non-sealed class — explicit implementations can never be overridden, blocking derived `[Inject]` classes (codefix: convert to implicit `public virtual`). Sealed classes are exempt |

---

## 6. Testing

- Test that `Inject()` assigns members correctly: create an instance, call `Inject(sp)`, assert members are non-null.
- Test nullable injection: register a service as optional, inject with nullable type, verify `null` is handled gracefully.
- Test the `ServiceRegistrationGenerator` by creating the `[ServiceRegistrar]` partial class, calling `RegisterServices()` on it, building the `ServiceCollection`, and resolving the expected services.
- Test shared instances: resolve the main registration and every `[AlsoAs]` view, assert `ReferenceEquals` holds; for scoped mains, assert per-scope sharing and cross-scope isolation.
- Test keyed registrations: resolve via `GetRequiredKeyedService<T>(key)`, assert keyed and unkeyed spaces do not leak into each other.
- Use the real DI container (`ServiceCollection` + `BuildServiceProvider`) in tests — don't mock `IServiceProvider` unless you're testing the generator output in isolation.
- Test scope detection with the real container: root provider → `IsScoped()` false (with and without `ValidateScopes`), child scope (including nested scopes) → true; marker resolves to a shared instance within a scope and different instances across scopes.
- Snapshot tests (via Verify) are the standard way to validate generated source code — see `tests/Everlong.DI.Tests/DI/Snapshots/` for examples.
