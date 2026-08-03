---
name: everlong-di-workflow
description: Use Everlong.DI member injection correctly. Avoid "it compiles but the injected member is null" surprises.
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

## 1. Core Rules

### 1.1 `[Injectable]` + `partial` + `IInjectable` — all three required.

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
| `partial` | Generator can't emit the `Inject()` method into a separate file → CS0260 |
| `IInjectable` | No contract — the generated `Inject()` method is just a public method nobody is forced to call |
| All present | Generator produces `public virtual void Inject(IServiceProvider services) { … }` with an idempotency guard and an `OnInjected()` partial hook |

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
services.AddInjector();  // registers IInjectorServiceProvider
var injector = services.BuildServiceProvider().GetRequiredService<IInjectorServiceProvider>();
var svc = injector.GetRequiredService<MyService>();  // Inject() called automatically
```

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

`OnInjected()` is called whether or not the idempotency guard short-circuits. For `Reinjectable = false` classes, implement `OnInjected()` to run once; for reinjectable classes, it runs on every call.

---

## 2. Service Registration Attributes

### 2.1 `[Singleton]`, `[Transient]`, `[Scoped]` — meta-data only without `[ServiceRegistrar]`.

These attributes alone do nothing at runtime. They only become meaningful when accompanied by a `[ServiceRegistrar]` partial class that the `ServiceRegistrationGenerator` fills in:

```csharp
[Singleton<IMyService>]
public partial class MyService : IMyService;

[ServiceRegistrar]
public partial class MyRegistrar : IServiceRegistrar;

// Generated by SG:
// public partial class MyRegistrar : IServiceRegistrar
// {
//     public void RegisterServices(IServiceCollection services)
//     {
//         ServiceRegistrarHelper.VerifyImplementation<IMyService, MyService>();
//         services.TryAdd(new ServiceDescriptor(typeof(IMyService), typeof(MyService), ServiceLifetime.Singleton));
//     }
// }

// Usage:
services.AddServices(new MyRegistrar());
```

### 2.2 Generic variants (`[Singleton<T>]`, `[Transient<T>]`, `[Scoped<T>]`) register against the abstract type. Non-generic variants register the concrete type as self.

```csharp
[Singleton]                  // → typeof(MyService) → typeof(MyService)
[Singleton<IMyService>]      // → typeof(IMyService) → typeof(MyService)
```

Choose the generic form when callers depend on the interface. Use the non-generic form when only the concrete class is resolved directly.

### 2.3 `IsEnumerable = true` changes `TryAdd` to `Add`.

```csharp
[Singleton<IMyService>(IsEnumerable = true)]
// → services.Add(new ServiceDescriptor(typeof(IMyService), typeof(MyService), ServiceLifetime.Singleton))
// → allows multiple registrations for IMyService, resolved as IEnumerable<IMyService>
```

Without `IsEnumerable`, the generator emits `TryAdd` — the first registration wins. With it, `Add` is used and multiple implementations can coexist.

---

## 3. Keyed Injection

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
| `[Inject]` on a `readonly` field | The generator skips the entire class silently. Field won't be assigned, interface contract remains unimplemented (CS0535). Use partial property injection instead.
| Using `Reinjectable = false` (default) for a transient that gets re-injected into different scopes | The idempotency guard means members from the first injection are never updated. Either set `Reinjectable = true` or re-create the instance. |
| `[Injectable]` on a non-partial class | Generator cannot inject code → compilation error. The analyzer catches this at IDE time (DIG0007 / DIG0009). |
| Forgetting `IInjectable` on the class | The generated `Inject()` method exists but is not part of any interface — nothing enforces it gets called. The analyzer flags missing `[Injectable]` (DIG0009). |
| Calling `Inject()` before the service provider is ready | If the injected members depend on services that haven't been registered yet, `GetRequiredService<T>()` throws at injection time, not at usage time. That is correct behavior — fail fast. |
| Holding a scoped service in a field injected once into a singleton | The `[Inject]` is called once. If the target is a singleton, its injected scoped services are captured for the entire application lifetime. This is a DI anti-pattern — same as constructor-injecting a scoped service into a singleton.
| Forgetting to register `IInjectorServiceProvider` when auto-inject is expected | `services.AddInjector()` must be called; otherwise `IInjectorServiceProvider` is not in the container and no auto-injection happens. |
| Registering the same service with both non-generic and generic `[Singleton]` / `[Transient]` / `[Scoped]` on the same class | `TryAdd` ensures only the first wins, but the generator emits both registration calls, leading to confusion. Pick one style per class. |
| Editing generated `*.Inject.g.cs` files | They're overwritten on every build. Change the source attributes instead. |

---

## 6. Testing

- Test that `Inject()` assigns members correctly: create an instance, call `Inject(sp)`, assert members are non-null.
- Test nullable injection: register a service as optional, inject with nullable type, verify `null` is handled gracefully.
- Test the `ServiceRegistrationGenerator` by creating the `[ServiceRegistrar]` partial class, calling `RegisterServices()` on it, building the `ServiceCollection`, and resolving the expected services.
- Use the real DI container (`ServiceCollection` + `BuildServiceProvider`) in tests — don't mock `IServiceProvider` unless you're testing the generator output in isolation.
- Snapshot tests (via Verify) are the standard way to validate generated source code — see `tests/Everlong.DI.Tests/` for examples.

---

## 7. CLI / Tooling

```bash
dotnet build              # Generates code + compiles
dotnet test               # Run unit tests (52 tests covering generators, analyzers, code fixers)
git tag vX.Y.Z && git push origin vX.Y.Z   # CI builds, packs and publishes to nuget.org (trusted publishing)
```

No design-time tooling is required — the source generator runs automatically during `dotnet build`. No `dotnet ef` equivalent here.
