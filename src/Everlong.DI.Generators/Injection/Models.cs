using Everlong.DI.Generators.Models;

namespace Everlong.DI.Generators.Injection;

internal sealed record InjectionInfo(
  HierarchyInfo Hierarchy,
  EquatableArray<InjectedMember> Members,
  bool ChainExposesInject,
  bool IsSealed
);

internal sealed record InjectedMember(
  string Name,
  TypeName Type,
  TypeName DeclaredType,
  bool IsPartial,
  string? Modifiers,
  bool IsField,
  bool IsNullable,
  string? KeyExpression);
