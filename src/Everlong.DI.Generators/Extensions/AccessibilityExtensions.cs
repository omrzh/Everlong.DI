namespace Everlong.DI.Generators.Extensions;

internal static class AccessibilityExtensions
{
  public static SyntaxTokenList ToSyntaxTokenList(this Accessibility accessibility) => accessibility switch
  {
    Accessibility.NotApplicable => TokenList(),
    Accessibility.Private => TokenList(Token(SyntaxKind.PrivateKeyword)),
    Accessibility.ProtectedAndInternal => TokenList(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.ProtectedKeyword)),
    Accessibility.Protected => TokenList(Token(SyntaxKind.ProtectedKeyword)),
    Accessibility.Internal => TokenList(Token(SyntaxKind.InternalKeyword)),
    Accessibility.ProtectedOrInternal => TokenList(Token(SyntaxKind.ProtectedKeyword), Token(SyntaxKind.InternalKeyword)),
    Accessibility.Public => TokenList(Token(SyntaxKind.PublicKeyword)),
    _ => TokenList()
  };
}
