using Everlong.DI.Generators.Extensions;
using Everlong.DI.Generators.Helpers;

namespace Everlong.DI.Generators.Injection;

[Generator(LanguageNames.CSharp)]
public sealed partial class MemberInjectionGenerator : IIncrementalGenerator
{
  public void Initialize(IncrementalGeneratorInitializationContext context)
  {
    IncrementalValuesProvider<Result<InjectionInfo?>> targets = context.SyntaxProvider
      .ForAttributeWithMetadataName(
        Attributes.InjectableFull,
        predicate: PredicateHelper.IsPartialClassDecl,
        transform: Transform
      );

    context.ReportDiagnostics(targets.Select(static (item, _) => item.Errors));

    IncrementalValuesProvider<InjectionInfo> validTargets = targets
      .Where(static item => item.Value is not null)
      .Select(static (item, _) => item.Value!);

    context.RegisterImplementationSourceOutput(validTargets, WrappedExecute);
  }
}
