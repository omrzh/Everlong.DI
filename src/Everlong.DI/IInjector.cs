namespace Everlong.DI;

/// <summary>
///   Provides strongly typed member injection for <see cref="IInjectable" /> instances.
/// </summary>
public interface IInjector
{
  /// <summary>
  ///   Injects dependencies into an existing <see cref="IInjectable" /> instance.
  /// </summary>
  /// <param name="instance">The target instance that accepts member injection.</param>
  void Inject(IInjectable instance);
}
