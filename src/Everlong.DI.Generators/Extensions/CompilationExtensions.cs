using System.Diagnostics.CodeAnalysis;

namespace Everlong.DI.Generators.Extensions;

internal static class CompilationExtensions
{
  public static bool HasLanguageVersionAtLeastEqualTo(this Compilation compilation, LanguageVersion languageVersion)
    => ((CSharpCompilation)compilation).LanguageVersion >= languageVersion;

  public static bool HasLanguageVersionGreaterThan(this Compilation compilation, LanguageVersion languageVersion)
    => ((CSharpCompilation)compilation).LanguageVersion > languageVersion;

  public static bool IsLanguageVersionPreview(this Compilation compilation)
    => ((CSharpCompilation)compilation).LanguageVersion == LanguageVersion.Preview;

  public static bool HasAccessibleTypeWithMetadataName(this Compilation compilation, string fullyQualifiedMetadataName)
  {
    INamedTypeSymbol? type = compilation.GetTypeByMetadataName(fullyQualifiedMetadataName);
    if (type is not null)
      return type.CanBeAccessedFrom(compilation.Assembly);

    type ??= compilation.Assembly.GetTypeByMetadataName(fullyQualifiedMetadataName);
    if (type is not null)
      return type.CanBeAccessedFrom(compilation.Assembly);

    foreach (IModuleSymbol module in compilation.Assembly.Modules)
    {
      foreach (IAssemblySymbol referencedAssembly in module.ReferencedAssemblySymbols)
      {
        if (referencedAssembly.GetTypeByMetadataName(fullyQualifiedMetadataName) is not INamedTypeSymbol currentType)
          continue;
        switch (currentType.GetEffectiveAccessibility())
        {
          case Accessibility.Public:
          case Accessibility.Internal when referencedAssembly.GivesAccessTo(compilation.Assembly):
            return true;
        }
      }
    }
    return false;
  }

  public static bool TryBuildNamedTypeSymbolMap<T>(
      this Compilation compilation,
      IEnumerable<KeyValuePair<T, string>> typeNames,
      [NotNullWhen(true)] out ImmutableDictionary<T, INamedTypeSymbol>? typeSymbols)
      where T : IEquatable<T>
  {
    ImmutableDictionary<T, INamedTypeSymbol>.Builder builder = ImmutableDictionary.CreateBuilder<T, INamedTypeSymbol>();
    builder.ValueComparer = SymbolEqualityComparer.Default;
    foreach (KeyValuePair<T, string> pair in typeNames)
    {
      if (compilation.GetTypeByMetadataName(pair.Value) is not INamedTypeSymbol attributeSymbol)
      {
        typeSymbols = null;
        return false;
      }
      builder.Add(pair.Key, attributeSymbol);
    }
    typeSymbols = builder.ToImmutable();
    return true;
  }
}
