using Everlong.DI.Generators.Constants;

namespace Everlong.DI.Generators.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PropertyInjectionAnalyzer : DiagnosticAnalyzer
{
  public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    ImmutableArray.Create(
      Descriptors.PropertySetter,
      Descriptors.TargetPartial,
      Descriptors.PropertyStatic,
      Descriptors.PropertyInitPartial,
      Descriptors.FieldInjectionToProperty,
      Descriptors.InjectableRequired);

  public override void Initialize(AnalysisContext context)
  {
    context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
    context.EnableConcurrentExecution();

    context.RegisterSymbolAction(AnalyzeType, SymbolKind.NamedType);
    context.RegisterSymbolAction(AnalyzeProperty, SymbolKind.Property);
    context.RegisterSymbolAction(AnalyzeField, SymbolKind.Field);
  }

  private static void AnalyzeType(SymbolAnalysisContext context)
  {
    var typeSymbol = (INamedTypeSymbol)context.Symbol;
    if (typeSymbol.TypeKind != TypeKind.Class)
      return;

    var hasInjectable = typeSymbol.GetAttributes()
      .Any(static a => a.AttributeClass?.ToDisplayString() == Attributes.InjectableFull);
    if (hasInjectable)
      return;

    var hasInjectMember = typeSymbol.GetMembers()
      .OfType<IPropertySymbol>()
      .Any(static p => p.GetAttributes().Any(static a => a.AttributeClass?.ToDisplayString() == Attributes.InjectFull))
      || typeSymbol.GetMembers()
      .OfType<IFieldSymbol>()
      .Any(static f => f.GetAttributes().Any(static a => a.AttributeClass?.ToDisplayString() == Attributes.InjectFull));

    if (!hasInjectMember)
      return;

    context.ReportDiagnostic(Diagnostic.Create(
      Descriptors.InjectableRequired,
      typeSymbol.Locations.FirstOrDefault(),
      typeSymbol.Name));
  }

  private void AnalyzeField(SymbolAnalysisContext context)
  {
    var fieldSymbol = (IFieldSymbol)context.Symbol;

    var injectAttribute = fieldSymbol.GetAttributes()
      .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == Attributes.InjectFull);

    if (injectAttribute == null)
      return;

    if (fieldSymbol.IsStatic)
    {
      context.ReportDiagnostic(Diagnostic.Create(Descriptors.PropertyStatic, fieldSymbol.Locations[0],
                                                 fieldSymbol.Name));
      return;
    }

    context.ReportDiagnostic(Diagnostic.Create(
      Descriptors.FieldInjectionToProperty,
      fieldSymbol.Locations[0],
      fieldSymbol.Name));
  }

  private void AnalyzeProperty(SymbolAnalysisContext context)
  {
    var propertySymbol = (IPropertySymbol)context.Symbol;

    var injectAttribute = propertySymbol.GetAttributes()
      .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == Attributes.InjectFull);

    if (injectAttribute == null)
      return;

    if (propertySymbol.IsStatic)
    {
      context.ReportDiagnostic(Diagnostic.Create(Descriptors.PropertyStatic, propertySymbol.Locations[0],
                                                 propertySymbol.Name));
    }

    if (propertySymbol.ContainingType != null)
    {
      bool isClassPartial = false;
      foreach (var reference in propertySymbol.ContainingType.DeclaringSyntaxReferences)
      {
        var syntax = reference.GetSyntax(context.CancellationToken);
        if (syntax is TypeDeclarationSyntax typeDecl &&
            typeDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
        {
          isClassPartial = true;
          break;
        }
      }

      if (!isClassPartial)
      {
        context.ReportDiagnostic(Diagnostic.Create(Descriptors.TargetPartial, propertySymbol.ContainingType.Locations[0],
                                                   propertySymbol.ContainingType.Name));
      }
    }

    bool isPropertyPartial = false;
    foreach (var reference in propertySymbol.DeclaringSyntaxReferences)
    {
      var syntax = reference.GetSyntax(context.CancellationToken);
      if (syntax is PropertyDeclarationSyntax propDecl &&
          propDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
      {
        isPropertyPartial = true;
        break;
      }
    }

    if (!isPropertyPartial)
    {
      if (propertySymbol.SetMethod == null)
      {
        context.ReportDiagnostic(Diagnostic.Create(Descriptors.PropertySetter, propertySymbol.Locations[0],
                                                   propertySymbol.Name));
      }
      else if (propertySymbol.SetMethod.IsInitOnly)
      {
        context.ReportDiagnostic(Diagnostic.Create(Descriptors.PropertyInitPartial, propertySymbol.Locations[0],
                                                   propertySymbol.Name));
      }
    }
  }
}
