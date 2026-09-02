namespace Everlong.DI.CodeFixers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ManualInjectExplicitCodeFixProvider)), Shared]
public class ManualInjectExplicitCodeFixProvider : CodeFixProvider
{
  public override ImmutableArray<string> FixableDiagnosticIds =>
    ImmutableArray.Create(Descriptors.ManualInjectExplicitId);

  public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

  public override async Task RegisterCodeFixesAsync(CodeFixContext context)
  {
    var diagnostic = context.Diagnostics.FirstOrDefault(d => d.Id == Descriptors.ManualInjectExplicitId);
    if (diagnostic == null) return;

    context.RegisterCodeFix(
      CodeAction.Create(
        title: "Convert to implicit virtual Inject",
        createChangedDocument: c => ConvertToImplicitVirtualAsync(context.Document, diagnostic, c),
        equivalenceKey: "ConvertExplicitInjectToImplicitVirtual"),
      diagnostic);
  }

  private static async Task<Document> ConvertToImplicitVirtualAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
  {
    var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
    if (root is not CompilationUnitSyntax compilationUnit)
      return document;

    var methodDecl = compilationUnit.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<MethodDeclarationSyntax>();
    if (methodDecl?.ExplicitInterfaceSpecifier == null)
      return document;

    // "void IInjectable.Inject(...)" → "public virtual void Inject(...)": strip the
    // explicit specifier, add public + virtual. The indentation lives on the return-type
    // token's leading trivia when there are no modifiers — move it onto the new first token.
    var voidToken = methodDecl.ReturnType.GetFirstToken();
    var leading = voidToken.LeadingTrivia;

    var publicToken = SyntaxFactory.Token(SyntaxKind.PublicKeyword)
      .WithLeadingTrivia(leading)
      .WithTrailingTrivia(SyntaxFactory.Space);
    var virtualToken = SyntaxFactory.Token(SyntaxKind.VirtualKeyword)
      .WithTrailingTrivia(SyntaxFactory.Space);

    var newModifiers = SyntaxFactory.TokenList(publicToken, virtualToken)
      .AddRange(methodDecl.Modifiers);

    var updated = methodDecl
      .WithExplicitInterfaceSpecifier(null)
      .WithModifiers(newModifiers);

    updated = updated.ReplaceToken(voidToken, voidToken.WithLeadingTrivia(SyntaxFactory.TriviaList()));
    return document.WithSyntaxRoot(compilationUnit.ReplaceNode(methodDecl, updated));
  }
}
