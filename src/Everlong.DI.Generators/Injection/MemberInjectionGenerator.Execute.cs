using Everlong.DI.Generators.Constants;
using Everlong.DI.Generators.Helpers;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Everlong.DI.Generators.Injection;

partial class MemberInjectionGenerator
{
  private static void WrappedExecute(SourceProductionContext context, InjectionInfo info)
    => ExecuteHelper.Execute(context, info, Execute);

  private static void Execute(SourceProductionContext context, InjectionInfo? info)
  {
    if (info == null) return;

    var statements = new List<StatementSyntax>();
    var members = new List<MemberDeclarationSyntax>();

    // Idempotency guard: Inject() assigns members on the first call only. v2 made this
    // unconditional — an instance is wired exactly once per lifetime; re-wiring across
    // scopes would capture scoped services into long-lived instances (a DI anti-pattern).
    const string guardField = Conventions.ReservedPrefix + "injected";
    members.Add(ParseMemberDeclaration($"private bool {guardField};\n")!);
    statements.Add(ParseStatement($"if ({guardField}) return;"));

    if (info.ChainExposesInject)
    {
      statements.Add(ParseStatement($"base.{Methods.Inject}({Args.Services});"));
    }

    // Two-phase injection, per level: first RESOLVE every member into a buffer local — a
    // throwing resolution (fail-fast) aborts here, before any member of this level has been
    // assigned (all-or-nothing). Only when every resolution succeeded are the members
    // assigned, then the guard commits.
    var locals = new List<string>(info.Members.Length);

    foreach (var member in info.Members)
    {
      string getServiceCall;
      string serviceMethod = member.IsNullable ? Methods.GetService : Methods.GetRequiredService;
      string keyedServiceMethod = member.IsNullable ? Methods.GetKeyedService : Methods.GetRequiredKeyedService;
      if (member.KeyExpression != null)
        getServiceCall = $"{Args.Services}.{keyedServiceMethod}<{member.Type.FullyQualified}>({member.KeyExpression})";
      else
        getServiceCall = $"{Args.Services}.{serviceMethod}<{member.Type.FullyQualified}>()";

      if (member is { IsPartial: true, IsField: false })
      {
        var fieldName = $"{Conventions.InjectedFieldPrefix}{member.Name}";

        // Declarations use DeclaredType (nullability-preserving); resolution calls keep using
        // the annotation-free Type.
        members.Add(ParseMemberDeclaration(
                      $"[{Attributes.EditorBrowsable}({Framework.EditorBrowsableStateNever})] " +
                      $"private {member.DeclaredType.FullyQualified} {fieldName} = default!;")!);

        members.Add(ParseMemberDeclaration(
                      $"{member.Modifiers ?? "public"} {member.DeclaredType.FullyQualified} {member.Name} => {fieldName};")!);
      }

      string local = $"{Conventions.InjectedLocalPrefix}{locals.Count}";
      locals.Add(local);
      statements.Add(ParseStatement($"{member.DeclaredType.FullyQualified} {local} = {getServiceCall};"));
    }

    for (int i = 0; i < info.Members.Length; i++)
    {
      InjectedMember member = info.Members[i];
      string local = locals[i];
      if (member is { IsPartial: true, IsField: false })
      {
        var fieldName = $"{Conventions.InjectedFieldPrefix}{member.Name}";
        statements.Add(ParseStatement($"{fieldName} = {local};"));
      }
      else
      {
        statements.Add(ParseStatement($"this.{member.Name} = {local};"));
      }
    }


    // Commit the guard only after every resolution succeeded (commit semantics): a throwing
    // GetRequiredService (fail-fast) must leave Δinjected false so the caller can fix the
    // provider and retry Inject() on the same instance. Kept before OnInjected() so a
    // re-entrant Inject() inside the hook is still a no-op.
    statements.Add(ParseStatement($"{guardField} = true;"));

    // Call the partial hook so implementers can react after injection
    // (guard is already committed at this point, so a re-entrant Inject() inside the hook
    // is a no-op).
    statements.Add(ParseStatement("OnInjected();"));

    var injectModifiers = TokenList(Token(SyntaxKind.PublicKeyword));
    if (info.ChainExposesInject)
      injectModifiers = injectModifiers.Add(Token(SyntaxKind.OverrideKeyword));
    else if (!info.IsSealed)
      injectModifiers = injectModifiers.Add(Token(SyntaxKind.VirtualKeyword));
    // sealed + no chain → plain `public void Inject`: this class starts a chain and is the
    // last one in it (sealed ⇒ nothing can derive and override), so `virtual` would never be
    // overridden — and C# forbids `virtual` in sealed classes anyway (CS0549). The method
    // simply implements the interface contract. sealed + chain → `override` is unaffected.

    var injectMethod = MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), Identifier(Methods.Inject))
      .WithModifiers(injectModifiers)
      .WithParameterList(ParameterList(SingletonSeparatedList(
        Parameter(Identifier(Args.Services)).WithType(ParseTypeName(Interfaces.IServiceProvider)))))
      .WithBody(Block(statements));

    members.Add(injectMethod);

    // Emit a partial OnInjected declaration so classes can optionally implement it
    members.Add(ParseMemberDeclaration("partial void OnInjected();")!);

    IEnumerable<BaseTypeSyntax>? baseTypes = null;
    if (!info.ChainExposesInject)
      // Chain start: stamp the v2 anchor. IAutoInject : IInjectable, so the generated type
      // satisfies the IInjectable resolution contract used by the injector wrappers.
      baseTypes = [SimpleBaseType(IdentifierName(Interfaces.IAutoInject))];

    var usings = new[]
    {
      UsingDirective(IdentifierName(Ns.System)),
      UsingDirective(ParseName(Ns.MsDi)),
      UsingDirective(ParseName(Ns.DiNamespace))
    };

    var compilationUnit = info.Hierarchy.GetCompilationUnit(members.ToImmutableArray(), baseTypes, usings);

    // The generated file may contain nullable annotations (e.g. "where TArgs : PageArgs?"
    // or nullable member types), so declare an explicit nullable context.
    context.AddSource($"{info.Hierarchy.FilenameHint}.Inject.g.cs",
                      $"// Δ-prefixed members are generated machinery; do not reference them.\n#nullable enable\n" +
                      compilationUnit.NormalizeWhitespace(indentation: "  ").ToFullString());
  }
}
