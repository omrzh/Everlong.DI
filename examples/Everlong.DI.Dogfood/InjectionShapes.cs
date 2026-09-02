namespace Everlong.DI.Dogfood;

// ── Member-injection shapes (v2 semantics) ─────────────────────────────────────
//
// These classes exercise the shapes documented in docs/skills/…/SKILL.md §1.
// Nothing here carries a class-level attribute: [Inject] members anchor generation,
// IAutoInject is the optional marker, and memberless levels are transparent.

// (1) Framework-style root WITH members — a chain start. The generated partial stamps
//     `: IAutoInject` (hence IInjectable) and emits a virtual Inject.
public partial class RoutableViewModel : IAutoInject
{
    [Inject] public partial IShell Shell { get; }
    [Inject] public partial IRouter Router { get; }
}

// (2) Transparent intermediate: no members, no marker → nothing is generated. The leaf
//     overrides the root's virtual Inject THROUGH this class.
public partial class RoutableViewModel<TArgs> : RoutableViewModel { }

// (3) The leaf — the v2 shape that used to break with CS0114 + skipped injections.
//     Only an [Inject] member is written here.
public partial class PostDetailPageModel : RoutableViewModel<PostDetailArgs>
{
    [Inject] public partial IPageService PageService { get; }

    private int _onInjectedCalls;
    partial void OnInjected() => _onInjectedCalls++;

    public int OnInjectedCalls => _onInjectedCalls;
    public string Describe()
        => $"shell={Shell.Tag}|router={Router.Tag}|svc={PageService.Tag}";
}

// (4a) Sealed chain start: generated member is plain `public void Inject` (non-virtual —
//      nothing could override it and C# forbids virtual in sealed classes, CS0549).
public sealed partial class FinalStatelessService : IAutoInject
{
    [Inject] public partial IClock Clock { get; }
}

// (4b) Sealed class in the middle of a chain: still emits `public override`.
public partial class OpenServiceBase : IAutoInject
{
    [Inject] public partial IClock Clock { get; }
}

public sealed partial class FinalChainedService : OpenServiceBase
{
    [Inject] public partial IPageService Extra { get; }
}

// (5) Re-listing IAutoInject on a derived class = "give me my own level": the memberless
//     HookDerived gets its own chain-through override, so ITS OnInjected hook fires.
public partial class HookRoot : IAutoInject { }

public partial class HookDerived : HookRoot, IAutoInject
{
    private int _ownLevelCalls;
    partial void OnInjected() => _ownLevelCalls++;

    public int OwnLevelCalls => _ownLevelCalls;
}

// (6) Nullable → GetService (null when unregistered); non-nullable → GetRequiredService
//     (throws when unregistered). IRemoteConfig is never registered.
public partial class OptionalConsumer : IAutoInject
{
    [Inject] public partial IClock? MaybeClock { get; }
    [Inject] public partial IRemoteConfig? Missing { get; }
    [Inject] public partial IClock Clock { get; }
}

// (7) Keyed injection: string / Type / enum keys resolve from the keyed space.
public partial class KeyedConsumer : IAutoInject
{
    [Inject("tenant:eu")] public partial ICache? EuCache { get; }
    [Inject(CacheTier.Fast)] public partial ICache? FastCache { get; }
    [Inject(typeof(ICache))] public partial ICache? TypedCache { get; }
}

// (8) Non-nullable member whose service is not registered → GetRequiredService throws.
public partial class MissingRequiredConsumer : IAutoInject
{
    [Inject] public partial IRemoteConfig Config { get; }
}

// (9) Two members: the first resolves, the second throws → all-or-nothing per level.
public partial class TwoPhaseConsumer : IAutoInject
{
    [Inject] public partial IClock Clock { get; }
    [Inject] public partial IRemoteConfig Config { get; }
}

// (10) Field injection — the simple style, preferred when partial properties are not an
//      option (it needs no <LangVersion>preview</LangVersion>). Same two-phase semantics.
public partial class FieldConsumer : IAutoInject
{
    [Inject] private IClock _clock;                       // non-nullable field → GetRequiredService
    [Inject] private IPageService? _optionalPage;         // nullable field → GetService
    [Inject] private IRemoteConfig? _missing;             // never registered → null

    public string Describe()
        => $"{_clock.Tag}|{_optionalPage?.Tag ?? "none"}|{(_missing is null ? "null" : "set")}";
}
