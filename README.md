# Everlong.DI

**Everlong.DI** is a lightweight member-injection & service-registration library for .NET.  
It provides a clean attribute-based DI experience with source generators — no reflection overhead at runtime.

```
dotnet add package Everlong.DI
```

---

## What's included

One NuGet package, everything inside:

| Layer | Contents |
|---|---|
| **Contracts** | `IInjectable`, `IInjector`, `IInjectorServiceProvider`, `IServiceRegistrar` |
| **Attributes** | `[Injectable]`, `[Inject]`, `[Singleton]`, `[Transient]`, `[Scoped]`, `[ServiceRegistrar]` |
| **Helpers** | `ServiceRegistrarHelper` (AOT-safe registration validation), `ServiceCollectionExtensions.AddInjector()` |
| **Service Provider** | `InjectorServiceProvider` — built-in wrapper that auto-injects `IInjectable` instances on every resolve |
| **Source Generator** | `MemberInjectionGenerator` (generates `Inject()` bodies), `ServiceRegistrationGenerator` (generates `RegisterServices()`) |
| **Analyzers** | `PropertyInjectionAnalyzer`, `ReadonlyInjectionAnalyzer`, `ReadonlyInjectionSuppressor` |
| **Code Fixers** | `PropertyInjectionCodeFixProvider`, `InjectableCodeFixProvider`, `PartialKeywordCodeFixProvider` |

---

## Requirements

- .NET 8+
- Your project **must** set `<LangVersion>preview</LangVersion>` in `.csproj` to use partial properties:

  ```xml
  <PropertyGroup>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>
  ```

  This is required by the C# compiler for the partial property syntax (`[Inject] public partial ILogger Logger { get; }`).
  The source generator itself targets `netstandard2.0` and works with any modern Roslyn version.

---

## Quick Start

### 1. Mark a class for member injection

```csharp
using Everlong.DI;

[Injectable]                                // ← triggers source generator
public partial class MyService : IInjectable  // ← IInjectable gives you the Inject() contract
{
    [Inject] private ILogger _logger;        // ← field injection
    [Inject] public ISomeService Service { get; set; }  // ← property injection

    // The generator produces:
    // public virtual void Inject(IServiceProvider services)
    // {
    //     _logger = services.GetRequiredService<ILogger>();
    //     Service = services.GetRequiredService<ISomeService>();
    // }
}
```

### 2. Call `Inject()` after resolution — or auto-inject

**Manual injection:**

```csharp
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddSingleton<MyService>();
// ... register other services ...

var sp = services.BuildServiceProvider();
var instance = sp.GetRequiredService<MyService>();

// Trigger member injection:
instance.Inject(sp);
```

**Automatic injection** — register the built-in `IInjectorServiceProvider` wrapper and every resolved `IInjectable` gets its members injected automatically:

```csharp
var services = new ServiceCollection();
services.AddInjector();                          // registers IInjectorServiceProvider as scoped
services.AddSingleton<MyService>();

var injector = services.BuildServiceProvider().GetRequiredService<IInjectorServiceProvider>();
var instance = injector.GetRequiredService<MyService>();  // Inject() called automatically
```

You can also change the lifetime: `services.AddInjector(ServiceLifetime.Singleton)`.

### 3. Service registration via attributes

```csharp
[Singleton]                           // register as self
public partial class CacheService : IInjectable { … }

[Singleton<IService>(IsEnumerable = true)]   // register as IService + enumerable
public partial class MyService : IService, IInjectable { … }

[Transient]
public partial class Handler : IInjectable { … }

[Scoped]
public partial class ScopedService : IInjectable { … }
```

Then add a `[ServiceRegistrar]` class — the generator auto-implements `RegisterServices`:

