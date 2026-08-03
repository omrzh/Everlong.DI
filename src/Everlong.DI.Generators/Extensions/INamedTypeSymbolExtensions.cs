namespace Everlong.DI.Generators.Extensions;

internal static class INamedTypeSymbolExtensions
{
  public static bool IsPartial(this INamedTypeSymbol typeSymbol)
  {
    foreach (SyntaxReference syntaxReference in typeSymbol.DeclaringSyntaxReferences)
    {
      if (syntaxReference.GetSyntax() is TypeDeclarationSyntax typeDeclaration &&
          typeDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword))
        return true;
    }
    return false;
  }

  public static IEnumerable<ISymbol> GetAllMembers(this INamedTypeSymbol symbol)
  {
    for (INamedTypeSymbol? currentSymbol = symbol; currentSymbol is { SpecialType: not SpecialType.System_Object }; currentSymbol = currentSymbol.BaseType)
    {
      foreach (ISymbol memberSymbol in currentSymbol.GetMembers())
        yield return memberSymbol;
    }
  }

  public static IEnumerable<ISymbol> GetAllMembersFromSameAssembly(this INamedTypeSymbol symbol)
  {
    for (INamedTypeSymbol? currentSymbol = symbol; currentSymbol is { SpecialType: not SpecialType.System_Object }; currentSymbol = currentSymbol.BaseType)
    {
      if (!SymbolEqualityComparer.Default.Equals(currentSymbol.ContainingAssembly, symbol.ContainingAssembly))
        yield break;
      foreach (ISymbol memberSymbol in currentSymbol.GetMembers())
        yield return memberSymbol;
    }
  }

  public static IEnumerable<ISymbol> GetAllMembers(this INamedTypeSymbol symbol, string name)
  {
    for (INamedTypeSymbol? currentSymbol = symbol; currentSymbol is { SpecialType: not SpecialType.System_Object }; currentSymbol = currentSymbol.BaseType)
    {
      foreach (ISymbol memberSymbol in currentSymbol.GetMembers(name))
        yield return memberSymbol;
    }
  }
}
