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
public class TransientAttribute : Attribute
{
  /// <summary>
  /// Gets the key of the service registration (for keyed DI), or <see langword="null"/> for an unkeyed registration.
  /// </summary>
  public object? Key { get; }

  /// <summary>
  /// Initializes a new instance of the <see cref="TransientAttribute"/> class (unkeyed).
  /// </summary>
  public TransientAttribute() { }

  /// <summary>
  /// Initializes a new instance of the <see cref="TransientAttribute"/> class with a service key.
  /// </summary>
  /// <param name="key">The key of the service.</param>
  public TransientAttribute(object key) => Key = key;
}

/// <summary>
/// Marks a class to be registered as a transient service for a specific service type.
/// </summary>
/// <typeparam name="TAbstract">The service type.</typeparam>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public class TransientAttribute<TAbstract> : Attribute
{
  /// <summary>
  /// Gets a value indicating whether the current instance supports enumeration of its elements.
  /// </summary>
  public bool IsEnumerable { get; }

  /// <summary>
  /// Gets the key of the service registration (for keyed DI), or <see langword="null"/> for an unkeyed registration.
  /// </summary>
  public object? Key { get; }

  /// <summary>
  /// Initializes a new instance of the <see cref="TransientAttribute{TAbstract}"/> class (unkeyed).
  /// </summary>
  public TransientAttribute() { }

  /// <summary>
  /// Initializes a new instance of the <see cref="TransientAttribute{TAbstract}"/> class (unkeyed).
  /// </summary>
  /// <param name="isEnumerable">Whether to register as an enumerable service.</param>
  public TransientAttribute(bool isEnumerable) => IsEnumerable = isEnumerable;

  /// <summary>
  /// Initializes a new instance of the <see cref="TransientAttribute{TAbstract}"/> class with a service key.
  /// </summary>
  /// <param name="key">The key of the service.</param>
  /// <param name="enumerable">Whether to register as an enumerable service.</param>
  public TransientAttribute(object? key = null, bool enumerable = false)
  {
    Key = key;
    IsEnumerable = enumerable;
  }
}

/// <summary>
/// Marks a class to be registered as a singleton service.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class SingletonAttribute : Attribute
{
  /// <summary>
  /// Gets the key of the service registration (for keyed DI), or <see langword="null"/> for an unkeyed registration.
  /// </summary>
  public object? Key { get; }

  /// <summary>
  /// Initializes a new instance of the <see cref="SingletonAttribute"/> class (unkeyed).
  /// </summary>
  public SingletonAttribute() { }

  /// <summary>
  /// Initializes a new instance of the <see cref="SingletonAttribute"/> class with a service key.
  /// </summary>
  /// <param name="key">The key of the service.</param>
  public SingletonAttribute(object key) => Key = key;
}

/// <summary>
/// Marks a class to be registered as a singleton service for a specific service type.
/// </summary>
/// <typeparam name="T">The service type.</typeparam>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public class SingletonAttribute<T> : Attribute
{
  /// <summary>
  /// Gets a value indicating whether the current instance supports enumeration of its elements.
  /// </summary>
  public bool IsEnumerable { get; }

  /// <summary>
  /// Gets the key of the service registration (for keyed DI), or <see langword="null"/> for an unkeyed registration.
  /// </summary>
  public object? Key { get; }

  /// <summary>
  /// Initializes a new instance of the <see cref="SingletonAttribute{T}"/> class (unkeyed).
  /// </summary>
  public SingletonAttribute() { }

  /// <summary>
  /// Initializes a new instance of the <see cref="SingletonAttribute{T}"/> class (unkeyed).
  /// </summary>
  /// <param name="isEnumerable">Whether to register as an enumerable service.</param>
  public SingletonAttribute(bool isEnumerable) => IsEnumerable = isEnumerable;

  /// <summary>
  /// Initializes a new instance of the <see cref="SingletonAttribute{T}"/> class with a service key.
  /// </summary>
  /// <param name="key">The key of the service.</param>
  /// <param name="enumerable">Whether to register as an enumerable service.</param>
  public SingletonAttribute(object? key = null, bool enumerable = false)
  {
    Key = key;
    IsEnumerable = enumerable;
  }
}

