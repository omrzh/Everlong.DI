using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Everlong.DI.CodeFixers;

namespace Everlong.DI.Tests;

/// <summary>
///   Tests for <see cref="PartialKeywordCodeFixProvider"/> — verifying the syntax transformation
///   without the full analyzer/code-fix pipeline (since the owning analyzer also fires other diagnostics).
/// </summary>
public class PartialKeywordCodeFixTests
{
  [Fact]
  public void MakeTypePartial_ShouldAddPartialKeyword()
  {
    var source = "public class TestClass { }";
    var tree = CSharpSyntaxTree.ParseText(source);
    var root = tree.GetRoot();
    var typeDecl = root.DescendantNodes().OfType<TypeDeclarationSyntax>().First();

    var partialToken = SyntaxFactory.Token(SyntaxKind.PartialKeyword).WithTrailingTrivia(SyntaxFactory.Space);
    var newModifiers = typeDecl.Modifiers.Add(partialToken);
    var newTypeDecl = typeDecl.WithModifiers(newModifiers);
    var newRoot = root.ReplaceNode(typeDecl, newTypeDecl);

    var result = newRoot.ToFullString();
    Assert.Contains("public partial class TestClass", result);
  }
}
