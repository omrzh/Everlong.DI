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

  /// <summary>
  ///   Registers the <see cref="IScopeMarker" /> as a scoped service, enabling
  ///   <see cref="IsScoped" /> on providers built from this collection.
  /// </summary>
  /// <param name="services">The service collection.</param>
  /// <returns>The same service collection for chaining.</returns>
  public static IServiceCollection AddScopeMarker(this IServiceCollection services)
  {
    services.AddScoped<IScopeMarker, ScopeMarker>();
    return services;
  }

  /// <summary>
  ///   Determines whether <paramref name="provider" /> is a child scope of a provider configured
  ///   with <see cref="AddScopeMarker" />. Returns <see langword="false" /> for the root provider,
  ///   for providers without the marker registered, and when scoped resolution from the root
  ///   throws under <c>ValidateScopes</c>.
  /// </summary>
  /// <param name="provider">The service provider to test.</param>
  /// <returns>
  ///   <see langword="true" /> if the scope marker is registered and <paramref name="provider" />
  ///   is a child scope; otherwise <see langword="false" />.
  /// </returns>
  public static bool IsScoped(this IServiceProvider provider)
  {
    ArgumentNullException.ThrowIfNull(provider);
    try
    {
      return provider.GetService<IScopeMarker>() is { } marker && !marker.IsRootScope;
    }
    catch (InvalidOperationException)
    {
      return false; // scoped resolution from the root throws under ValidateScopes
    }
  }
}
