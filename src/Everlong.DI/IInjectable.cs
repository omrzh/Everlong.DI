namespace Everlong.DI;

/// <summary>
///   Defines a contract for components that support member injection.
///   Classes marked with <see cref="InjectableAttribute"/> and implementing this interface
///   will have an <c>Inject(IServiceProvider)</c> implementation generated automatically.
/// </summary>
public interface IInjectable
{
  /// <summary>
  ///   Injects dependencies from the service provider into this instance's
  ///   <see cref="InjectAttribute"/>-annotated members.
  /// </summary>
  /// <param name="services">A service provider to resolve dependencies from.</param>
  void Inject(IServiceProvider services);
}
