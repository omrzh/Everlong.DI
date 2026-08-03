using Everlong.DI.Generators.Extensions;

namespace Everlong.DI.Generators.Helpers;

public static class PredicateHelper
{
  public static bool IsPartialClassDecl(SyntaxNode node, CancellationToken token = default)
  {
    return node is TypeDeclarationSyntax typeDecl and not InterfaceDeclarationSyntax
           && typeDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword))
           && !token.IsCancellationRequested;
  }

  public static bool IsPartialRecursively(INamedTypeSymbol? symbol)
  {
    if (symbol == null) return false;
    if (!symbol.IsPartial()) return false;
    var current = symbol.ContainingType;
    while (current != null)
    {
      if (!current.IsPartial()) return false;
      current = current.ContainingType;
    }
    return true;
  }

  public static bool IsClass(SyntaxNode node, CancellationToken token = default)
    => node is ClassDeclarationSyntax;

  public static bool IsRecord(SyntaxNode node, CancellationToken token = default)
    => node is RecordDeclarationSyntax;
}
