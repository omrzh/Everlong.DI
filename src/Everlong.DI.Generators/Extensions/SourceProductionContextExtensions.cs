using System.Text;

namespace Everlong.DI.Generators.Extensions;

internal static class SourceProductionContextExtensions
{
  public static void AddSource(this SourceProductionContext context, string name, CompilationUnitSyntax compilationUnit)
  {
#if !ROSLYN_4_3_1_OR_GREATER
    name = name.Replace('+', '.').Replace('`', '_');
#endif
    context.AddSource(name, compilationUnit.GetText(Encoding.UTF8));
  }
}