/// <summary>
/// Marks an additional service type through which the class's single registered instance is exposed.
/// The class must carry exactly one <see cref="SingletonAttribute"/>, <see cref="SingletonAttribute{T}"/>,
/// <see cref="ScopedAttribute"/> or <see cref="ScopedAttribute{TAbstract}"/> registration as its main
/// registration; every <see cref="AlsoAsAttribute{TAlso}"/> adds one shared view of that instance.
/// </summary>
/// <typeparam name="TAlso">The additional service type (an interface implemented by the class).</typeparam>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public class AlsoAsAttribute<TAlso> : Attribute
{
  /// <summary>
  /// Gets a value indicating whether the current instance supports enumeration of its elements.
  /// </summary>
  public bool IsEnumerable { get; }

  /// <summary>
  /// Gets the key of the service registration (for keyed DI), or <see langword="null"/> for an unkeyed registration.
  /// </summary>
  public object? Key { get; }

  /// <summary>
  /// Initializes a new instance of the <see cref="AlsoAsAttribute{TAlso}"/> class (unkeyed).
  /// </summary>
  public AlsoAsAttribute() { }

  /// <summary>
  /// Initializes a new instance of the <see cref="AlsoAsAttribute{TAlso}"/> class (unkeyed).
  /// </summary>
  /// <param name="isEnumerable">Whether to register as an enumerable service.</param>
  public AlsoAsAttribute(bool isEnumerable) => IsEnumerable = isEnumerable;

  /// <summary>
  /// Initializes a new instance of the <see cref="AlsoAsAttribute{TAlso}"/> class with a service key.
  /// </summary>
  /// <param name="key">The key of the service.</param>
  /// <param name="enumerable">Whether to register as an enumerable service.</param>
  public AlsoAsAttribute(object? key = null, bool enumerable = false)
  {
    Key = key;
    IsEnumerable = enumerable;
  }
}

/// <summary>
/// Marks a class to be registered as a scoped service.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class ScopedAttribute : Attribute
{
  /// <summary>
  /// Gets the key of the service registration (for keyed DI), or <see langword="null"/> for an unkeyed registration.
  /// </summary>
  public object? Key { get; }

  /// <summary>
  /// Initializes a new instance of the <see cref="ScopedAttribute"/> class (unkeyed).
  /// </summary>
  public ScopedAttribute() { }

  /// <summary>
  /// Initializes a new instance of the <see cref="ScopedAttribute"/> class with a service key.
  /// </summary>
  /// <param name="key">The key of the service.</param>
  public ScopedAttribute(object key) => Key = key;
}

/// <summary>
/// Marks a class to be registered as a scoped service for a specific service type.
/// </summary>
/// <typeparam name="TAbstract">The service type.</typeparam>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public class ScopedAttribute<TAbstract> : Attribute
{
  /// <summary>
  /// Gets a value indicating whether the current instance supports enumeration of its elements.
  /// </summary>
  public bool IsEnumerable { get; }

  /// <summary>
  /// Gets the key of the service registration (for keyed DI), or <see langword="null"/> for an unkeyed registration.
  /// </summary>
  public object? Key { get; }

  /// <summary>
  /// Initializes a new instance of the <see cref="ScopedAttribute{TAbstract}"/> class (unkeyed).
  /// </summary>
  public ScopedAttribute() { }

  /// <summary>
  /// Initializes a new instance of the <see cref="ScopedAttribute{TAbstract}"/> class (unkeyed).
  /// </summary>
  /// <param name="isEnumerable">Whether to register as an enumerable service.</param>
  public ScopedAttribute(bool isEnumerable) => IsEnumerable = isEnumerable;

  /// <summary>
  /// Initializes a new instance of the <see cref="ScopedAttribute{TAbstract}"/> class with a service key.
  /// </summary>
  /// <param name="key">The key of the service.</param>
  /// <param name="enumerable">Whether to register as an enumerable service.</param>
  public ScopedAttribute(object? key = null, bool enumerable = false)
  {
    Key = key;
    IsEnumerable = enumerable;
  }
}
