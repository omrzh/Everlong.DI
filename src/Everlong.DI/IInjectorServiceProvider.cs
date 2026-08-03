using Microsoft.Extensions.DependencyInjection;

namespace Everlong.DI;

/// <summary>
/// A keyed service provider that also supports member injection for <see cref="IInjectable"/> instances.
/// </summary>
public interface IInjectorServiceProvider : IKeyedServiceProvider, IInjector;
