using Everlong.DI;
using Everlong.DI.Dogfood;
using Microsoft.Extensions.DependencyInjection;

// ── Tiny assertion harness ─────────────────────────────────────────────────────
int checks = 0;
void Check(bool ok, string what)
{
    checks++;
    Console.WriteLine($"  [{(ok ? "ok" : "FAIL")}] {what}");
    if (!ok) throw new Exception($"CHECK FAILED: {what}");
}

// ── Container setup ────────────────────────────────────────────────────────────
var services = new ServiceCollection();

services.AddInjector();                    // IInjectorServiceProvider — auto-inject on resolve
services.AddScopeMarker();                 // IsScoped() support
services.AddServices(new AppRegistrar());  // registrar-driven attribute registration

// Plain container services consumed through [Inject] members:
services.AddSingleton<IShell>(new ShellSvc());
services.AddSingleton<IRouter>(new RouterSvc());
services.AddSingleton<IClock>(new ClockSvc());
services.AddKeyedSingleton<ICache>(CacheTier.Fast, new TagCache("fast"));
services.AddKeyedSingleton<ICache>(typeof(ICache), new TagCache("typed"));

// VM types resolved through the auto-inject wrapper:
services.AddTransient<PostDetailPageModel>();

using ServiceProvider sp = services.BuildServiceProvider();

Console.WriteLine("Everlong.DI v2 dogfood — every check must pass.\n");

// ── A. Service registration (attribute-driven, unchanged from v1) ─────────────
Console.WriteLine("A. registration attributes (registrar + AlsoAs + enumerable + keyed + scoped)");

Check(sp.GetRequiredService<IPinger>().Ping() == "pong", "TryAdd singleton via [Singleton<IPinger>]");

var shared = sp.GetRequiredService<SharedThingImpl>();
var sharedView = sp.GetRequiredService<ISharedThing>();
Check(ReferenceEquals(shared, sharedView), "[Singleton] self + [AlsoAs<ISharedThing>] share one instance");

var handlers = sp.GetServices<IEventHandler>().ToList();
Check(handlers.Count == 2 && handlers.All(h => h.Name is "audit" or "notify"),
    $"enumerable [Singleton<IEventHandler>(isEnumerable: true)] yields 2 (got {handlers.Count})");

Check(sp.GetRequiredService<ICache>().Tag == "default", "unkeyed ICache -> default");
Check(sp.GetRequiredKeyedService<ICache>("tenant:eu").Tag == "eu", "keyed ICache[\"tenant:eu\"] -> eu");
Check(!ReferenceEquals(sp.GetRequiredService<ICache>(), sp.GetRequiredKeyedService<ICache>("tenant:eu")),
    "keyed and unkeyed spaces are separate");

using (var scope1 = sp.CreateScope())
using (var scope2 = sp.CreateScope())
{
    var a = scope1.ServiceProvider.GetRequiredService<IPageService>();
    var b = scope1.ServiceProvider.GetRequiredService<IPageService>();
    var c = scope2.ServiceProvider.GetRequiredService<IPageService>();
    Check(ReferenceEquals(a, b) && !ReferenceEquals(a, c),
        "scoped service shared in-scope, isolated across scopes");
}

// ── B. Member injection — the hierarchy that used to break ────────────────────
// RoutableViewModel(IAutoInject, Shell/Router) : RoutableViewModel<TArgs>(transparent)
//   : PostDetailPageModel([Inject] PageService). The leaf's override must chain through
//   the transparent generic middle to the root's wiring (no CS0114, no null members).
Console.WriteLine("B. member-injection chain (transparent memberless middle)");

var vm = new PostDetailPageModel();
vm.Inject(sp);
Check(vm.Describe() == "shell=shell|router=router|svc=scoped-page-service",
    $"leaf Inject() wires root Shell/Router AND own PageService (got \"{vm.Describe()}\")");
vm.Inject(sp);
Check(vm.OnInjectedCalls == 1, "second Inject() is a no-op (idempotency, hook fired once)");

// ── C. Auto-inject wrapper ─────────────────────────────────────────────────────
Console.WriteLine("C. AddInjector wrapper (IInjectorServiceProvider)");
using (var scope = sp.CreateScope())
{
    var injector = scope.ServiceProvider.GetRequiredService<IInjectorServiceProvider>();
    var resolved = injector.GetRequiredService<PostDetailPageModel>();
    Check(resolved.Describe() == "shell=shell|router=router|svc=scoped-page-service",
        $"wrapper-resolved instance is injected automatically (got \"{resolved.Describe()}\")");
    Check(resolved is IInjectable, "generated types satisfy IInjectable via IAutoInject");
}

