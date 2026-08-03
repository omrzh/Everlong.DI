using Everlong.DI.Generators.Models;

namespace Everlong.DI.Generators.DI;

internal record ServiceInfo(
    string ImplementationType,
    string ServiceType,
    string Lifetime,
    bool IsEnumerable,
    string AssemblyName
);

internal record ServiceRegistrarInfo(
    string Namespace,
    string ClassName,
    EquatableArray<ContainingTypeInfo> ContainingTypes,
    LocationInfo? Location
);
