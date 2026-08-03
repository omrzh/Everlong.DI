namespace Everlong.DI.CodeFixers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PropertyInjectionCodeFixProvider)), Shared]
public class PropertyInjectionCodeFixProvider : CodeFixProvider
{
  public override ImmutableArray<string> FixableDiagnosticIds =>
    ImmutableArray.Create(Descriptors.FieldInjectionToPropertyId);

  public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

  public override async Task RegisterCodeFixesAsync(CodeFixContext context)
  {
    var diagnostic = context.Diagnostics.FirstOrDefault(d => d.Id == Descriptors.FieldInjectionToPropertyId);
    if (diagnostic == null)
      return;
    context.RegisterCodeFix(
      CodeAction.Create(
        title: "Convert to partial property injection",
        createChangedDocument: ct => ApplyFix(context.Document, diagnostic, ct),
        equivalenceKey: "ConvertToPartialProperty"),
      diagnostic);
  }

  private async Task<Document> ApplyFix(Document document, Diagnostic diagnostic, CancellationToken ct)
  {
    var root = await document.GetSyntaxRootAsync(ct);
    if (root == null)
      return document;

    var semanticModel = await document.GetSemanticModelAsync(ct);
    if (semanticModel == null)
      return document;

    var node = root.FindNode(diagnostic.Location.SourceSpan);
    var variableDeclarator = node as VariableDeclaratorSyntax ?? node.FirstAncestorOrSelf<VariableDeclaratorSyntax>();
    if (variableDeclarator == null)
      return document;

    var fieldDeclaration = variableDeclarator.FirstAncestorOrSelf<FieldDeclarationSyntax>();
    if (fieldDeclaration == null)
      return document;

    var fieldSymbol = semanticModel.GetDeclaredSymbol(variableDeclarator, ct) as IFieldSymbol;
    if (fieldSymbol == null)
      return document;

    if (fieldSymbol.IsStatic)
      return document;

    var fieldName = variableDeclarator.Identifier.Text;
    var propertyName = GetPropertyName(fieldName);

    var referenceNodes = root.DescendantNodes()
      .OfType<IdentifierNameSyntax>()
      .Where(identifier =>
      {
        var symbol = semanticModel.GetSymbolInfo(identifier, ct).Symbol;
        return SymbolEqualityComparer.Default.Equals(symbol, fieldSymbol);
      })
      .ToList();

    var trackedRoot = root.TrackNodes(referenceNodes.Cast<SyntaxNode>()
                                        .Concat([fieldDeclaration, variableDeclarator]));
    var trackedReferences = referenceNodes
      .Select(referenceNode => trackedRoot.GetCurrentNode(referenceNode))
      .OfType<IdentifierNameSyntax>()
      .ToList();

    var rewrittenRoot = trackedRoot.ReplaceNodes(
      trackedReferences,
      (current, _) => current.WithIdentifier(
        SyntaxFactory.Identifier(
          current.Identifier.LeadingTrivia,
          propertyName,
          current.Identifier.TrailingTrivia)));

    var currentFieldDeclaration = rewrittenRoot.GetCurrentNode(fieldDeclaration);
    var currentVariableDeclarator = rewrittenRoot.GetCurrentNode(variableDeclarator);
    if (currentFieldDeclaration == null || currentVariableDeclarator == null)
      return document;

    var newModifiersList = currentFieldDeclaration.Modifiers
      .Where(m => !m.IsKind(SyntaxKind.ReadOnlyKeyword))
      .ToList();
    if (!newModifiersList.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
      newModifiersList.Add(SyntaxFactory.Token(SyntaxKind.PartialKeyword));

    var propertyDeclaration = SyntaxFactory.PropertyDeclaration(currentFieldDeclaration.Declaration.Type, propertyName)
      .WithModifiers(SyntaxFactory.TokenList(newModifiersList))
      .WithAttributeLists(currentFieldDeclaration.AttributeLists)
      .WithAccessorList(SyntaxFactory.AccessorList(
                          SyntaxFactory.List([
                            SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                              .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                          ])));

    propertyDeclaration = propertyDeclaration
      .WithLeadingTrivia(currentFieldDeclaration.GetLeadingTrivia())
      .WithTrailingTrivia(currentFieldDeclaration.GetTrailingTrivia());

    var variables = currentFieldDeclaration.Declaration.Variables;
    if (variables.Count == 1)
    {
      var newRoot = rewrittenRoot.ReplaceNode(currentFieldDeclaration, propertyDeclaration);
      return document.WithSyntaxRoot(newRoot);
    }

    var newVariables = variables.Remove(currentVariableDeclarator);
    var newFieldDeclaration = currentFieldDeclaration.WithDeclaration(
      currentFieldDeclaration.Declaration.WithVariables(newVariables));

    var parent = currentFieldDeclaration.Parent;
    if (parent is not TypeDeclarationSyntax typeDecl)
      return document;

    var members = typeDecl.Members;
    var index = members.IndexOf(currentFieldDeclaration);
    var newMembers = members.Replace(currentFieldDeclaration, newFieldDeclaration);
    newMembers = newMembers.Insert(index + 1, propertyDeclaration);

    var newTypeDecl = typeDecl.WithMembers(newMembers);
    var updatedRoot = rewrittenRoot.ReplaceNode(typeDecl, newTypeDecl);
    return document.WithSyntaxRoot(updatedRoot);
  }

  private static string GetPropertyName(string fieldName)
  {
    string name = fieldName.TrimStart('_');
    if (name.Length == 0)
      return "Property";
    return char.ToUpperInvariant(name[0]) + name.Substring(1);
  }
}
