using Everlong.DI.Generators.Extensions;
using Everlong.DI.Generators.Helpers;

namespace Everlong.DI.Generators.Injection;

[Generator(LanguageNames.CSharp)]
public sealed partial class MemberInjectionGenerator : IIncrementalGenerator
{
  public void Initialize(IncrementalGeneratorInitializationContext context)
  {
    // v2 anchor: generation is driven by the *shape* of the class, not by a class-level
    // attribute. A partial class is a candidate when it (syntactically) declares an
    // [Inject] member or lists IAutoInject in its base list. Semantic confirmation happens
    // in Transform; the predicate only does cheap token scans so it can run over every
    // class declaration in the compilation.
    IncrementalValuesProvider<Result<InjectionInfo?>> targets = context.SyntaxProvider
      .CreateSyntaxProvider(
        predicate: IsCandidateInjectionTarget,
        transform: static (ctx, token) => Transform(ctx, token)
      );

    context.ReportDiagnostics(targets.Select(static (item, _) => item.Errors));

    IncrementalValuesProvider<InjectionInfo> validTargets = targets
      .Where(static item => item.Value is not null)
      .Select(static (item, _) => item.Value!);

    context.RegisterImplementationSourceOutput(validTargets, WrappedExecute);
  }

  /// <summary>
  ///   Cheap syntax-only filter over every class declaration. Kept allocation-light:
  ///   only identifier-token scans, no semantic model.
  /// </summary>
  internal static bool IsCandidateInjectionTarget(SyntaxNode node, CancellationToken token)
  {
    if (token.IsCancellationRequested)
      return false;

    if (node is not ClassDeclarationSyntax classDeclaration)
      return false;

    if (!classDeclaration.Modifiers.Any(static m => m.IsKind(SyntaxKind.PartialKeyword)))
      return false;

    if (classDeclaration.BaseList is { Types.Count: > 0 } baseList
        && baseList.Types.Any(static t => EndsWithSimpleName(t.ToString(), Interfaces.IAutoInject)))
      return true;

    foreach (MemberDeclarationSyntax member in classDeclaration.Members)
    {
      if (member is TypeDeclarationSyntax)
        continue; // nested types are separate candidates; do not scan their members here

      if (member.AttributeLists.Count > 0
          && member.AttributeLists.Any(static l => l.Attributes.Any(IsInjectAttributeSyntax)))
        return true;
    }

    return false;
  }

  private static bool IsInjectAttributeSyntax(AttributeSyntax attribute)
    => EndsWithSimpleName(attribute.Name.ToString(), "Inject");

  // Matches the simple name of a (possibly qualified / generic) type name, e.g.
  // "Everlong.DI.IAutoInject", "IAutoInject", "global::Everlong.DI.IAutoInject" all end in
  // "IAutoInject" — but "NotIAutoInjectX" must not match, hence the boundary check on the
  // preceding character.
  private static bool EndsWithSimpleName(string text, string simpleName)
  {
    if (!text.EndsWith(simpleName, StringComparison.Ordinal))
      return false;

    int start = text.Length - simpleName.Length;
    return start == 0 || !(char.IsLetterOrDigit(text[start - 1]) || text[start - 1] == '_');
  }
}
