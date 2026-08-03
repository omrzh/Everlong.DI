using Everlong.DI.Generators.Constants;

namespace Everlong.DI.Generators.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ReadonlyInjectionSuppressor : DiagnosticSuppressor
{
  private static readonly SuppressionDescriptor Rule = new(
    id: "DIGSP001",
    suppressedDiagnosticId: "CS0649",
    justification: "Field is injected by generated member injection code."
  );

  public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions => ImmutableArray.Create(Rule);

  public override void ReportSuppressions(SuppressionAnalysisContext context)
  {
    foreach (var diagnostic in context.ReportedDiagnostics)
    {
      var node = diagnostic.Location.SourceTree?.GetRoot(context.CancellationToken)
        .FindNode(diagnostic.Location.SourceSpan);

      if (node is VariableDeclaratorSyntax variableDeclarator)
      {
        var symbol = context.GetSemanticModel(diagnostic.Location.SourceTree!).GetDeclaredSymbol(variableDeclarator);
        if (symbol is IFieldSymbol fieldSymbol)
        {
          var injectAttribute = fieldSymbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == Attributes.InjectFull);

          if (injectAttribute != null && !fieldSymbol.IsReadOnly)
          {
            context.ReportSuppression(Suppression.Create(Rule, diagnostic));
          }
        }
      }
    }
  }
}
