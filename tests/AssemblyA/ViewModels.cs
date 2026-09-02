using Everlong.DI;

namespace AssemblyA;

// ── Stubs to mirror the real-world shape (CommunityToolkit.Mvvm base + page args) ──
public class ObservableObject { }

public class PageArgs { }

public interface IShellManager { }

public interface ILayer { }

// Case 1: non-generic partial class with [Inject] members + a base class (no marker).
public abstract partial class TargetViewModel : ObservableObject
{
  /// <summary>Shell-level access (options, intents).</summary>
  [Inject] public partial IShellManager Manager { get; }

  /// <summary>Navigation facade — the one entry point for navigation requests.</summary>
  [Inject] public partial ILayer Navigator { get; }
}

// Case 2: generic partial class with [Inject] members + a base class + type-parameter constraint (no marker).
public abstract partial class TargetViewModel<TArgs> : ObservableObject where TArgs : PageArgs?
{
  /// <summary>Shell-level access (options, intents).</summary>
  [Inject] public partial IShellManager Manager { get; }

  /// <summary>Navigation facade — the one entry point for navigation requests.</summary>
  [Inject] public partial ILayer Navigator { get; }
}
