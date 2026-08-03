namespace Everlong.DI;

/// <summary>
/// Provides utility methods for validating service registrations to ensure architectural integrity,
/// particularly for Native AOT and trimmed environments.
/// </summary>
public static class ServiceRegistrarHelper
{
  /// <summary>
  /// Ensures that the specified generic type is a concrete class and not an interface or an abstract class.
  /// </summary>
  /// <typeparam name="TImplementation">The type to validate.</typeparam>
  /// <exception cref="ArgumentException">Thrown when <typeparamref name="TImplementation"/> is an interface or an abstract class.</exception>
  /// <remarks>
  /// The <see cref="DynamicallyAccessedMembersAttribute"/> ensures that public constructors are preserved during the trimming process.
  /// </remarks>
  public static void EnsureConcreteType<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    TImplementation>() =>
    EnsureConcreteType(typeof(TImplementation));

  /// <summary>
  /// Ensures that the provided <see cref="Type"/> is a concrete class and not an interface or an abstract class.
  /// </summary>
  /// <param name="implementationType">The type metadata to validate.</param>
  /// <exception cref="ArgumentException">Thrown when <paramref name="implementationType"/> is an interface or an abstract class.</exception>
  public static void EnsureConcreteType(Type implementationType)
  {
    if (implementationType.IsAbstract || implementationType.IsInterface)
    {
      throw new ArgumentException(
        $"`{implementationType.FullName}` cannot be registered as an implementation. Use a concrete, non-abstract class.",
        nameof(implementationType));
    }
  }

  /// <summary>
  /// Verifies that an implementation type is compatible with a service contract and is valid for registration.
  /// </summary>
  /// <typeparam name="TService">The service contract (interface or base class).</typeparam>
  /// <typeparam name="TImplementation">The actual implementation class.</typeparam>
  /// <exception cref="ArgumentException">
  /// Thrown when <typeparamref name="TImplementation"/> is not a concrete class
  /// or is not assignable to <typeparamref name="TService"/>.
  /// </exception>
  public static void VerifyImplementation<TService,
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    TImplementation>()
  {
    var implementationType = typeof(TImplementation);
    var serviceType = typeof(TService);

    EnsureConcreteType(implementationType);

    if (!serviceType.IsAssignableFrom(implementationType))
    {
      throw new ArgumentException(
        $"`{implementationType.FullName}` cannot be registered as an implementation of `{serviceType.FullName}`.",
        nameof(TImplementation));
    }
  }
}
