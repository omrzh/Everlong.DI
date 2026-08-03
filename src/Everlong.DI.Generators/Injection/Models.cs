using Everlong.DI.Generators.Models;

namespace Everlong.DI.Generators.Injection;

internal sealed record InjectionInfo(
  HierarchyInfo Hierarchy,
  EquatableArray<InjectedMember> Members,
  bool BaseImplementsInject,
  bool IsSealed,
  bool Reinjectable
);

internal sealed record InjectedMember(
  string Name,
  TypeName Type,
  bool IsPartial,
  string? Modifiers,
  bool IsField,
  bool IsNullable,
  string? KeyExpression);
