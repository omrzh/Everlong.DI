using System.Diagnostics.CodeAnalysis;

namespace Everlong.DI.Generators.Extensions;

internal static class AttributeDataExtensions
{
  public static bool HasNamedArgument<T>(this AttributeData attributeData, string name, T? value)
  {
    foreach (KeyValuePair<string, TypedConstant> properties in attributeData.NamedArguments)
    {
      if (properties.Key == name)
        return properties.Value.Value is T argumentValue && EqualityComparer<T?>.Default.Equals(argumentValue, value);
    }
    return false;
  }

  public static Location? GetLocation(this AttributeData attributeData)
  {
    if (attributeData.ApplicationSyntaxReference is { } syntaxReference)
      return syntaxReference.SyntaxTree.GetLocation(syntaxReference.Span);
    return null;
  }

  public static bool TryGetConstructorArgument<T>(this AttributeData attributeData, int index, [NotNullWhen(true)] out T? result)
  {
    if (attributeData.ConstructorArguments.Length > index && attributeData.ConstructorArguments[index].Value is T argument)
    {
      result = argument;
      return true;
    }
    result = default;
    return false;
  }

  /// <summary>
  ///   Produces a C# source expression for the keyed-service key at <paramref name="argumentIndex"/>,
  ///   or <see langword="null"/> when the argument is absent or null.
  /// </summary>
  /// <remarks>
  ///   Supports the key types accepted by attribute constructors: <see cref="Type"/>,
  ///   enums, strings, and numeric primitives.
  /// </remarks>
  public static string? GetKeyExpression(this AttributeData attributeData, int argumentIndex = 0)
  {
    if (attributeData.ConstructorArguments.Length <= argumentIndex)
      return null;

    TypedConstant arg = attributeData.ConstructorArguments[argumentIndex];
    if (arg.IsNull)
      return null;

    if (arg is { Kind: TypedConstantKind.Type, Value: ITypeSymbol typeSymbol })
      return $"typeof({typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})";

    if (arg is { Kind: TypedConstantKind.Enum, Value: not null, Type: not null })
    {
      string enumType = arg.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
      return $"({enumType}){arg.Value}";
    }

    if (arg is { Kind: TypedConstantKind.Primitive, Value: string s })
      return $"\"{s}\"";

    if (arg is { Kind: TypedConstantKind.Primitive, Value: not null })
      return arg.Value.ToString();

    return null;
  }

  public static T? GetNamedArgument<T>(this AttributeData attributeData, string name, T? fallback = default)
  {
    if (attributeData.TryGetNamedArgument(name, out T? value))
      return value;
    return fallback;
  }

  public static bool TryGetNamedArgument<T>(this AttributeData attributeData, string name, out T? value)
  {
    foreach (KeyValuePair<string, TypedConstant> properties in attributeData.NamedArguments)
    {
      if (properties.Key == name)
      {
        value = (T?)properties.Value.Value;
        return true;
      }
    }
    value = default;
    return false;
  }

  public static IEnumerable<T?> GetConstructorArguments<T>(this AttributeData attributeData)
      where T : class
  {
    static IEnumerable<T?> Enumerate(IEnumerable<TypedConstant> constants)
    {
      foreach (TypedConstant constant in constants)
      {
        if (constant.IsNull) yield return null;
        if (constant.Kind == TypedConstantKind.Primitive && constant.Value is T value)
          yield return value;
        else if (constant.Kind == TypedConstantKind.Array)
        {
          foreach (T? item in Enumerate(constant.Values))
            yield return item;
        }
      }
    }
    return Enumerate(attributeData.ConstructorArguments);
  }
}
