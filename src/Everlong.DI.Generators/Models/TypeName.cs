using Microsoft.CodeAnalysis;

namespace Everlong.DI.Generators.Models;

internal record TypeName(string FullyQualified)
{
  private const string GlobalPrefix = "global::";
  private readonly string? _namespace;
  private string? _fullName;
  private string? _shortName;
  private string? _safeName;
  private string? _xmlSafeShortName;
  private string? _xmlSafeFullName;

  public string FullName => _fullName ??= FullyQualified.StartsWith(GlobalPrefix)
                              ? FullyQualified.Substring(GlobalPrefix.Length)
                              : FullyQualified;

  public string Namespace
  {
    get
    {
      if (_namespace != null) return _namespace;
      var withoutGenerics = StripGenerics(FullName);
      var dot = withoutGenerics.LastIndexOf('.');
      return dot < 0 ? string.Empty : FullName.Substring(0, dot);
    }
    init => _namespace = value;
  }

  public string ShortName
  {
    get
    {
      if (_shortName != null) return _shortName;
      var withoutGenerics = StripGenerics(FullName);
      var dot = withoutGenerics.LastIndexOf('.');
      return _shortName = dot < 0 ? FullName : FullName.Substring(dot + 1);
    }
  }

  public string SafeName => _safeName ??= ShortName.Replace('<', '_').Replace('>', '_').Replace(',', '_');
  public string XmlSafeShortName => _xmlSafeShortName ??= ShortName.Replace("<", "&lt;").Replace(">", "&gt;");
  public string XmlSafeFullName => _xmlSafeFullName ??= FullyQualified.Replace("<", "&lt;").Replace(">", "&gt;");

  public static implicit operator TypeName(string fullyQualified) => new(fullyQualified);

  public static TypeName FromNamedTypeSymbol(INamedTypeSymbol symbol) =>
    new TypeName(symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
    {
      Namespace = symbol.ContainingNamespace.IsGlobalNamespace
                    ? string.Empty
                    : symbol.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                        .Replace("global::", "")
    };

  private static string StripGenerics(string name)
  {
    var angle = name.IndexOf('<');
    return angle < 0 ? name : name.Substring(0, angle);
  }

  public virtual bool Equals(TypeName? other) =>
    other is not null && FullyQualified == other.FullyQualified;

  public override int GetHashCode() => FullyQualified.GetHashCode();
}
