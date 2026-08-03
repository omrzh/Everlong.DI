namespace Everlong.DI.Generators.Extensions;

internal static class IncrementalGeneratorInitializationContextExtensions
{
  public static void RegisterConditionalSourceOutput(
      this IncrementalGeneratorInitializationContext context,
      IncrementalValueProvider<bool> source,
      Action<SourceProductionContext> action)
  {
    context.RegisterSourceOutput(source, (context, condition) =>
    {
      if (condition) action(context);
    });
  }

  public static void RegisterConditionalImplementationSourceOutput<T>(
      this IncrementalGeneratorInitializationContext context,
      IncrementalValueProvider<(bool Condition, T State)> source,
      Action<SourceProductionContext, T> action)
  {
    context.RegisterImplementationSourceOutput(source, (context, item) =>
    {
      if (item.Condition) action(context, item.State);
    });
  }

  public static void ReportDiagnostics(this IncrementalGeneratorInitializationContext context,
      IncrementalValuesProvider<ImmutableArray<Diagnostic>> diagnostics)
  {
    context.RegisterSourceOutput(diagnostics, static (context, diagnostics) =>
    {
      foreach (Diagnostic diagnostic in diagnostics)
        context.ReportDiagnostic(diagnostic);
    });
  }

  public static void ReportDiagnostics(this IncrementalGeneratorInitializationContext context,
      IncrementalValuesProvider<EquatableArray<DiagnosticInfo>> diagnostics)
  {
    context.RegisterSourceOutput(diagnostics.Select(static (arr, _) =>
    {
      var builder = ImmutableArray.CreateBuilder<Diagnostic>(arr.Length);
      foreach (var diag in arr)
        builder.Add(diag.ToDiagnostic());
      return builder.ToImmutable();
    }), static (context, diagnostics) =>
    {
      foreach (Diagnostic diagnostic in diagnostics)
        context.ReportDiagnostic(diagnostic);
    });
  }

  public static void ReportDiagnostics(this IncrementalGeneratorInitializationContext context,
      IncrementalValuesProvider<Diagnostic> diagnostics)
  {
    context.RegisterSourceOutput(diagnostics, static (context, diagnostic) => context.ReportDiagnostic(diagnostic));
  }
}
