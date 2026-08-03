namespace Everlong.DI;

/// <summary>
/// Marks a class as a service registrar container.
/// Apply this to a partial class; the source generator will produce the
/// <see cref="IServiceRegistrar.RegisterServices"/> implementation
/// that registers all <see cref="SingletonAttribute"/>, <see cref="TransientAttribute"/>,
/// and <see cref="ScopedAttribute"/>-annotated types.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class ServiceRegistrarAttribute : Attribute;

/// <summary>
/// Marks a class to be registered as a transient service.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class TransientAttribute : Attribute;

/// <summary>
/// Marks a class to be registered as a transient service for a specific service type.
/// </summary>
/// <typeparam name="TAbstract">The service type.</typeparam>
/// <param name="isEnumerable">Whether to register as an enumerable service.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class TransientAttribute<TAbstract>(bool isEnumerable = false) : Attribute
{
  /// <summary>
  /// Gets a value indicating whether the current instance supports enumeration of its elements.
  /// </summary>
  public bool IsEnumerable { get; } = isEnumerable;
}

/// <summary>
/// Marks a class to be registered as a singleton service.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class SingletonAttribute : Attribute;

/// <summary>
/// Marks a class to be registered as a singleton service for a specific service type.
/// </summary>
/// <typeparam name="T">The service type.</typeparam>
/// <param name="isEnumerable">Whether to register as an enumerable service.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class SingletonAttribute<T>(bool isEnumerable = false) : Attribute
{
  /// <summary>
  /// Gets a value indicating whether the current instance supports enumeration of its elements.
  /// </summary>
  public bool IsEnumerable { get; } = isEnumerable;
}

/// <summary>
/// Marks a class to be registered as a scoped service.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class ScopedAttribute : Attribute;

/// <summary>
/// Marks a class to be registered as a scoped service for a specific service type.
/// </summary>
/// <typeparam name="TAbstract">The service type.</typeparam>
/// <param name="isEnumerable">Whether to register as an enumerable service.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class ScopedAttribute<TAbstract>(bool isEnumerable = false) : Attribute
{
  /// <summary>
  /// Gets a value indicating whether the current instance supports enumeration of its elements.
  /// </summary>
  public bool IsEnumerable { get; } = isEnumerable;
}
