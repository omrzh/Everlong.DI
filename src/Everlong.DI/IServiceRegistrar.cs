using Microsoft.Extensions.DependencyInjection;

namespace Everlong.DI;

/// <summary>
///   Defines a contract for push-based service registration into a DI container.
///   Implement this interface on a partial class annotated with <see cref="ServiceRegistrarAttribute"/>
///   and the source generator will produce the <see cref="RegisterServices"/> implementation automatically.
/// </summary>
public interface IServiceRegistrar
{
  /// <summary>
  ///   Add services into the specified <see cref="IServiceCollection" />.
  /// </summary>
  /// <param name="services">The service collection to register into.</param>
  void RegisterServices(IServiceCollection services);
}
