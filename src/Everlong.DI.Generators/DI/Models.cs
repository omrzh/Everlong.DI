using Everlong.DI.Generators.Models;

namespace Everlong.DI.Generators.DI;

internal enum ServiceKind
{
  /// <summary>Non-generic registration of the class itself ([Singleton]).</summary>
  Self,
  /// <summary>Generic registration of a service type ([Singleton&lt;T&gt;]).</summary>
  Generic,
  /// <summary>Shared-view registration ([AlsoAs&lt;T&gt;]).</summary>
  AlsoAs,
}

internal record ServiceInfo(
    string ImplementationType,
    string ServiceType,
    string Lifetime,
    bool IsEnumerable,
    string AssemblyName,
    string? KeyExpression = null,
    ServiceKind Kind = ServiceKind.Generic,
    LocationInfo? Location = null
);

internal record ServiceRegistrarInfo(
    string Namespace,
    string ClassName,
    EquatableArray<ContainingTypeInfo> ContainingTypes,
    LocationInfo? Location
);
