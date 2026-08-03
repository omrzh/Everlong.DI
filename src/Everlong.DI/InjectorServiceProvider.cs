using Microsoft.Extensions.DependencyInjection;

namespace Everlong.DI;

/// <summary>
///   Default implementation of <see cref="IInjectorServiceProvider" /> that wraps an inner
///   <see cref="IServiceProvider" /> and auto-calls <see cref="IInjectable.Inject" /> on every resolved object.
/// </summary>
internal sealed class InjectorServiceProvider : IInjectorServiceProvider
{

  /// <summary>
  ///   Initializes a new instance of the <see cref="InjectorServiceProvider" /> class.
  /// </summary>
  /// <param name="sp">
  ///   The underlying <see cref="IServiceProvider" /> to wrap. Must also implement
  ///   <see cref="IKeyedServiceProvider" /> to support keyed service resolution.
  /// </param>
  public InjectorServiceProvider(IServiceProvider sp)
  {
    if (sp is not IKeyedServiceProvider keySp)
    {
      throw new ArgumentException("The provided IServiceProvider must also implement IKeyedServiceProvider.", nameof(sp));
    }
    _sp = keySp;
  }


  private readonly IKeyedServiceProvider _sp;

  /// <summary>
  ///   Gets a service of the specified type.
  /// </summary>
  /// <param name="serviceType">The type of the service to resolve.</param>
  /// <returns>
  ///   The resolved service instance, or <c>null</c> if the service is not registered or could not be resolved.
  ///   If the resolved instance implements <see cref="IInjectable" />, member injection is automatically performed.
  /// </returns>
  /// <remarks>
  ///   <para>
  ///     This method delegates to the underlying <see cref="IServiceProvider" /> and automatically applies
  ///     member injection to the resolved instance if it implements <see cref="IInjectable" />.
  ///   </para>
  /// </remarks>
  public object? GetService(Type serviceType) => AutoInject(_sp.GetService(serviceType));

  /// <summary>
  ///   Gets a keyed service of the specified type using the provided service key.
  /// </summary>
  /// <param name="serviceType">The type of the service to resolve.</param>
  /// <param name="serviceKey">
  ///   The key used to identify the specific service registration. Can be <c>null</c> for unkeyed registrations.
  /// </param>
  /// <returns>
  ///   The resolved service instance, or <c>null</c> if the service with the given key is not registered or could not be resolved.
  ///   If the resolved instance implements <see cref="IInjectable" />, member injection is automatically performed.
  /// </returns>
  /// <remarks>
  ///   <para>
  ///     Keyed services allow multiple registrations of the same type with different keys.
  ///     This method delegates to the underlying <see cref="IKeyedServiceProvider" /> and automatically applies
  ///     member injection to the resolved instance if it implements <see cref="IInjectable" />.
  ///   </para>
  /// </remarks>
  public object? GetKeyedService(Type serviceType, object? serviceKey)
    => AutoInject(_sp.GetKeyedService(serviceType, serviceKey));

  /// <summary>
  ///   Gets a required keyed service of the specified type using the provided service key.
  /// </summary>
  /// <param name="serviceType">The type of the service to resolve.</param>
  /// <param name="serviceKey">
  ///   The key used to identify the specific service registration. Can be <c>null</c> for unkeyed registrations.
  /// </param>
  /// <returns>
  ///   The resolved service instance. Member injection is automatically performed if the instance implements
  ///   <see cref="IInjectable" />.
  /// </returns>
  /// <remarks>
  ///   <para>
  ///     Unlike <see cref="GetKeyedService" />, this method requires the service to be registered and resolvable.
  ///     Keyed services allow multiple registrations of the same type with different keys.
  ///     This method delegates to the underlying <see cref="IKeyedServiceProvider" /> and automatically applies
  ///     member injection to the resolved instance if it implements <see cref="IInjectable" />.
  ///   </para>
  /// </remarks>
  /// <exception cref="InvalidOperationException">
  ///   Thrown when the service with the specified key is not registered or could not be resolved.
  /// </exception>
  public object GetRequiredKeyedService(Type serviceType, object? serviceKey)
    => AutoInject(_sp.GetRequiredKeyedService(serviceType, serviceKey))!;

  /// <summary>
  ///   Performs member injection on the specified instance.
  /// </summary>
  /// <param name="instance">
  ///   The instance on which to perform member injection. The instance should implement
  ///   <see cref="IInjectable" /> or this method will have no effect.
  /// </param>
  /// <remarks>
  ///   <para>
  ///     This method explicitly triggers member injection on an instance by delegating to
  ///     <see cref="IInjectable.Inject" />. This is useful for manually injecting members
  ///     into objects that were not resolved through the service provider.
  ///   </para>
  ///   <para>
  ///     Member injection is automatically applied to instances resolved through
  ///     <see cref="GetService" />, <see cref="GetKeyedService" />, and <see cref="GetRequiredKeyedService" />,
  ///     so explicit calls to this method are typically only needed for manually instantiated objects.
  ///   </para>
  /// </remarks>
  public void Inject(IInjectable instance) => instance.Inject(this);

  private object? AutoInject(object? instance)
  {
    if (instance is IInjectable target)
    {
      target.Inject(this);
    }

    return instance;
  }
}
