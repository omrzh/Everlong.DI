using Everlong.DI.Generators.Constants;
using Everlong.DI.Generators.Models;
using System.Collections.Immutable;
using Everlong.DI.Generators.Helpers;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Everlong.DI.Generators.DI;

[Generator(LanguageNames.CSharp)]
public sealed class ServiceRegistrationGenerator : IIncrementalGenerator
{
  public void Initialize(IncrementalGeneratorInitializationContext context)
  {
    var singleton = CreateProvider(context, Attributes.SingletonFull, "Singleton");
    var singletonGeneric = CreateProvider(context, Attributes.SingletonGenericFull, "Singleton");
    var transient = CreateProvider(context, Attributes.TransientFull, "Transient");
    var transientGeneric = CreateProvider(context, Attributes.TransientGenericFull, "Transient");
    var scoped = CreateProvider(context, Attributes.ScopedFull, "Scoped");
    var scopedGeneric = CreateProvider(context, Attributes.ScopedGenericFull, "Scoped");

    var allServices = singleton
        .Collect().Select(static (items, _) => items.AsEquatable())
        .Combine(singletonGeneric.Collect().Select(static (items, _) => items.AsEquatable()))
        .Combine(transient.Collect().Select(static (items, _) => items.AsEquatable()))
        .Combine(transientGeneric.Collect().Select(static (items, _) => items.AsEquatable()))
        .Combine(scoped.Collect().Select(static (items, _) => items.AsEquatable()))
        .Combine(scopedGeneric.Collect().Select(static (items, _) => items.AsEquatable()))
        .Select((x, _) =>
        {
          var (((((s, sg), t), tg), sc), scg) = x;
          return s.Concat(sg).Concat(t).Concat(tg).Concat(sc).Concat(scg).ToEquatableArray();
        });

    var serviceRegistrarProvider = context.SyntaxProvider
        .ForAttributeWithMetadataName(
            Attributes.ServiceRegistrarFull,
            predicate: PredicateHelper.IsPartialClassDecl,
            transform: (ctx, _) => TransformServiceRegistrar(ctx))
        .Where(static s => s != null)
        .Select(static (s, _) => s!)
        .Collect()
        .Select(static (items, _) => items.AsEquatable());

    var input = serviceRegistrarProvider.Combine(allServices);

    context.RegisterSourceOutput(input, Execute);
  }

  private static IncrementalValuesProvider<ServiceInfo> CreateProvider(
      IncrementalGeneratorInitializationContext context,
      string attributeName,
      string lifetime)
  {
    return context.SyntaxProvider
        .ForAttributeWithMetadataName(
            attributeName,
            predicate: static (node, _) => node is ClassDeclarationSyntax or RecordDeclarationSyntax,
            transform: (ctx, _) => Transform(ctx, lifetime))
        .SelectMany(static (items, _) => items);
  }

  private static IEnumerable<ServiceInfo> Transform(GeneratorAttributeSyntaxContext context, string lifetime)
  {
    if (context.TargetSymbol is not INamedTypeSymbol { TypeKind: TypeKind.Class or TypeKind.Struct } classSymbol)
      yield break;

    foreach (var attr in context.Attributes)
    {
      bool isGeneric = attr.AttributeClass?.IsGenericType ?? false;
      bool isEnumerable = false;

      if (isGeneric && attr.ConstructorArguments.Length >= 1)
      {
        if (attr.ConstructorArguments[0].Value is bool val)
          isEnumerable = val;
      }

      string implementationType = classSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
      string serviceType = implementationType;

      if (isGeneric && attr.AttributeClass?.TypeArguments.Length > 0)
      {
        serviceType = attr.AttributeClass.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
      }

      yield return new ServiceInfo(implementationType, serviceType, lifetime, isEnumerable, classSymbol.ContainingAssembly.Name);
    }
  }

  private static ServiceRegistrarInfo? TransformServiceRegistrar(GeneratorAttributeSyntaxContext ctx)
  {
    if (ctx.TargetSymbol is not INamedTypeSymbol symbol) return null;
    if (!PredicateHelper.IsPartialRecursively(symbol)) return null;

    var containingTypes = new List<ContainingTypeInfo>();
    var current = symbol.ContainingType;
    while (current != null)
    {
      containingTypes.Add(new ContainingTypeInfo(current.Name, string.Empty, current.IsStatic, current.IsRecord));
      current = current.ContainingType;
    }

    return new ServiceRegistrarInfo(
        symbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : symbol.ContainingNamespace.ToDisplayString(),
        symbol.Name,
        containingTypes.ToImmutableArray().AsEquatableArray(),
        LocationInfo.CreateFrom(symbol.Locations.FirstOrDefault()));
  }

