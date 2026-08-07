using Everlong.DI.Generators.Constants;
using Everlong.DI.Generators.Extensions;
using Everlong.DI.Generators.Models;
using System.Collections.Immutable;

namespace Everlong.DI.Generators.Injection;

partial class MemberInjectionGenerator
{
  private static Result<InjectionInfo?> Transform(GeneratorAttributeSyntaxContext context, CancellationToken token)
  {
    using ImmutableArrayBuilder<DiagnosticInfo> diagnostics = ImmutableArrayBuilder<DiagnosticInfo>.Rent();

    try
    {
      if (context.TargetNode is not ClassDeclarationSyntax classDeclaration)
        return new Result<InjectionInfo?>(null, diagnostics.ToImmutable());

      if (context.TargetSymbol is not INamedTypeSymbol typeSymbol)
        return new Result<InjectionInfo?>(null, diagnostics.ToImmutable());

      if (!SymbolEqualityComparer.Default.Equals(typeSymbol.ContainingAssembly, context.SemanticModel.Compilation.Assembly))
        return new Result<InjectionInfo?>(null, diagnostics.ToImmutable());

      if (typeSymbol.DeclaringSyntaxReferences.Length > 1)
      {
        var canonicalDeclaration = typeSymbol.DeclaringSyntaxReferences
          .OrderBy(static r => r.SyntaxTree.FilePath, StringComparer.Ordinal)
          .ThenBy(static r => r.Span.Start)
          .First();
        if (!ReferenceEquals(classDeclaration.SyntaxTree, canonicalDeclaration.SyntaxTree)
            || classDeclaration.Span != canonicalDeclaration.Span)
          return new Result<InjectionInfo?>(null, diagnostics.ToImmutable());
      }

      HierarchyInfo hierarchy = HierarchyInfo.From(typeSymbol);
      bool baseImplementsInject = BaseImplementsInject(typeSymbol);
      bool reinjectable = GetReinjectable(context.Attributes);
      List<InjectedMember> members = [];

      foreach (ISymbol memberSymbol in typeSymbol.GetMembers())
      {
        if (memberSymbol is IPropertySymbol propertySymbol)
        {
          AttributeData? injectAttribute = GetInjectAttribute(propertySymbol.GetAttributes());
          if (injectAttribute is null || propertySymbol.IsStatic) continue;

          bool isPartial = false;
          string? modifiers = null;
          bool isNullable = propertySymbol.NullableAnnotation == NullableAnnotation.Annotated;

          if (propertySymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(token) is PropertyDeclarationSyntax propertySyntax)
          {
            isPartial = propertySyntax.Modifiers.Any(static m => m.IsKind(SyntaxKind.PartialKeyword));
            modifiers = propertySyntax.Modifiers.ToString();
            isNullable = propertySyntax.Type is NullableTypeSyntax;
          }

          members.Add(new InjectedMember(
            propertySymbol.Name,
            propertySymbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            isPartial, modifiers, IsField: false, isNullable,
            KeyExpression: injectAttribute.GetKeyExpression()));
        }
        else if (memberSymbol is IFieldSymbol fieldSymbol)
        {
          AttributeData? injectAttribute = GetInjectAttribute(fieldSymbol.GetAttributes());
          if (injectAttribute is null || fieldSymbol.IsStatic) continue;

          if (fieldSymbol.IsReadOnly)
            return new Result<InjectionInfo?>(null, diagnostics.ToImmutable());

          string? modifiers = null;
          bool isNullable = fieldSymbol.NullableAnnotation == NullableAnnotation.Annotated;
          if (fieldSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(token) is VariableDeclaratorSyntax
              { Parent: VariableDeclarationSyntax { Parent: BaseFieldDeclarationSyntax fieldDeclaration } })
          {
            modifiers = fieldDeclaration.Modifiers.ToString();
            isNullable = fieldDeclaration.Declaration.Type is NullableTypeSyntax;
          }

          members.Add(new InjectedMember(
            fieldSymbol.Name,
            fieldSymbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            IsPartial: false, modifiers, IsField: true, isNullable,
            KeyExpression: injectAttribute.GetKeyExpression()));
        }
      }

      if (members.Count == 0)
        return new Result<InjectionInfo?>(null, diagnostics.ToImmutable());

      var info = new InjectionInfo(
        hierarchy, members.ToImmutableArray().ToEquatableArray(), baseImplementsInject, typeSymbol.IsSealed, reinjectable);
      return new Result<InjectionInfo?>(info, diagnostics.ToImmutable());
    }
    catch (OperationCanceledException) { throw; }
    catch (Exception ex)
    {
      diagnostics.Add(DiagnosticInfo.Create(Descriptors.TransformError, context.TargetNode, ex.Message));
      return new Result<InjectionInfo?>(null, diagnostics.ToImmutable());
    }
  }

  private static AttributeData? GetInjectAttribute(ImmutableArray<AttributeData> attributes)
    => attributes.FirstOrDefault(static a => a.AttributeClass?.ToDisplayString() == Attributes.InjectFull);

  private static bool BaseImplementsInject(INamedTypeSymbol typeSymbol)
  {
    INamedTypeSymbol? baseType = typeSymbol.BaseType;
    if (baseType is null || baseType.SpecialType == SpecialType.System_Object)
      return false;

    return baseType.AllInterfaces.Any(static i => i.Name == Interfaces.IInjectable)
           || baseType.GetMembers().Any(static m =>
             (m is IPropertySymbol or IFieldSymbol)
             && m.GetAttributes().Any(static a => a.AttributeClass?.ToDisplayString() == Attributes.InjectFull));
  }

  private static bool GetReinjectable(ImmutableArray<AttributeData> attributes)
  {
    foreach (var attr in attributes)
    {
      if (attr.AttributeClass?.ToDisplayString() != Attributes.InjectableFull)
        continue;
      foreach (KeyValuePair<string, TypedConstant> namedArg in attr.NamedArguments)
      {
        if (namedArg.Key == "Reinjectable" && namedArg.Value.Value is bool b)
          return b;
      }
    }
    return false;
  }
}
