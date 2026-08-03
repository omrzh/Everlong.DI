using System.Collections.Immutable;
using Everlong.DI.Generators.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Everlong.DI.Generators.Models;

internal sealed partial record HierarchyInfo(
  string FilenameHint,
  string MetadataName,
  string Namespace,
  EquatableArray<TypeInfo> Hierarchy)
{
  public static HierarchyInfo From(INamedTypeSymbol typeSymbol)
  {
    using ImmutableArrayBuilder<TypeInfo> hierarchy = ImmutableArrayBuilder<TypeInfo>.Rent();

    for (INamedTypeSymbol? parent = typeSymbol;
         parent is not null;
         parent = parent.ContainingType)
    {
      string typeParameters = parent.TypeParameters.IsEmpty
                                ? string.Empty
                                : $"<{string.Join(", ", parent.TypeParameters.Select(t => t.Name))}>";

      string modifiers = parent.IsStatic ? "static" : string.Empty;

      hierarchy.Add(new TypeInfo(
                      parent.Name, typeParameters, parent.TypeKind, parent.IsRecord, modifiers,
                      BuildConstraintClauses(parent.TypeParameters)));
    }

    string ns = typeSymbol.ContainingNamespace.IsGlobalNamespace
      ? string.Empty
      : typeSymbol.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);

    // MetadataName carries the arity suffix (e.g. "TargetViewModel`1") but no type parameters,
    // so it stays a valid hint/file name even for generic types.
    var nameParts = new List<string>();
    for (INamedTypeSymbol? parent = typeSymbol; parent is not null; parent = parent.ContainingType)
      nameParts.Add(parent.MetadataName);
    nameParts.Reverse();
    string filenameHint = ns.Length == 0
      ? string.Join(".", nameParts)
      : $"{ns}.{string.Join(".", nameParts)}";

    return new(
      filenameHint,
      typeSymbol.MetadataName,
      ns,
      hierarchy.ToImmutable());
  }

  private static string BuildConstraintClauses(ImmutableArray<ITypeParameterSymbol> typeParameters)
  {
    var clauses = new List<string>();

    foreach (ITypeParameterSymbol typeParameter in typeParameters)
    {
      var parts = new List<string>();

      if (typeParameter.HasReferenceTypeConstraint)
        parts.Add(typeParameter.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated
                    ? "class?"
                    : "class");
      else if (typeParameter.HasValueTypeConstraint && typeParameter.HasUnmanagedTypeConstraint)
        parts.Add("unmanaged");
      else if (typeParameter.HasValueTypeConstraint)
        parts.Add("struct");

      if (typeParameter.HasNotNullConstraint)
        parts.Add("notnull");

      foreach (ITypeSymbol constraintType in typeParameter.ConstraintTypes)
        parts.Add(constraintType.GetFullyQualifiedNameWithNullabilityAnnotations());

      if (typeParameter.HasConstructorConstraint)
        parts.Add("new()");

      if (parts.Count > 0)
        clauses.Add($"where {typeParameter.Name} : {string.Join(", ", parts)}");
    }

    return string.Join(" ", clauses);
  }

  public CompilationUnitSyntax GetCompilationUnit(
    ImmutableArray<MemberDeclarationSyntax> memberDeclarations,
    IEnumerable<BaseTypeSyntax>? baseTypes = null,
    IEnumerable<UsingDirectiveSyntax>? usings = null)
  {
    TypeDeclarationSyntax typeDeclaration = Hierarchy[0].GetSyntax();

    if (baseTypes != null)
      typeDeclaration = typeDeclaration.WithBaseList(BaseList(SeparatedList(baseTypes)));

    typeDeclaration = typeDeclaration.WithMembers(List(memberDeclarations));

    for (int i = 1; i < Hierarchy.Length; i++)
    {
      typeDeclaration = Hierarchy[i].GetSyntax()
        .WithMembers(SingletonList<MemberDeclarationSyntax>(typeDeclaration));
    }

    var compilationUnit = CompilationUnit();

    if (usings != null)
      compilationUnit = compilationUnit.WithUsings(List(usings));

    if (!string.IsNullOrEmpty(Namespace))
    {
      compilationUnit = compilationUnit.AddMembers(
        FileScopedNamespaceDeclaration(ParseName(Namespace))
          .WithMembers(SingletonList<MemberDeclarationSyntax>(typeDeclaration)));
    }
    else
    {
      compilationUnit = compilationUnit.AddMembers(typeDeclaration);
    }

    return compilationUnit;
  }
}
