namespace Everlong.DI;

/// <summary>
///   Marks a property or field to be injected by the dependency injection container
///   when <see cref="IInjectable.Inject"/> is called.
/// </summary>
/// <remarks>
///   <para>
///     <c>[Inject]</c> members anchor generation (v2): a partial class that declares at
///     least one of them gets an <c>Inject(IServiceProvider)</c> implementation generated
///     automatically — no class-level marker required. The class only needs an explicit
///     opt-in (<see cref="IAutoInject"/>) when it owns no <c>[Inject]</c> members but still
///     wants to be an injection chain root (e.g. a memberless framework base).
///   </para>
/// </remarks>
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
