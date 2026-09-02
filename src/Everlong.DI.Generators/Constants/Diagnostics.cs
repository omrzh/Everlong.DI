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
  // DIG0009 was removed in v2: [Inject] members are self-sufficient (member-anchored
  // generation); the class-level opt-in is the IAutoInject interface, not an attribute.
  // IDs are never reused.
  internal const string FieldInjectionToPropertyId = "DIG0010";
  internal const string AlsoAsMissingMainId = "DIG0011";
  internal const string AlsoAsOnTransientId = "DIG0012";
  internal const string AlsoAsAmbiguousMainId = "DIG0013";
  internal const string AlsoAsOnEnumerableMainId = "DIG0014";
  internal const string SelfAndGenericInSameLifetimeId = "DIG0015";
  internal const string MultipleLifetimesId = "DIG0016";
  internal const string AlsoAsTypeNotImplementedId = "DIG0017";
  internal const string ManualInjectVirtualId = "DIG0018";
  internal const string ManualInjectExplicitId = "DIG0019";

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

  internal static readonly DiagnosticDescriptor AlsoAsMissingMain = new(
    AlsoAsMissingMainId,
    "AlsoAs without main registration",
    "Type '{0}' uses [AlsoAs] but has no [Singleton]/[Scoped] registration to share. Add [Singleton] or [Scoped].",
    Category.Configuration,
    DiagnosticSeverity.Error,
    isEnabledByDefault: true);

  internal static readonly DiagnosticDescriptor AlsoAsOnTransient = new(
    AlsoAsOnTransientId,
    "AlsoAs on transient registration",
    "Type '{0}' uses [AlsoAs] with a transient registration; transient services have no shared instance. Use [Singleton] or [Scoped].",
    Category.Configuration,
    DiagnosticSeverity.Error,
    isEnabledByDefault: true);

  internal static readonly DiagnosticDescriptor AlsoAsAmbiguousMain = new(
    AlsoAsAmbiguousMainId,
    "Ambiguous AlsoAs main registration",
    "Type '{0}' uses [AlsoAs] but has multiple main registrations; exactly one [Singleton]/[Scoped] registration is allowed with [AlsoAs]",
    Category.Configuration,
    DiagnosticSeverity.Error,
    isEnabledByDefault: true);

  internal static readonly DiagnosticDescriptor AlsoAsOnEnumerableMain = new(
    AlsoAsOnEnumerableMainId,
    "AlsoAs on enumerable main registration",
    "Type '{0}' uses [AlsoAs] with an enumerable main registration; a single instance cannot be resolved. Remove enumerable from the main registration.",
    Category.Configuration,
    DiagnosticSeverity.Error,
    isEnabledByDefault: true);

  internal static readonly DiagnosticDescriptor SelfAndGenericInSameLifetime = new(
    SelfAndGenericInSameLifetimeId,
    "Self and generic registration in the same lifetime",
    "Type '{0}' mixes a self registration ([Singleton]) with a generic registration ([Singleton<T>]) in the same lifetime. Use [Singleton] + [AlsoAs<T>] to share one instance, or multiple [Singleton<T>] for independent instances.",
    Category.Configuration,
    DiagnosticSeverity.Error,
    isEnabledByDefault: true);

  internal static readonly DiagnosticDescriptor MultipleLifetimes = new(
    MultipleLifetimesId,
    "Multiple lifetimes on one type",
    "Type '{0}' mixes registrations with different lifetimes ([Singleton]/[Scoped]/[Transient]); exactly one lifetime is allowed per type",
    Category.Configuration,
    DiagnosticSeverity.Error,
    isEnabledByDefault: true);

  internal static readonly DiagnosticDescriptor AlsoAsTypeNotImplemented = new(
    AlsoAsTypeNotImplementedId,
    "AlsoAs type not implemented",
    "AlsoAs type '{0}' must be an interface implemented by '{1}'",
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

  internal static readonly DiagnosticDescriptor ManualInjectExplicit = new(
    ManualInjectExplicitId,
    "Explicit IInjectable.Inject on an open type",
    "'{0}' explicitly implements Everlong.DI.IInjectable on a non-sealed type — explicit implementations cannot be overridden, which blocks derived [Inject] classes; convert to an implicit public virtual Inject, or seal the type",
    Category.Usage,
    DiagnosticSeverity.Error,
    isEnabledByDefault: true);

  internal static readonly DiagnosticDescriptor ManualInjectVirtual = new(
    ManualInjectVirtualId,
    "IInjectable.Inject should be virtual",
    "'Inject' implements Everlong.DI.IInjectable on a non-sealed type but is not virtual — mark it 'virtual' (or 'abstract') so derived [Inject] classes can override and chain; otherwise generated overrides fail (CS0506)",
    Category.Usage,
    DiagnosticSeverity.Error,
    isEnabledByDefault: true);
}
