namespace Everlong.DI;

/// <summary>
///   Defines the member-injection contract: an object that can receive
///   <see cref="InjectAttribute"/>-annotated members from a service provider.
/// </summary>
/// <remarks>
///   <para>
///     Generated code implements <see cref="IAutoInject"/> (which derives from this
///     interface), so container integration code typically checks for this interface:
///     every <see cref="IAutoInject"/> implementor is one, and hand-written injectables
///     may implement only <see cref="IInjectable"/>.
///   </para>
///   <para>
///     <b>Sealed chain starts:</b> when a sealed class starts a chain (no injectable
///     ancestor), the generated member is a plain <c>public void Inject(...)</c> —
///     non-virtual — because a sealed class cannot be derived, so a virtual method could
///     never be overridden, and C# forbids <c>virtual</c> in sealed classes (CS0549).
///   </para>
/// </remarks>
public interface IInjectable
{
  /// <summary>
  ///   Injects dependencies from the service provider into this instance's
  ///   <see cref="InjectAttribute"/>-annotated members.
  /// </summary>
  /// <param name="services">A service provider to resolve dependencies from.</param>
  void Inject(IServiceProvider services);
}
