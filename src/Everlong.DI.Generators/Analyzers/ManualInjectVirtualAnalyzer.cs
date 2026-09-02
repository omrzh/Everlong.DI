using Everlong.DI.Generators.Constants;

namespace Everlong.DI.Generators.Analyzers;

/// <summary>
///   Flags hand-written <c>Inject(IServiceProvider)</c> implementations of
///   <see cref="Everlong.DI.IInjectable"/> on non-sealed classes that block derived
///   injection chains:
///   <list type="bullet">
///     <item>
///       <description>
///         DIG0018 — an implicit implementation that is not <c>virtual</c>/<c>abstract</c>;
///       </description>
///     </item>
///     <item>
///       <description>
///         DIG0019 — an explicit implementation (<c>void IInjectable.Inject(...)</c>), which
///         can never be overridden.
///       </description>
///     </item>
///   </list>
///   An open class declares it may be derived, so it must leave the injection channel
///   overridable; sealed classes are exempt (nothing can derive).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ManualInjectVirtualAnalyzer : DiagnosticAnalyzer
{
  public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    ImmutableArray.Create(Descriptors.ManualInjectVirtual, Descriptors.ManualInjectExplicit);

  public override void Initialize(AnalysisContext context)
  {
    context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
    context.EnableConcurrentExecution();

    context.RegisterSymbolAction(AnalyzeType, SymbolKind.NamedType);
  }

  private static void AnalyzeType(SymbolAnalysisContext context)
  {
    var typeSymbol = (INamedTypeSymbol)context.Symbol;
    if (typeSymbol.TypeKind != TypeKind.Class || typeSymbol.IsSealed)
      return;

    // Only Everlong.DI.IInjectable matters — a foreign type merely named IInjectable must
    // not trigger this rule.
    if (context.Compilation.GetTypeByMetadataName(Interfaces.IInjectableFull) is not { } iInjectable)
      return;
    if (iInjectable.GetMembers(Methods.Inject).FirstOrDefault() is not IMethodSymbol injectMethod)
      return;

    foreach (IMethodSymbol method in typeSymbol.GetMembers().OfType<IMethodSymbol>())
    {
      // Explicit implementations report a fully-qualified Name ("Everlong.DI.IInjectable.Inject")
      // and are modeled virtual+final — handle them before any name/virtual filter.
      if (method.ExplicitInterfaceImplementations.Length > 0)
      {
        bool implementsInject = method.ExplicitInterfaceImplementations.Any(m =>
          m.Name == Methods.Inject && m.ContainingType?.ToDisplayString() == Interfaces.IInjectableFull);
        if (!implementsInject)
          continue;

        context.ReportDiagnostic(Diagnostic.Create(
          Descriptors.ManualInjectExplicit,
          method.Locations.FirstOrDefault() ?? typeSymbol.Locations.FirstOrDefault(),
          method.Name));
        continue;
      }

      if (method.Name != Methods.Inject || method.IsStatic)
        continue;
      if (method.IsAbstract || method.IsVirtual || method.IsOverride)
        continue;

      if (!SymbolEqualityComparer.Default.Equals(
            typeSymbol.FindImplementationForInterfaceMember(injectMethod), method))
        continue;

      context.ReportDiagnostic(Diagnostic.Create(
        Descriptors.ManualInjectVirtual,
        method.Locations.FirstOrDefault() ?? typeSymbol.Locations.FirstOrDefault(),
        method.Name));
    }
  }
}
