using Everlong.DI.Generators.Constants;
using Everlong.DI.Generators.Extensions;
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
    var singleton = CreateProvider(context, Attributes.SingletonFull, "Singleton", ServiceKind.Self);
    var singletonGeneric = CreateProvider(context, Attributes.SingletonGenericFull, "Singleton", ServiceKind.Generic);
    var transient = CreateProvider(context, Attributes.TransientFull, "Transient", ServiceKind.Self);
    var transientGeneric = CreateProvider(context, Attributes.TransientGenericFull, "Transient", ServiceKind.Generic);
    var scoped = CreateProvider(context, Attributes.ScopedFull, "Scoped", ServiceKind.Self);
    var scopedGeneric = CreateProvider(context, Attributes.ScopedGenericFull, "Scoped", ServiceKind.Generic);
    var alsoAs = CreateProvider(context, Attributes.AlsoAsFull, "AlsoAs", ServiceKind.AlsoAs);

    var allServices = singleton
        .Collect().Select(static (items, _) => items.AsEquatable())
        .Combine(singletonGeneric.Collect().Select(static (items, _) => items.AsEquatable()))
        .Combine(transient.Collect().Select(static (items, _) => items.AsEquatable()))
        .Combine(transientGeneric.Collect().Select(static (items, _) => items.AsEquatable()))
        .Combine(scoped.Collect().Select(static (items, _) => items.AsEquatable()))
        .Combine(scopedGeneric.Collect().Select(static (items, _) => items.AsEquatable()))
        .Combine(alsoAs.Collect().Select(static (items, _) => items.AsEquatable()))
        .Select((x, _) =>
        {
          var ((((((s, sg), t), tg), sc), scg), a) = x;
          // Distinct: ForAttributeWithMetadataName invokes the transform once per
          // attribute instance, each time with the full attribute list.
          return s.Concat(sg).Concat(t).Concat(tg).Concat(sc).Concat(scg).Concat(a).Distinct().ToEquatableArray();
        });

    var invalidAlsoAs = context.SyntaxProvider
        .ForAttributeWithMetadataName(
            Attributes.AlsoAsFull,
            predicate: static (node, _) => node is ClassDeclarationSyntax or RecordDeclarationSyntax,
            transform: static (ctx, _) => ValidateAlsoAsType(ctx))
        .Where(static d => d != null)
        .Select(static (d, _) => d!)
        .Collect()
        .Select(static (items, _) => items.AsEquatable());

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

    context.RegisterSourceOutput(invalidAlsoAs, static (spc, diagnostics) =>
    {
      foreach (var d in diagnostics)
        spc.ReportDiagnostic(Diagnostic.Create(d.Descriptor, d.Location?.ToLocation(), d.Arguments.ToArray()));
    });
  }

  private static IncrementalValuesProvider<ServiceInfo> CreateProvider(
      IncrementalGeneratorInitializationContext context,
      string attributeName,
      string lifetime,
      ServiceKind kind)
  {
    return context.SyntaxProvider
        .ForAttributeWithMetadataName(
            attributeName,
            predicate: static (node, _) => node is ClassDeclarationSyntax or RecordDeclarationSyntax,
            transform: (ctx, _) => Transform(ctx, lifetime, kind))
        .SelectMany(static (items, _) => items);
  }

  private static DiagnosticInfo? ValidateAlsoAsType(GeneratorAttributeSyntaxContext context)
  {
    if (context.TargetSymbol is not INamedTypeSymbol classSymbol)
      return null;

    foreach (var attr in context.Attributes)
    {
      if (IsAlsoAsCompatible(attr, classSymbol))
        continue;

      var tAlso = attr.AttributeClass?.TypeArguments.FirstOrDefault();
      string typeName = tAlso?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) ?? "?";
      return DiagnosticInfo.Create(Descriptors.AlsoAsTypeNotImplemented, context.TargetNode, typeName, classSymbol.Name);
    }
    return null;
  }

  private static IEnumerable<ServiceInfo> Transform(GeneratorAttributeSyntaxContext context, string lifetime, ServiceKind kind)
  {
    if (context.TargetSymbol is not INamedTypeSymbol { TypeKind: TypeKind.Class or TypeKind.Struct } classSymbol)
      yield break;

    foreach (var attr in context.Attributes)
    {
      if (kind == ServiceKind.AlsoAs && !IsAlsoAsCompatible(attr, classSymbol))
        continue;

      bool isEnumerable = false;
      string? keyExpression = null;

      if (attr.ConstructorArguments.Length > 0)
      {
        if (attr.ConstructorArguments[0].Value is bool enumerable)
        {
          // [Singleton<T>(isEnumerable: true)] — existing bool constructor.
          isEnumerable = enumerable;
        }
        else
        {
          // [Singleton<T>("key")] / [Singleton<T>("key", enumerable: true)] — keyed constructor.
          keyExpression = attr.GetKeyExpression();
          if (attr.ConstructorArguments.Length > 1 && attr.ConstructorArguments[1].Value is bool enumerableValue)
            isEnumerable = enumerableValue;
        }
      }

      string implementationType = classSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
      string serviceType = implementationType;

      if (kind is ServiceKind.Generic or ServiceKind.AlsoAs)
      {
        serviceType = attr.AttributeClass!.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
      }

      yield return new ServiceInfo(
          implementationType, serviceType, lifetime, isEnumerable,
          classSymbol.ContainingAssembly.Name, keyExpression, kind,
          LocationInfo.CreateFrom(classSymbol.Locations.FirstOrDefault()));
    }
  }

  private static bool IsAlsoAsCompatible(AttributeData attr, INamedTypeSymbol classSymbol)
  {
    var tAlso = attr.AttributeClass?.TypeArguments.FirstOrDefault();
    return tAlso is INamedTypeSymbol { TypeKind: TypeKind.Interface } alsoAsType
        && classSymbol.AllInterfaces.Contains(alsoAsType, SymbolEqualityComparer.Default);
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
    foreach (var group in services.GroupBy(static s => s.ImplementationType))
    {
      List<ServiceInfo> items = group.ToList();
      List<ServiceInfo> mains = items.Where(static i => i.Kind != ServiceKind.AlsoAs).ToList();
      List<ServiceInfo> alsoAs = items.Where(static i => i.Kind == ServiceKind.AlsoAs).ToList();

      // R0: exactly one lifetime per type.
      if (mains.Select(static m => m.Lifetime).Distinct().Count() > 1)
      {
        foreach (ServiceInfo m in mains)
          context.ReportDiagnostic(Diagnostic.Create(Descriptors.MultipleLifetimes, m.Location?.ToLocation(), m.ImplementationType));
        continue;
      }

      // R1: self registration and generic registration are mutually exclusive within a lifetime.
      if (mains.Any(static m => m.Kind == ServiceKind.Self) && mains.Any(static m => m.Kind == ServiceKind.Generic))
      {
        foreach (ServiceInfo m in mains)
          context.ReportDiagnostic(Diagnostic.Create(Descriptors.SelfAndGenericInSameLifetime, m.Location?.ToLocation(), m.ImplementationType));
        continue;
      }

      foreach (ServiceInfo m in mains)
      {
        string lifetime = $"ServiceLifetime.{m.Lifetime}";

        if (m.ServiceType == m.ImplementationType)
        {
          statements.Add(ParseStatement($"ServiceRegistrarHelper.EnsureConcreteType<{m.ImplementationType}>();"));
        }
        else
        {
          statements.Add(ParseStatement($"ServiceRegistrarHelper.VerifyImplementation<{m.ServiceType}, {m.ImplementationType}>();"));
        }

        string descriptor = m.KeyExpression is null
            ? $"new ServiceDescriptor(typeof({m.ServiceType}), typeof({m.ImplementationType}), {lifetime})"
            : $"ServiceDescriptor.Keyed{m.Lifetime}(typeof({m.ServiceType}), {m.KeyExpression}, typeof({m.ImplementationType}))";

        statements.Add(ParseStatement($"services.{(m.IsEnumerable ? "Add" : "TryAdd")}({descriptor});"));
      }

      if (alsoAs.Count == 0)
        continue;

      if (mains.Count == 0)
      {
        foreach (ServiceInfo a in alsoAs)
          context.ReportDiagnostic(Diagnostic.Create(Descriptors.AlsoAsMissingMain, a.Location?.ToLocation(), a.ImplementationType));
        continue;
      }

      if (mains.All(static m => m.Lifetime == "Transient"))
      {
        foreach (ServiceInfo a in alsoAs)
          context.ReportDiagnostic(Diagnostic.Create(Descriptors.AlsoAsOnTransient, a.Location?.ToLocation(), a.ImplementationType));
        continue;
      }

      if (mains.Count > 1)
      {
        foreach (ServiceInfo a in alsoAs)
          context.ReportDiagnostic(Diagnostic.Create(Descriptors.AlsoAsAmbiguousMain, a.Location?.ToLocation(), a.ImplementationType));
        continue;
      }

      ServiceInfo main = mains[0];
      if (main.IsEnumerable)
      {
        foreach (ServiceInfo a in alsoAs)
          context.ReportDiagnostic(Diagnostic.Create(Descriptors.AlsoAsOnEnumerableMain, a.Location?.ToLocation(), a.ImplementationType));
        continue;
      }

      foreach (ServiceInfo a in alsoAs)
      {
        statements.Add(ParseStatement($"services.{(a.IsEnumerable ? "Add" : "TryAdd")}({BuildForwardDescriptor(a, main)});"));
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

  private static string BuildForwardDescriptor(ServiceInfo alsoAs, ServiceInfo main)
  {
    string lifetime = $"ServiceLifetime.{main.Lifetime}";
    string factory;

    if (main.Kind == ServiceKind.Self)
    {
      // The main registration is the concrete class itself: the instance is
      // guaranteed to be assignable to the AlsoAs type, no defensive cast needed.
      string source = main.KeyExpression is null
          ? $"sp.GetRequiredService<{main.ImplementationType}>()"
          : $"sp.GetRequiredKeyedService<{main.ImplementationType}>({main.KeyExpression})";
      factory = $"sp => {source}";
    }
    else
    {
      // The main registration is a service type: under TryAdd the service may be
      // claimed by another implementation, so verify the resolved instance.
      string resolve = main.KeyExpression is null
          ? $"sp.GetRequiredService<{main.ServiceType}>()"
          : $"sp.GetRequiredKeyedService<{main.ServiceType}>({main.KeyExpression})";
      factory = $"sp => {{ var s = {resolve}; return s is {alsoAs.ServiceType} b ? b : throw new global::System.InvalidOperationException(" +
                $"\"AlsoAs forwarding of {alsoAs.ServiceType} via {main.ServiceType} failed: the main service is claimed by another implementation.\"); }}";
    }

    return alsoAs.KeyExpression is null
        ? $"new ServiceDescriptor(typeof({alsoAs.ServiceType}), {factory}, {lifetime})"
        : $"new ServiceDescriptor(typeof({alsoAs.ServiceType}), {alsoAs.KeyExpression}, {factory}, {lifetime})";
  }
}
