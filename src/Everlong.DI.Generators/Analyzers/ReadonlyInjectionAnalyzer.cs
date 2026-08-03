using Everlong.DI.Generators.Constants;

namespace Everlong.DI.Generators.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ReadonlyInjectionAnalyzer : DiagnosticAnalyzer
{
  public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptors.ReadonlyInjection);

  public override void Initialize(AnalysisContext context)
  {
    context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
    context.EnableConcurrentExecution();
    context.RegisterSyntaxNodeAction(AnalyzeField, SyntaxKind.FieldDeclaration);
  }

  private static void AnalyzeField(SyntaxNodeAnalysisContext context)
  {
    var fieldDeclaration = (FieldDeclarationSyntax)context.Node;

    if (!fieldDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.ReadOnlyKeyword)))
      return;

    foreach (var variable in fieldDeclaration.Declaration.Variables)
    {
      var symbol = context.SemanticModel.GetDeclaredSymbol(variable);
      if (symbol is IFieldSymbol fieldSymbol)
      {
        var injectAttribute = fieldSymbol.GetAttributes()
          .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == Attributes.InjectFull);

        if (injectAttribute != null)
        {
          context.ReportDiagnostic(Diagnostic.Create(Descriptors.ReadonlyInjection, variable.Identifier.GetLocation(), fieldSymbol.Name));
        }
      }
    }
  }
}
