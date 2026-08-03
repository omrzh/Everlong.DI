namespace Everlong.DI.CodeFixers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(InjectableCodeFixProvider)), Shared]
public class InjectableCodeFixProvider : CodeFixProvider
{
  public override ImmutableArray<string> FixableDiagnosticIds =>
    ImmutableArray.Create(Descriptors.InjectableRequiredId);

  public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

  public override async Task RegisterCodeFixesAsync(CodeFixContext context)
  {
    var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
    var diagnostic = context.Diagnostics.FirstOrDefault(d => d.Id == Descriptors.InjectableRequiredId);
    if (root == null || diagnostic == null)
      return;

    var typeDecl = root.FindToken(diagnostic.Location.SourceSpan.Start)
      .Parent?
      .AncestorsAndSelf()
      .OfType<TypeDeclarationSyntax>()
      .FirstOrDefault();
    if (typeDecl == null)
      return;

    context.RegisterCodeFix(
      CodeAction.Create(
        title: "Add [Injectable] and make class partial",
        createChangedDocument: c => AddInjectableAsync(context.Document, typeDecl, c),
        equivalenceKey: "AddInjectableAndPartial"),
      diagnostic);
  }

  private static async Task<Document> AddInjectableAsync(
    Document document,
    TypeDeclarationSyntax typeDecl,
    CancellationToken cancellationToken)
  {
    var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
    if (root is not CompilationUnitSyntax compilationUnit)
      return document;

    var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
    if (semanticModel == null)
      return document;

    var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl, cancellationToken);
    if (typeSymbol == null)
      return document;

    var hasInjectable = typeSymbol.GetAttributes()
      .Any(static a => a.AttributeClass?.ToDisplayString() == Attributes.InjectableFull);
    var hasPartial = typeDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));

    var updatedType = typeDecl;
    if (!hasInjectable)
    {
      var InjectableAttribute = SyntaxFactory.AttributeList(
        SyntaxFactory.SingletonSeparatedList(
          SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("Injectable"))));
      updatedType = updatedType.AddAttributeLists(InjectableAttribute);
    }

    if (!hasPartial)
    {
      var partialKeyword = SyntaxFactory.Token(SyntaxKind.PartialKeyword).WithTrailingTrivia(SyntaxFactory.Space);
      updatedType = updatedType.WithModifiers(updatedType.Modifiers.Add(partialKeyword));
    }

    var updatedRoot = compilationUnit.ReplaceNode(typeDecl, updatedType);

    var hasDiUsing = updatedRoot.Usings.Any(static u =>
      !u.StaticKeyword.IsKind(SyntaxKind.StaticKeyword)
      && u.Alias is null
      && u.Name?.ToString() == Ns.DiNamespace);

    if (!hasDiUsing)
    {
      updatedRoot = updatedRoot.AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(Ns.DiNamespace)));
    }

    return document.WithSyntaxRoot(updatedRoot);
  }
}
