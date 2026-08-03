using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Everlong.DI.Generators.Models;

internal sealed record TypeInfo(
  string Name,
  string TypeParameters,
  TypeKind Kind,
  bool IsRecord,
  string? Modifiers = null,
  string ConstraintClauses = "")
{
  public TypeDeclarationSyntax GetSyntax()
  {
    TypeDeclarationSyntax declaration = Kind switch
    {
      TypeKind.Struct => StructDeclaration(Name),
      TypeKind.Interface => InterfaceDeclaration(Name),
      TypeKind.Class when IsRecord =>
        RecordDeclaration(Token(SyntaxKind.RecordKeyword), Name)
          .WithOpenBraceToken(Token(SyntaxKind.OpenBraceToken))
          .WithCloseBraceToken(Token(SyntaxKind.CloseBraceToken)),
      _ => ClassDeclaration(Name)
    };

    if (!string.IsNullOrEmpty(Modifiers))
    {
      foreach (var modifier in ParseTokens(Modifiers!))
        declaration = declaration.AddModifiers(modifier);
    }

    declaration = declaration.AddModifiers(Token(SyntaxKind.PartialKeyword));

    if (!string.IsNullOrEmpty(TypeParameters))
    {
      var dummyCode = $"class Dummy{TypeParameters} {{}}";
      if (!string.IsNullOrEmpty(ConstraintClauses))
        dummyCode = $"class Dummy{TypeParameters} {ConstraintClauses} {{}}";
      var compilationUnit = ParseCompilationUnit(dummyCode);
      var dummyClass = (ClassDeclarationSyntax)compilationUnit.Members[0];
      if (dummyClass.TypeParameterList != null)
        declaration = declaration.WithTypeParameterList(dummyClass.TypeParameterList);
      if (dummyClass.ConstraintClauses.Count > 0)
        declaration = declaration.WithConstraintClauses(dummyClass.ConstraintClauses);
    }

    return declaration;
  }
}