```csharp
using Everlong.DI;

[ServiceRegistrar]
public partial class MyRegistrar : IServiceRegistrar;

// Generated:
// public partial class MyRegistrar : IServiceRegistrar
// {
//     public void RegisterServices(IServiceCollection services)
//     {
//         ServiceRegistrarHelper.EnsureConcreteType<CacheService>();
//         services.TryAdd(new ServiceDescriptor(typeof(CacheService), typeof(CacheService), ServiceLifetime.Singleton));
//         ServiceRegistrarHelper.VerifyImplementation<IService, MyService>();
//         services.TryAdd(new ServiceDescriptor(typeof(IService), typeof(MyService), ServiceLifetime.Singleton));
//         …
//     }
// }

// Registration:
services.AddServices(new MyRegistrar());
```

---

## Attributes Reference

| Attribute | Target | Description |
|---|---|---|
| `[Injectable]` | class | Marks a partial class for generated `Inject()` method. Supports `Reinjectable = true` to allow repeated injection |
| `[Inject]` | property / field | Marks a member to be injected |
| `[Singleton]` | class | Registers the class as singleton (self) |
| `[Singleton<T>]` | class | Registers the class as singleton for service type `T` |
| `[Transient]` | class | Registers the class as transient (self) |
| `[Transient<T>]` | class | Registers the class as transient for service type `T` |
| `[Scoped]` | class | Registers the class as scoped (self) |
| `[Scoped<T>]` | class | Registers the class as scoped for service type `T` |
| `[ServiceRegistrar]` | class | Marks a partial class to host the generated `RegisterServices` |

### `[Injectable]` options

| Option | Default | Description |
|---|---|---|
| `Reinjectable` | `false` | When `false`, the generated `Inject()` method is idempotent — subsequent calls do nothing. Set to `true` to allow re-assignment on every call (e.g. for transient instances that may be injected multiple times). |

### `[Inject]` overloads

```csharp
[Inject]                 // unkeyed — resolved via GetRequiredService<T>()
[Inject("key")]          // string key
[Inject(42)]             // int key
[Inject(typeof(TKey))]   // Type key
[Inject(SomeEnum.X)]     // Enum key
```

Nullable members automatically use `GetService<T>()` / `GetKeyedService<T>()` (safe, returns null if not registered).

### Generated `Inject()` method

Partial properties generate a backing field + expression-bodied property:

```csharp
[Inject] public partial ILogger Logger { get; }
// Generated:
// [EditorBrowsable(Never)]
// private ILogger __injected_Logger = default!;
// public partial ILogger Logger => __injected_Logger;
```

Every generated `Inject()` method includes a call to `OnInjected()` at the end. You can optionally implement this partial method to run custom logic after injection:

```csharp
[Injectable]
public partial class MyService : IInjectable
{
    [Inject] private ILogger _logger;

    partial void OnInjected()
    {
        // Called after all members are injected
        _logger.LogInformation("Injection complete");
    }
}
```

---

## Key Interfaces

| Interface | Role |
|---|---|
| `IInjectable` | Contract for member injection — the generated code implements `Inject(IServiceProvider)` |
| `IInjector` | Typed entry point — `void Inject(IInjectable instance)` |
| `IInjectorServiceProvider` | Composite of `IKeyedServiceProvider` + `IInjector` — auto-injects on resolve |
| `IServiceRegistrar` | Push-based registration — `void RegisterServices(IServiceCollection)` |

---

## AOT / Trimming

`ServiceRegistrarHelper.EnsureConcreteType<T>()` and `VerifyImplementation<TService, TImpl>()` are annotated with `[DynamicallyAccessedMembers]` to preserve constructors during trimming.

The source generator produces direct `GetRequiredService<T>()` calls, so there is no reflection in the hot path.

---

## Build & Pack

```bash
dotnet build
dotnet pack src/Everlong.DI -c Release
```

Package is produced under `src/Everlong.DI/bin/Release/`.

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
    ├── AssemblyA/                        # Analyzer test reference assemblies
    └── AssemblyB/
```

---

## License

MIT
