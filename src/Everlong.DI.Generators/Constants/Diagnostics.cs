using Microsoft.CodeAnalysis;

namespace Everlong.DI.Generators.Constants;

internal static class Descriptors
{
  internal const string TransformErrorId = "DIG0001";
  internal const string ExecutionErrorId = "DIG0002";
  internal const string MultipleServiceTablesId = "DIG0003";
  internal const string PropertySetterId = "DIG0004";
  internal const string PropertyStaticId = "DIG0005";
  internal const string PropertyInitPartialId = "DIG0006";
  internal const string ClassPartialId = "DIG0007";
  internal const string ReadonlyInjectionId = "DIG0008";
  internal const string InjectableRequiredId = "DIG0009";
  internal const string FieldInjectionToPropertyId = "DIG0010";

  internal static class Category
  {
    internal const string Injection = "Injection";
    internal const string Usage = "Usage";
    internal const string Transform = "Transform";
    internal const string Generator = "Generator";
    internal const string Configuration = "Configuration";
  }

  internal static readonly DiagnosticDescriptor PropertySetter = new(
    PropertySetterId,
    "Property must have a setter",
    "Property '{0}' must have a setter to be injectable",
    Category.Injection,
    DiagnosticSeverity.Error,
    isEnabledByDefault: true);

  internal static readonly DiagnosticDescriptor PropertyStatic = new(
    PropertyStaticId,
    "Member must not be static",
    "Member '{0}' must not be static to be injectable",
    Category.Injection,
    DiagnosticSeverity.Error,
    isEnabledByDefault: true);

  internal static readonly DiagnosticDescriptor PropertyInitPartial = new(
    PropertyInitPartialId,
    "Init-only property must be partial",
    "Init-only property '{0}' must be partial to be injectable",
    Category.Injection,
    DiagnosticSeverity.Error,
    isEnabledByDefault: true);

  internal static readonly DiagnosticDescriptor TargetPartial = new(
    ClassPartialId,
    "Class must be partial",
    "The target type '{0}' must be partial to allow code generation",
    Category.Usage,
    DiagnosticSeverity.Error,
    isEnabledByDefault: true);

  internal static readonly DiagnosticDescriptor ReadonlyInjection = new(
    ReadonlyInjectionId,
    "Readonly field injection",
    "Field '{0}' is readonly and cannot be injected. Remove readonly or use constructor injection.",
    Category.Injection,
    DiagnosticSeverity.Error,
    isEnabledByDefault: true);

  internal static readonly DiagnosticDescriptor InjectableRequired = new(
    InjectableRequiredId,
    "Missing [Injectable] on injection type",
    "Type '{0}' contains [Inject] members and must be marked with [Injectable]",
    Category.Usage,
    DiagnosticSeverity.Error,
    isEnabledByDefault: true);

  internal static readonly DiagnosticDescriptor FieldInjectionToProperty = new(
    FieldInjectionToPropertyId,
    "Use partial property injection",
    "Consider using partial property injection for field '{0}'",
    Category.Injection,
    DiagnosticSeverity.Info,
    isEnabledByDefault: true);

  internal static readonly DiagnosticDescriptor MultipleServiceTables = new(
    MultipleServiceTablesId,
    "Multiple ServiceRegistrar attributes",
    "Multiple [ServiceRegistrar] attributes found. Only one ServiceRegistrar is allowed per assembly.",
    Category.Configuration,
    DiagnosticSeverity.Error,
    isEnabledByDefault: true);

  internal static readonly DiagnosticDescriptor TransformError = new(
    TransformErrorId,
    "Transform Error",
    "Generator internal error at transform phase: input={0}; message={1}; stack={2}",
    Category.Transform,
    DiagnosticSeverity.Error,
    isEnabledByDefault: true);

  internal static readonly DiagnosticDescriptor ExecutionError = new(
    ExecutionErrorId,
    "Source Generator Exception",
    "Generator internal error at execution phase: input={0}; message={1}; stack={2}",
    Category.Generator,
    DiagnosticSeverity.Error,
    isEnabledByDefault: true);
}
