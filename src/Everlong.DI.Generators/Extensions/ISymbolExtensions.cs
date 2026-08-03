using System.Diagnostics.CodeAnalysis;

namespace Everlong.DI.Generators.Extensions;

internal static class ISymbolExtensions
{
  public static string GetFullyQualifiedName(this ISymbol symbol) =>
    symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

  public static string GetFullyQualifiedNameWithNullabilityAnnotations(this ISymbol symbol) => symbol.ToDisplayString(
    SymbolDisplayFormat.FullyQualifiedFormat.AddMiscellaneousOptions(
      SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier));

  public static string GetFullyQualifiedMetadataName(this ISymbol symbol)
  {
    var fullName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    if (fullName.StartsWith("global::", StringComparison.Ordinal))
      fullName = fullName.Substring("global::".Length);
    return fullName.Replace('+', '.');
  }

  public static bool HasFullyQualifiedName(this ISymbol symbol, string name) =>
    symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == name;

  public static bool HasAttributeWithFullyQualifiedMetadataName(this ISymbol symbol, string name)
  {
    foreach (AttributeData attribute in symbol.GetAttributes())
    {
      if (attribute.AttributeClass?.GetFullyQualifiedMetadataName() == name)
        return true;
    }
    return false;
  }

  public static bool HasAttributeWithType(this ISymbol symbol, ITypeSymbol typeSymbol) =>
    TryGetAttributeWithType(symbol, typeSymbol, out _);

  public static bool TryGetAttributeWithType(this ISymbol symbol,
                                             ITypeSymbol typeSymbol,
                                             [NotNullWhen(true)] out AttributeData? attributeData)
  {
    foreach (AttributeData attribute in symbol.GetAttributes())
    {
      if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, typeSymbol))
      {
        attributeData = attribute;
        return true;
      }
    }
    attributeData = null;
    return false;
  }

  public static Accessibility GetEffectiveAccessibility(this ISymbol symbol)
  {
    Accessibility visibility = Accessibility.Public;
    switch (symbol.Kind)
    {
      case SymbolKind.Alias:
        return Accessibility.Private;
      case SymbolKind.Parameter:
        return GetEffectiveAccessibility(symbol.ContainingSymbol);
      case SymbolKind.TypeParameter:
        return Accessibility.Private;
    }
    while (symbol is not null && symbol.Kind != SymbolKind.Namespace)
    {
      switch (symbol.DeclaredAccessibility)
      {
        case Accessibility.NotApplicable:
        case Accessibility.Private:
          return Accessibility.Private;
        case Accessibility.Internal:
        case Accessibility.ProtectedAndInternal:
          visibility = Accessibility.Internal;
          break;
      }
      symbol = symbol.ContainingSymbol;
    }
    return visibility;
  }

  public static bool CanBeAccessedFrom(this ISymbol symbol, IAssemblySymbol assembly)
  {
    Accessibility accessibility = symbol.GetEffectiveAccessibility();
    return
      accessibility == Accessibility.Public ||
      accessibility == Accessibility.Internal && symbol.ContainingAssembly.GivesAccessTo(assembly);
  }

  public static Location? GetLocationFromAttributeDataOrDefault(this ISymbol symbol, AttributeData attributeData)
  {
    Location? firstLocation = null;
    SyntaxTree? attributeTree = attributeData.ApplicationSyntaxReference?.SyntaxTree;
    foreach (Location location in symbol.Locations)
    {
      if (location.SourceTree == attributeTree)
        return location;
      firstLocation ??= location;
    }
    return firstLocation;
  }
}
