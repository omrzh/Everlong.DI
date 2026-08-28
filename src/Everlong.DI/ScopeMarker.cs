using Microsoft.Extensions.DependencyInjection;

namespace Everlong.DI;

/// <summary>
///   Marker registered by <see cref="ServiceCollectionExtensions.AddScopeMarker"/> that lets callers
///   detect whether a given <see cref="IServiceProvider"/> is a child scope or the root provider.
/// </summary>
public interface IScopeMarker
{
  /// <summary>
  ///   Gets a value indicating whether this marker was resolved from the root provider
  ///   rather than from a child scope created by <see cref="IServiceScopeFactory.CreateScope"/>.
  /// </summary>
  bool IsRootScope { get; }
}

internal sealed class ScopeMarker : IScopeMarker
{
  public ScopeMarker(IServiceProvider provider)
  {
    // MS DI resolves IServiceScopeFactory against the root scope and IServiceProvider against the
    // current scope — they are the same object only when the current provider IS the root scope.
    var root = provider.GetService<IServiceScopeFactory>() as IServiceProvider ?? provider;
    var current = provider.GetService<IServiceProvider>() ?? provider;
    IsRootScope = ReferenceEquals(root, current);
  }

  public bool IsRootScope { get; }
}
