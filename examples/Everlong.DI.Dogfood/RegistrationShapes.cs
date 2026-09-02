namespace Everlong.DI.Dogfood;

// ── Service-registration shapes (unchanged from v1, dogfooded here too) ────────

public interface IPinger { string Ping(); }
public interface ISharedThing { string Tag { get; } }
public interface IEventHandler { string Name { get; } }

[Singleton<IPinger>]
public partial class RegistrarPinger : IPinger
{
    public string Ping() => "pong";
}

// Self main registration + a shared [AlsoAs] view of the same single instance.
[Singleton]
[AlsoAs<ISharedThing>]
public partial class SharedThingImpl : ISharedThing
{
    public string Tag => "shared";
}

// Two enumerable registrations of one service type (isEnumerable → Add, not TryAdd).
[Singleton<IEventHandler>(isEnumerable: true)]
public partial class AuditEventHandler : IEventHandler
{
    public string Name => "audit";
}

[Singleton<IEventHandler>(isEnumerable: true)]
public partial class NotifyEventHandler : IEventHandler
{
    public string Name => "notify";
}

// Keyed + unkeyed live in separate spaces.
[Singleton<ICache>]
public partial class DefaultCache : ICache
{
    public string Tag => "default";
}

[Singleton<ICache>("tenant:eu")]
public partial class EuCache : ICache
{
    public string Tag => "eu";
}

// Scoped: shared within a scope, isolated across scopes.
[Scoped<IPageService>]
public partial class ScopedPageService : IPageService
{
    public string Tag => "scoped-page-service";
}

[ServiceRegistrar]
public partial class AppRegistrar : IServiceRegistrar;
