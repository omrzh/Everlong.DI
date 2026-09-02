using Everlong.DI.Generators.Constants;
using Everlong.DI.Generators.Extensions;
using Everlong.DI.Generators.Models;

namespace Everlong.DI.Generators.Injection;

partial class MemberInjectionGenerator
{
  private static Result<InjectionInfo?> Transform(GeneratorSyntaxContext context, CancellationToken token)
  {
    using ImmutableArrayBuilder<DiagnosticInfo> diagnostics = ImmutableArrayBuilder<DiagnosticInfo>.Rent();

    try
    {
      if (context.Node is not ClassDeclarationSyntax classDeclaration)
        return new Result<InjectionInfo?>(null, diagnostics.ToImmutable());

      if (context.SemanticModel.GetDeclaredSymbol(classDeclaration, token) is not INamedTypeSymbol typeSymbol)
        return new Result<InjectionInfo?>(null, diagnostics.ToImmutable());

      if (!SymbolEqualityComparer.Default.Equals(typeSymbol.ContainingAssembly, context.SemanticModel.Compilation.Assembly))
        return new Result<InjectionInfo?>(null, diagnostics.ToImmutable());

      // Canonical-part dedupe: with the syntax-driven anchor, several partial parts of the
      // same type may each pass the candidate predicate (e.g. [Inject] members spread across
      // files). Emit exactly one generated partial — deterministically the candidate part
      // with the smallest (FilePath, Span). Which part is chosen is irrelevant to the output:
      // all members are collected from the merged type symbol.
      if (typeSymbol.DeclaringSyntaxReferences.Length > 1)
      {
        var canonical = typeSymbol.DeclaringSyntaxReferences
          .Where(r => r.GetSyntax(token) is ClassDeclarationSyntax c && IsCandidateInjectionTarget(c, token))
          .OrderBy(static r => r.SyntaxTree.FilePath, StringComparer.Ordinal)
          .ThenBy(static r => r.Span.Start)
          .FirstOrDefault();

        if (canonical is not null
            && (!ReferenceEquals(canonical.SyntaxTree, classDeclaration.SyntaxTree)
                || canonical.Span != classDeclaration.Span))
          return new Result<InjectionInfo?>(null, diagnostics.ToImmutable());
      }

      // --- v2 opt-in surface -------------------------------------------------------------
      // No class-level attribute exists anymore: [Inject] members anchor generation, and a
      // source `: IAutoInject` declaration opts a (typically memberless) class in as a
      // chain root with a generated Inject of its own.
      bool anchored = typeSymbol.Interfaces.Any(static i => i.ToDisplayString() == Interfaces.IAutoInjectFull);
      bool chainExposesInject = ChainExposesInject(typeSymbol);

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

          string typeName = propertySymbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
          // The generated partial accessor/backing-field declarations must carry the same
          // nullable annotation as the source partial property, or the compiler reports
          // CS9256 (signature mismatch). Nullable<T> value types (int?) and non-annotated
          // types already display correctly without an appended '?'.
          string declaredType = propertySymbol.Type.NullableAnnotation == NullableAnnotation.Annotated
            ? typeName + "?"
            : typeName;

          members.Add(new InjectedMember(
            propertySymbol.Name,
            typeName, declaredType,
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

          string typeName = fieldSymbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
          members.Add(new InjectedMember(
            fieldSymbol.Name,
            typeName, typeName,
            IsPartial: false, modifiers, IsField: true, isNullable,
            KeyExpression: injectAttribute.GetKeyExpression()));
        }
      }

      // Transparent levels: a partial class that merely sits between injectable levels —
      // no [Inject] members, no `: IAutoInject` — produces nothing. Derived classes chain
      // through it to the nearest generated ancestor Inject.
      if (members.Count == 0 && !anchored)
        return new Result<InjectionInfo?>(null, diagnostics.ToImmutable());

      var info = new InjectionInfo(
        HierarchyInfo.From(typeSymbol),
        members.ToImmutableArray().ToEquatableArray(),
        chainExposesInject,
        typeSymbol.IsSealed);
      return new Result<InjectionInfo?>(info, diagnostics.ToImmutable());
    }
    catch (OperationCanceledException) { throw; }
    catch (Exception ex)
    {
      diagnostics.Add(DiagnosticInfo.Create(Descriptors.TransformError, context.Node, ex.Message));
      return new Result<InjectionInfo?>(null, diagnostics.ToImmutable());
    }
  }

  private static AttributeData? GetInjectAttribute(ImmutableArray<AttributeData> attributes)
    => attributes.FirstOrDefault(static a => a.AttributeClass?.ToDisplayString() == Attributes.InjectFull);

  /// <summary>
  ///   Whether the base chain (starting at the direct base, walking to
  ///   <see cref="SpecialType.System_Object"/>) exposes an <c>Inject</c> to override.
  /// </summary>
  /// <remarks>
  ///   Purely source/compiled-symbol based, on purpose: a transform cannot observe sibling
  ///   generated output, so an intermediate link that would only expose <c>Inject</c>
  ///   through its own generated code must be recognized by recursion. Every walkable fact
  ///   here is a source fact:
  ///   <list type="bullet">
  ///     <item>
  ///       <description>
  ///         an ancestor (source or compiled) implements <see cref="IAutoInject"/> or
  ///         <see cref="IInjectable"/> (compiled ancestors carry the generated interface in
  ///         metadata; the interface list at the direct base already covers the whole
  ///         chain);
  ///       </description>
  ///     </item>
  ///     <item>
  ///       <description>
  ///         an ancestor declares its own <c>[Inject]</c> member (that class emits an
  ///         <c>Inject</c> of its own, virtual unless sealed — and a sealed class has no
  ///         derived classes).
  ///       </description>
  ///     </item>
  ///   </list>
  /// </remarks>
  private static bool ChainExposesInject(INamedTypeSymbol typeSymbol)
  {
    for (INamedTypeSymbol? current = typeSymbol.BaseType;
         current is not null && current.SpecialType != SpecialType.System_Object;
         current = current.BaseType)
    {
      if (current.AllInterfaces.Any(static i => i is { } itf
            && (itf.ToDisplayString() == Interfaces.IInjectableFull
                || itf.ToDisplayString() == Interfaces.IAutoInjectFull)))
        return true;

      if (current.GetMembers().Any(static m =>
            (m is IPropertySymbol or IFieldSymbol)
            && m.GetAttributes().Any(static a => a.AttributeClass?.ToDisplayString() == Attributes.InjectFull)))
        return true;
    }

    return false;
  }
}
