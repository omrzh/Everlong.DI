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

    // Guard: prevent double-injection unless Reinjectable
    if (!info.Reinjectable)
    {
      const string guardField = "__injected";
      members.Add(ParseMemberDeclaration($"private bool {guardField};\n")!);
      statements.Add(ParseStatement($"if ({guardField}) return;"));
      statements.Add(ParseStatement($"{guardField} = true;"));
    }

    if (info.BaseImplementsInject)
    {
      statements.Add(ParseStatement($"base.{Methods.Inject}({Args.Services});"));
    }

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

        members.Add(ParseMemberDeclaration(
                      $"[{Attributes.EditorBrowsable}({Framework.EditorBrowsableStateNever})] " +
                      $"private {member.Type.FullyQualified} {fieldName} = default!;")!);

        members.Add(ParseMemberDeclaration(
                      $"{member.Modifiers ?? "public"} {member.Type.FullyQualified} {member.Name} => {fieldName};")!);

        statements.Add(ParseStatement($"{fieldName} = {getServiceCall};"));
      }
      else
      {
        statements.Add(ParseStatement($"this.{member.Name} = {getServiceCall};"));
      }
    }

    // Call the partial hook so implementers can react after injection
    statements.Add(ParseStatement("OnInjected();"));

    var injectModifiers = TokenList(Token(SyntaxKind.PublicKeyword));
    if (info.BaseImplementsInject)
      injectModifiers = injectModifiers.Add(Token(SyntaxKind.OverrideKeyword));
    else if (!info.IsSealed)
      injectModifiers = injectModifiers.Add(Token(SyntaxKind.VirtualKeyword));
    // sealed + !baseImplements → public only（接口实现，不可 virtual）

    var injectMethod = MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), Identifier(Methods.Inject))
      .WithModifiers(injectModifiers)
      .WithParameterList(ParameterList(SingletonSeparatedList(
        Parameter(Identifier(Args.Services)).WithType(ParseTypeName(Interfaces.IServiceProvider)))))
      .WithBody(Block(statements));

    members.Add(injectMethod);

    // Emit a partial OnInjected declaration so classes can optionally implement it
    members.Add(ParseMemberDeclaration("partial void OnInjected();")!);

    IEnumerable<BaseTypeSyntax>? baseTypes = null;
    if (!info.BaseImplementsInject)
      baseTypes = [SimpleBaseType(IdentifierName(Interfaces.IInjectable))];

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
                      $"#nullable enable\n" +
                      compilationUnit.NormalizeWhitespace(indentation: "  ").ToFullString());
  }
}