  private static void Execute(SourceProductionContext context, (EquatableArray<ServiceRegistrarInfo> Tables, EquatableArray<ServiceInfo> Services) input)
  {
    var (tables, services) = input;
    if (tables.IsEmpty) return;

    if (tables.Length > 1)
    {
      foreach (var table in tables)
        context.ReportDiagnostic(Diagnostic.Create(Descriptors.MultipleServiceTables, table.Location?.ToLocation()));
      return;
    }

    var tableInfo = tables[0];
    var statements = new List<StatementSyntax>();
    foreach (var service in services)
    {
      var lifetimeEnum = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
          IdentifierName("ServiceLifetime"), IdentifierName(service.Lifetime));

      if (service.ServiceType == service.ImplementationType)
      {
        statements.Add(ParseStatement($"ServiceRegistrarHelper.EnsureConcreteType<{service.ImplementationType}>();"));
      }
      else
      {
        statements.Add(ParseStatement($"ServiceRegistrarHelper.VerifyImplementation<{service.ServiceType}, {service.ImplementationType}>();"));
      }

      if (service.IsEnumerable)
      {
        statements.Add(ExpressionStatement(
            InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("services"), IdentifierName("Add")))
            .WithArgumentList(ArgumentList(SingletonSeparatedList(
                Argument(ObjectCreationExpression(IdentifierName("ServiceDescriptor"))
                    .WithArgumentList(ArgumentList(SeparatedList([
                      Argument(TypeOfExpression(ParseTypeName(service.ServiceType))),
                      Argument(TypeOfExpression(ParseTypeName(service.ImplementationType))),
                      Argument(lifetimeEnum)
                    ])))))))));
      }
      else
      {
        statements.Add(ExpressionStatement(
            InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("services"), IdentifierName("TryAdd")))
            .WithArgumentList(ArgumentList(SingletonSeparatedList(
                Argument(ObjectCreationExpression(IdentifierName("ServiceDescriptor"))
                    .WithArgumentList(ArgumentList(SeparatedList([
                      Argument(TypeOfExpression(ParseTypeName(service.ServiceType))),
                      Argument(TypeOfExpression(ParseTypeName(service.ImplementationType))),
                      Argument(lifetimeEnum)
                    ])))))))));
      }
    }

    var method = MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), "RegisterServices")
        .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
        .WithParameterList(ParameterList(SingletonSeparatedList(
            Parameter(Identifier("services")).WithType(ParseTypeName("IServiceCollection")))))
        .WithBody(Block(statements));

    var modifiers = new List<SyntaxToken> { Token(SyntaxKind.PartialKeyword) };
    var classDecl = ClassDeclaration(tableInfo.ClassName)
        .WithModifiers(TokenList(modifiers))
        .WithBaseList(BaseList(SingletonSeparatedList<BaseTypeSyntax>(SimpleBaseType(IdentifierName("IServiceRegistrar")))))
        .WithMembers(SingletonList<MemberDeclarationSyntax>(method));

    var wrappedMember = SyntaxHelpers.WrapInClasses(classDecl, tableInfo.ContainingTypes);

    var compilationUnit = CompilationUnit()
        .AddUsings(
            UsingDirective(ParseName("Microsoft.Extensions.DependencyInjection")),
            UsingDirective(ParseName("Microsoft.Extensions.DependencyInjection.Extensions")),
            UsingDirective(ParseName("Everlong.DI")));

    if (string.IsNullOrEmpty(tableInfo.Namespace))
    {
      // Global namespace: emit the class without a namespace declaration.
      compilationUnit = compilationUnit.AddMembers(wrappedMember);
    }
    else
    {
      compilationUnit = compilationUnit.AddMembers(
        FileScopedNamespaceDeclaration(ParseName(tableInfo.Namespace))
          .WithMembers(SingletonList(wrappedMember)));
    }

    compilationUnit = compilationUnit
        .NormalizeWhitespace(indentation: "  ")
        .WithLeadingTrivia(ParseLeadingTrivia("// <auto-generated/>\r\n"));

    context.AddSource($"{tableInfo.ClassName}.g.cs", compilationUnit.ToFullString());
  }
}
