namespace Everlong.DI.CodeFixers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PartialKeywordCodeFixProvider)), Shared]
public class PartialKeywordCodeFixProvider : CodeFixProvider
{
  public sealed override ImmutableArray<string> FixableDiagnosticIds =>
    ImmutableArray.Create(Descriptors.ClassPartialId);

  public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

  public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
  {
    var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
    if (root == null) return;

    var classPartialDiag = context.Diagnostics.FirstOrDefault(d => d.Id == Descriptors.ClassPartialId);
    if (classPartialDiag != null)
    {
      var typeDecl = root.FindToken(classPartialDiag.Location.SourceSpan.Start).Parent?
        .AncestorsAndSelf()
        .OfType<TypeDeclarationSyntax>()
        .FirstOrDefault();

      if (typeDecl != null)
      {
        context.RegisterCodeFix(
          CodeAction.Create(
            title: "Make class partial",
            createChangedDocument: c => MakeTypePartialAsync(context.Document, typeDecl, c),
            equivalenceKey: "MakeClassPartial"),
          classPartialDiag);
      }
    }
  }

  private static async Task<Document> MakeTypePartialAsync(Document document,
                                                       TypeDeclarationSyntax typeDecl,
                                                       CancellationToken cancellationToken)
  {
    var root = await document.GetSyntaxRootAsync(cancellationToken);
    if (root == null)
      return document;

    if (typeDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
      return document;

    var partialToken = SyntaxFactory.Token(SyntaxKind.PartialKeyword).WithTrailingTrivia(SyntaxFactory.Space);
    var newModifiers = typeDecl.Modifiers.Add(partialToken);
    var newTypeDecl = typeDecl.WithModifiers(newModifiers);

    var newRoot = root.ReplaceNode(typeDecl, newTypeDecl);
    return document.WithSyntaxRoot(newRoot);
  }
}