// ── D. Sealed shapes ───────────────────────────────────────────────────────────
Console.WriteLine("D. sealed chain start vs sealed chained class");
var sealedStart = new FinalStatelessService();
sealedStart.Inject(sp);
Check(sealedStart.Clock is not null, "sealed chain start: plain non-virtual Inject works");

var sealedChained = new FinalChainedService();
sealedChained.Inject(sp);
Check(sealedChained.Clock is not null && sealedChained.Extra is not null,
    "sealed chained class still overrides and chains (base + own members)");

// ── E. Re-listed IAutoInject = own level ───────────────────────────────────────
Console.WriteLine("E. derived re-listing IAutoInject gets its own level");
var hooked = new HookDerived();
hooked.Inject(sp);
hooked.Inject(sp);
Check(hooked.OwnLevelCalls == 1, "memberless derived with own level: its OnInjected fires exactly once");

// ── F. Nullability & required fail-fast ────────────────────────────────────────
Console.WriteLine("F. nullable members (GetService) vs required (GetRequiredService)");
var optional = new OptionalConsumer();
optional.Inject(sp);
Check(optional.MaybeClock is not null && optional.Missing is null && optional.Clock is not null,
    "nullable members resolve or stay null; non-nullable resolves");
var missing = new MissingRequiredConsumer();
try
{
    missing.Inject(sp);
    Check(false, "unregistered non-nullable member must throw (fail fast)");
}
catch (InvalidOperationException)
{
    Check(true, "unregistered non-nullable member throws InvalidOperationException");
}

// Guard commit semantics: a throwing Inject leaves Δinjected false, so the SAME instance
// can be re-injected once the provider is fixed.
using var sp2 = new ServiceCollection()
    .AddSingleton<IRemoteConfig>(new RemoteConfigSvc())
    .AddSingleton<IClock>(new ClockSvc())
    .BuildServiceProvider();
missing.Inject(sp2);
Check(missing.Config is not null,
    "after fixing the provider, the same instance re-injects (guard commits only on success)");

// All-or-nothing per level: first member resolves, second throws — NOTHING at this level
// may be assigned (two-phase resolve-then-assign).
var twoPhase = new TwoPhaseConsumer();
try
{
    twoPhase.Inject(sp);
    Check(false, "second member unregistered must throw");
}
catch (InvalidOperationException) { }
Check(twoPhase.Clock is null,
    "failed Inject assigns nothing at that level (first member was buffered, not committed)");
twoPhase.Inject(sp2);
Check(twoPhase.Clock is not null && twoPhase.Config is not null,
    "retry wires all members of the level");

// ── G. Keyed member injection (string / enum / Type keys) ─────────────────────
Console.WriteLine("G. keyed [Inject] resolution");
var keyed = new KeyedConsumer();
keyed.Inject(sp);
Check(keyed.EuCache?.Tag == "eu", "string key resolves the keyed registration");
Check(keyed.FastCache?.Tag == "fast", "enum key resolves");
Check(keyed.TypedCache?.Tag == "typed", "Type key resolves");

// ── H. Scope marker ────────────────────────────────────────────────────────────
Console.WriteLine("H. scope detection (AddScopeMarker)");
Check(!sp.IsScoped(), "root provider is not scoped");
using (var scope = sp.CreateScope())
    Check(scope.ServiceProvider.IsScoped(), "child scope reports scoped");

// ── I. Field injection (simple style, no LangVersion-preview requirement) ──────
Console.WriteLine("I. field injection");
var fieldy = new FieldConsumer();
fieldy.Inject(sp);
Check(fieldy.Describe() == "clock|scoped-page-service|null",
    $"fields wire non-nullable + nullable + unregistered (got \"{fieldy.Describe()}\")");
fieldy.Inject(sp);
Check(fieldy.Describe() == "clock|scoped-page-service|null",
    "field injection is idempotent like any other level");

Console.WriteLine($"\n{checks}/{checks} checks passed — ALL OK!");
Console.WriteLine("Generated sources: examples/Everlong.DI.Dogfood/obj/generated/ (EmitCompilerGeneratedFiles).");

// ── Tiny container-service implementations ─────────────────────────────────────
sealed class ShellSvc : IShell { public string Tag => "shell"; }
sealed class RouterSvc : IRouter { public string Tag => "router"; }
sealed class ClockSvc : IClock { public string Tag => "clock"; }
sealed class TagCache(string tag) : ICache { public string Tag => tag; }
sealed class RemoteConfigSvc : IRemoteConfig { }
