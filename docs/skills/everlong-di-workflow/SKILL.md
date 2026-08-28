---
name: everlong-di-workflow
description: Use Everlong.DI correctly — member injection ([Injectable]/[Inject], keyed members, auto-inject via AddInjector) and attribute-based service registration ([Singleton]/[Scoped]/[Transient]/[AlsoAs]/[ServiceRegistrar], keyed and enumerable variants). Avoid null-injected-member surprises, torn-lifetime registration traps, and mis-registered services.
---

## 0. Know the Contract Before You `[Inject]`

Everlong.DI generates an `Inject(IServiceProvider)` method at compile time — it does **not** hook into `BuildServiceProvider()` or any DI container's resolution pipeline automatically. Someone has to **call** `Inject()` before the injected members are usable.

| Resolution pattern | Who calls `Inject()` | Typical usage |
|---|---|---|
| **Manual** | You, after `GetService<T>()` / `new()` | Console apps, workers, tests, any place without a framework interceptor |
| **Wrapper SP** | Built-in `InjectorServiceProvider` registered via `services.AddInjector()` | When you want auto-injection on every resolve without manual `Inject()` calls |
| **Framework interceptor** | An IoC container extension or base class that hooks into activation | ASP.NET, Avalonia, WPF with custom infrastructure |

**Before writing `[Injectable] partial class Foo : IInjectable`**, decide which caller will invoke `Inject()`. If none, every injected member stays `null` at runtime and the code compiles fine — silent failure.

---

## 1. Core Rules (Member Injection)

### 1.1 `[Injectable]` + `partial` required; `IInjectable` recommended.

```csharp
[Injectable]                              // triggers the source generator
public partial class MyService : IInjectable  // partial for generated code, IInjectable for the contract
{
    [Inject] private ILogger _logger;
}
```

Missing any one of the three:

| Missing | Result |
|---------|--------|
| `[Injectable]` | Generator skips the class. `IInjectable.Inject()` is not implemented → CS0535 |
| `partial` | Generator skips the class — non-partial classes are filtered out before generation. If you declared `IInjectable`, its `Inject()` stays unimplemented → CS0535; with `[Inject]` properties the analyzer additionally flags DIG0007 |
| `IInjectable` | Nothing breaks — the generator appends `: IInjectable` to the generated partial itself. Declaring it explicitly is still recommended: it documents the contract and keeps the class usable even without generated code |
| All present | Generator produces an `Inject(IServiceProvider)` implementation with an idempotency guard and an `OnInjected()` partial hook. The exact modifier depends on the class shape — see §1.8 |

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
// → __injected_Logger = services.GetRequiredService<ILogger>();
// → public partial ILogger Logger => __injected_Logger;
```

Partial properties are preferred for read-only public surface. Fields are simpler and work on any LangVersion.

### 1.5 Nullable members use `GetService<T>()` (safe); non-nullable use `GetRequiredService<T>()`.

```csharp
[Inject] private ILogger? _logger;   // → GetService<ILogger>() — returns null if not registered
[Inject] private ILogger _logger;    // → GetRequiredService<ILogger>() — throws if not registered
```

Mark a member as nullable when the service is optional. Otherwise, let `GetRequiredService` fail fast on misconfiguration.

### 1.6 `Reinjectable` controls whether `Inject()` can be called multiple times.

By default, `Inject()` is idempotent — the first call assigns all members and subsequent calls do nothing. This is safe for singletons:

```csharp
[Injectable]  // Reinjectable defaults to false
public partial class MySingleton : IInjectable
{
    [Inject] private ILogger _logger;
}
// Generated Inject() body:
// if (__injected) return;
// __injected = true;
// this._logger = services.GetRequiredService<ILogger>();
// OnInjected();
```

Set `Reinjectable = true` when you need re-assignment on every call (e.g. transient instances resolved into a new scope each time):

```csharp
[Injectable(Reinjectable = true)]
public partial class MyTransient : IInjectable
{
    [Inject] private ILogger _logger;
}
// Generated Inject() — no guard, always re-assigns
```

### 1.7 `OnInjected()` — partial hook called after all members are assigned.

Every generated `Inject()` method ends with a call to `partial void OnInjected()`. Implement it in your class to run custom logic after injection:

```csharp
[Injectable]
public partial class MyService : IInjectable
{
    [Inject] private ILogger _logger;

    partial void OnInjected()
    {
        _logger.LogInformation("Injection complete");
    }
}
```

`OnInjected()` is only called when injection actually runs — the idempotency guard short-circuits before it. For `Reinjectable = false` classes it therefore runs exactly once, on the first `Inject()` call; for reinjectable classes it runs on every call.

Types may be split across multiple partial files (e.g. shared logic in one file, platform-specific members in another). `[Injectable]` must appear on exactly **one** part (it is not `AllowMultiple` — two parts carrying it is CS0579); `[Inject]` members may live on any part. Generation is driven by the `[Injectable]` hit, never by file path ordering.

### 1.8 Inheritance — `Inject()` chains through base classes.

If the immediate base class is injectable (implements `IInjectable` or declares its own `[Inject]` members), the generated method is emitted as `public override` and calls `base.Inject(services)` first:

```csharp
[Injectable]
public partial class BaseService : IInjectable
{
    [Inject] private ILogger _logger;
}

[Injectable]
public partial class DerivedService : BaseService
{
    [Inject] private IHttpClientFactory _http;
}
// Generated DerivedService.Inject():
// public override void Inject(IServiceProvider services)
// {
//     base.Inject(services);
//     this._http = services.GetRequiredService<IHttpClientFactory>();
//     OnInjected();
// }
```

Each class in the hierarchy gets its own idempotency guard and its own `OnInjected()` call; `Reinjectable` is read from the class's own `[Injectable]` attribute. On a `sealed` class the generated method is a plain `public void Inject(...)` — neither `virtual` nor `override`.

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
| `[Inject]` on a `readonly` field | Compile-time error DIG0008 (the generator also skips the class, leaving the interface contract unimplemented → CS0535). Use partial property injection instead.
| Using `Reinjectable = false` (default) for a transient that gets re-injected into different scopes | The idempotency guard means members from the first injection are never updated. Either set `Reinjectable = true` or re-create the instance. |
| `[Injectable]` on a non-partial class | Generator cannot inject code → compilation error. The analyzer catches this at IDE time (DIG0007 / DIG0009). |
| `[Inject]` members on a class without `[Injectable]` | Compile-time error DIG0009. The analyzer requires `[Injectable]` on any type that declares `[Inject]` members. |
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
| DIG0003 | Error | More than one `[ServiceRegistrar]` per assembly |
| DIG0004 | Error | Non-partial `[Inject]` property has no setter |
| DIG0005 | Error | `[Inject]` member is `static` |
| DIG0006 | Error | `init`-only `[Inject]` property is not `partial` |
| DIG0007 | Error | Class with `[Inject]` members is not `partial` |
| DIG0008 | Error | `[Inject]` on a `readonly` field |
| DIG0009 | Error | `[Inject]` members without `[Injectable]` |
| DIG0010 | Info | Prefer partial property over field injection |
| DIG0011 | Error | `[AlsoAs]` without a main registration |
| DIG0012 | Error | `[AlsoAs]` on a transient main registration |
| DIG0013 | Error | `[AlsoAs]` with multiple main registrations |
| DIG0014 | Error | `[AlsoAs]` on an enumerable main registration |
| DIG0015 | Error | Self and generic registrations mixed in one lifetime |
| DIG0016 | Error | Multiple lifetimes on one type |
| DIG0017 | Error | AlsoAs type is not an interface implemented by the class |

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
