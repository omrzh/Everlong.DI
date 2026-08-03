using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Everlong.DI.Generators.Models;

internal sealed record LocationInfo(string FilePath, TextSpan TextSpan, LinePositionSpan LineSpan)
{
  public Location ToLocation() => Location.Create(FilePath, TextSpan, LineSpan);

  public static LocationInfo? CreateFrom(SyntaxNode node) => CreateFrom(node.GetLocation());

  public static LocationInfo? CreateFrom(Location? location)
  {
    if (location is null || location.SourceTree is null) return null;
    return new LocationInfo(location.SourceTree.FilePath, location.SourceSpan, location.GetLineSpan().Span);
  }
}

internal sealed record DiagnosticInfo(
    DiagnosticDescriptor Descriptor,
    LocationInfo? Location,
    ImmutableDictionary<string, string?> Properties,
    EquatableArray<string> Arguments)
{
  public bool Equals(DiagnosticInfo? other)
    => other is not null
       && Descriptor.Equals(other.Descriptor)
       && Location == other.Location
       && Arguments.Equals(other.Arguments);

  public override int GetHashCode()
  {
    HashCode hash = default;
    hash.Add(Descriptor);
    hash.Add(Location);
    hash.Add(Arguments);
    return hash.ToHashCode();
  }

  public Diagnostic ToDiagnostic()
  {
    if (Location is not null)
      return Diagnostic.Create(Descriptor, Location.ToLocation(), Properties, Arguments.ToArray());
    return Diagnostic.Create(Descriptor, null, Properties, Arguments.ToArray());
  }

  public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, ISymbol symbol, params object[] args)
  {
    Location location = symbol.Locations.First();
    return new(descriptor, LocationInfo.CreateFrom(location), ImmutableDictionary<string, string?>.Empty, args.Select(static arg => arg.ToString()).ToImmutableArray());
  }

  public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, SyntaxNode node, params object[] args)
  {
    Location location = node.GetLocation();
    return new(descriptor, LocationInfo.CreateFrom(location), ImmutableDictionary<string, string?>.Empty, args.Select(static arg => arg.ToString()).ToImmutableArray());
  }

  public static DiagnosticInfo Create(
    DiagnosticDescriptor descriptor, ISymbol symbol,
    ImmutableDictionary<string, string?> properties, params object[] args)
  {
    Location location = symbol.Locations.First();
    return new(descriptor, LocationInfo.CreateFrom(location), properties, args.Select(static arg => arg.ToString()).ToImmutableArray());
  }
}
