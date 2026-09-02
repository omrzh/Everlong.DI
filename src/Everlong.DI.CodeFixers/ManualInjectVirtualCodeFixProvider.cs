namespace Everlong.DI.CodeFixers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ManualInjectVirtualCodeFixProvider)), Shared]
public class ManualInjectVirtualCodeFixProvider : CodeFixProvider
{
  public override ImmutableArray<string> FixableDiagnosticIds =>
    ImmutableArray.Create(Descriptors.ManualInjectVirtualId);

  public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

  public override async Task RegisterCodeFixesAsync(CodeFixContext context)
  {
    var diagnostic = context.Diagnostics.FirstOrDefault(d => d.Id == Descriptors.ManualInjectVirtualId);
    if (diagnostic == null) return;

    context.RegisterCodeFix(
      CodeAction.Create(
        title: "Make Inject virtual",
        createChangedDocument: c => MakeVirtualAsync(context.Document, diagnostic, c),
        equivalenceKey: "MakeInjectVirtual"),
      diagnostic);
  }

  private static async Task<Document> MakeVirtualAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
  {
    var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
    if (root is not CompilationUnitSyntax compilationUnit)
      return document;

    var methodDecl = compilationUnit.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<MethodDeclarationSyntax>();
    if (methodDecl == null)
      return document;

    if (methodDecl.Modifiers.Any(SyntaxKind.VirtualKeyword))
      return document;

    var virtualToken = SyntaxFactory.Token(SyntaxKind.VirtualKeyword).WithTrailingTrivia(SyntaxFactory.Space);
    // Insert after the access modifier group: "public void" -> "public virtual void".
    int insertAt = 0;
    for (int i = 0; i < methodDecl.Modifiers.Count; i++)
    {
      if (methodDecl.Modifiers[i].IsKind(SyntaxKind.PublicKeyword)
          || methodDecl.Modifiers[i].IsKind(SyntaxKind.ProtectedKeyword)
          || methodDecl.Modifiers[i].IsKind(SyntaxKind.InternalKeyword)
          || methodDecl.Modifiers[i].IsKind(SyntaxKind.PrivateKeyword))
        insertAt = i + 1;
    }

    var updated = methodDecl.WithModifiers(methodDecl.Modifiers.Insert(insertAt, virtualToken));
    return document.WithSyntaxRoot(compilationUnit.ReplaceNode(methodDecl, updated));
  }
}
