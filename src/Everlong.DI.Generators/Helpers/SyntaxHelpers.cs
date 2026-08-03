using Everlong.DI.Generators.Models;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Everlong.DI.Generators.Helpers;

public static class SyntaxHelpers
{
  public static StatementSyntax Statement(string code) => ParseStatement(code);

  public static StatementSyntax Statement(string template, params (string placeholder, string value)[] replacements)
  {
    var syntax = ParseStatement(template);
    foreach (var (placeholder, value) in replacements)
    {
      while (true)
      {
        var token = syntax.DescendantTokens().FirstOrDefault(t => t.Text == placeholder);
        if (token == default) break;
        syntax = syntax.ReplaceToken(token, SyntaxFactory.Identifier(value));
      }
    }
    return syntax;
  }

  public static MemberDeclarationSyntax Member(string code) => ParseMemberDeclaration(code)!;

  public static MemberDeclarationSyntax Member(string template, params (string placeholder, string value)[] replacements)
  {
    var syntax = SyntaxFactory.ParseMemberDeclaration(template)!;
    foreach (var (placeholder, value) in replacements)
    {
      while (true)
      {
        var token = syntax.DescendantTokens().FirstOrDefault(t => t.Text == placeholder);
        if (token == default) break;
        syntax = syntax.ReplaceToken(token, SyntaxFactory.Identifier(value));
      }
    }
    return syntax;
  }

  public static IEnumerable<StatementSyntax> Statements(string code) =>
    ParseCompilationUnit(code).DescendantNodes().OfType<StatementSyntax>();

  public static MemberDeclarationSyntax WrapInClasses(MemberDeclarationSyntax member,
                                                      IEnumerable<ContainingTypeInfo> containingTypes)
  {
    var result = member;
    foreach (var info in containingTypes)
    {
      TypeDeclarationSyntax typeDecl = info.IsRecord
        ? RecordDeclaration(Token(SyntaxKind.RecordKeyword), info.Name)
        : ClassDeclaration(info.Name);
      var modifiers = new List<SyntaxToken>();
      if (info.IsStatic) modifiers.Add(Token(SyntaxKind.StaticKeyword));
      modifiers.Add(Token(SyntaxKind.PartialKeyword));
      typeDecl = typeDecl.WithModifiers(TokenList(modifiers)).WithMembers(SingletonList(result));
      result = typeDecl;
    }
    return result;
  }

  public static string GetAccessibilityString(Accessibility accessibility) => accessibility switch
  {
    Accessibility.Public => "public",
    Accessibility.Internal => "internal",
    Accessibility.Private => "private",
    Accessibility.Protected => "protected",
    Accessibility.ProtectedAndInternal => "private protected",
    Accessibility.ProtectedOrInternal => "protected internal",
    _ => ""
  };
}
