using Microsoft.Extensions.DependencyInjection;

namespace Everlong.DI;

public static class ServiceCollectionExtensions
{
  /// <summary>
  ///   Applies a single <see cref="IServiceRegistrar" />, calling its
  ///   <see cref="IServiceRegistrar.RegisterServices" /> method immediately.
  /// </summary>
  /// <param name="services">The service collection.</param>
  /// <param name="registrar">The registrar to apply.</param>
  /// <returns>The same service collection for chaining.</returns>
  public static IServiceCollection AddServices(this IServiceCollection services, IServiceRegistrar registrar)
  {
    ArgumentNullException.ThrowIfNull(registrar);
    registrar.RegisterServices(services);
    return services;
  }

  /// <summary>
  ///   Applies one or more <see cref="IServiceRegistrar" /> instances in order.
  /// </summary>
  /// <param name="services">The service collection.</param>
  /// <param name="registrars">The registrars to apply.</param>
  /// <returns>The same service collection for chaining.</returns>
  public static IServiceCollection AddServices(this IServiceCollection services, params IEnumerable<IServiceRegistrar> registrars)
  {
    ArgumentNullException.ThrowIfNull(registrars);
    foreach (var registrar in registrars)
      registrar.RegisterServices(services);
    return services;
  }

  /// <summary>
  ///  Adds the <see cref="IInjectorServiceProvider" /> to the service collection with the specified lifetime.
  /// </summary>
  public static IServiceCollection AddInjector(this IServiceCollection services, ServiceLifetime lifetime = ServiceLifetime.Scoped)
  {
    services.Add(new ServiceDescriptor(typeof(IInjectorServiceProvider), typeof(InjectorServiceProvider), lifetime));
    services.Add(new ServiceDescriptor(typeof(IInjector), (sp) => sp.GetRequiredService<IInjectorServiceProvider>(), lifetime));
    return services;
  }

}
