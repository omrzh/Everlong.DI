namespace Everlong.DI;

/// <summary>
///   Marks a class as a target for dependency injection member injection.
///   A partial class with this attribute will have an <c>Inject(IServiceProvider)</c>
///   method generated that resolves all <see cref="InjectAttribute"/>-annotated members.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class InjectableAttribute : Attribute
{
  /// <summary>
  ///   When <see langword="true"/>, the generated <c>Inject()</c> method may be called multiple times
  ///   and will re-assign all injected members each time.
  ///   When <see langword="false"/> (default), the first call assigns members and subsequent calls
  ///   return immediately, making injection idempotent and safe for singletons.
  /// </summary>
  public bool Reinjectable { get; set; }
}

/// <summary>
///   Marks a property or field to be injected by the dependency injection container
///   when <see cref="IInjectable.Inject"/> is called.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class InjectAttribute : Attribute
{
  /// <summary>
  ///   The key of the service to inject (for keyed DI).
  /// </summary>
  public object? Key { get; }

  /// <summary>
  ///   Initializes a new instance of the <see cref="InjectAttribute" /> class (unkeyed).
  /// </summary>
  public InjectAttribute() { }

  /// <summary>
  ///   Initializes a new instance of the <see cref="InjectAttribute" /> class with a string key.
  /// </summary>
  /// <param name="key">The key of the service.</param>
  public InjectAttribute(string key) => Key = key;

  /// <summary>
  ///   Initializes a new instance of the <see cref="InjectAttribute" /> class with an int key.
  /// </summary>
  /// <param name="key">The key of the service.</param>
  public InjectAttribute(int key) => Key = key;

  /// <summary>
  ///   Initializes a new instance of the <see cref="InjectAttribute" /> class with a Type key.
  /// </summary>
  /// <param name="key">The key of the service.</param>
  public InjectAttribute(Type key) => Key = key;

  /// <summary>
  ///   Initializes a new instance of the <see cref="InjectAttribute" /> class with an object key (e.g. Enum).
  /// </summary>
  /// <param name="key">The key of the service.</param>
  public InjectAttribute(object key) => Key = key;
}
