using Everlong.DI;
using Everlong.DI.Generators.DI;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Everlong.DI.Tests.DI;

public class ServiceRegistrationGeneratorVerificationTests
{
  [Fact]
  public void ShouldReportError_WhenMultipleServiceRegistratorsExist()
  {
    var source = @"
using Everlong.DI;

[ServiceRegistrar]
public partial class Table1 {}

[ServiceRegistrar]
public partial class Table2 {}
";
    var diagnostics = GetDiagnostics(source);
    Assert.Equal(2, diagnostics.Length);
    Assert.All(diagnostics, d => Assert.Equal("DIG0003", d.Id));
  }

  [Fact]
  public void ShouldGenerateForNestedServiceRegistrarInStaticContainer()
  {
    var source = @"
using Everlong.DI;

namespace TestApp;

public static partial class ServiceCollectionExtensions
{
    [ServiceRegistrar]
    private partial class RegistrationTable {}
}
";
    var generated = GetGeneratedSources(source);
    Assert.Contains("static partial class ServiceCollectionExtensions", generated);
    Assert.Contains("partial class RegistrationTable : IServiceRegistrar", generated);
    Assert.Contains("void RegisterServices(IServiceCollection services)", generated);
  }

  [Fact]
  public void ShouldGenerateForGlobalNamespaceServiceRegistrar()
  {
    var source = @"
using Everlong.DI;

[ServiceRegistrar]
public partial class RegistrationTable {}
";

    var generated = GetGeneratedSources(source);
    Assert.Contains("partial class RegistrationTable : IServiceRegistrar", generated);
    Assert.Contains("void RegisterServices(IServiceCollection services)", generated);
    Assert.DoesNotContain("namespace", generated);
  }

  private System.Collections.Immutable.ImmutableArray<Diagnostic> GetDiagnostics(string source)
    => RunGenerator(source).Diagnostics;

  private string GetGeneratedSources(string source)
  {
    var runResult = RunGenerator(source);
    return string.Join(
      Environment.NewLine,
      runResult.GeneratedTrees.Select(t => t.GetText().ToString()));
  }

  private GeneratorDriverRunResult RunGenerator(string source)
  {
    var syntaxTree = CSharpSyntaxTree.ParseText(source);

    var references = AppDomain.CurrentDomain.GetAssemblies()
      .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
      .Select(a => MetadataReference.CreateFromFile(a.Location))
      .Distinct()
      .ToList();

    references.Add(MetadataReference.CreateFromFile(typeof(ServiceRegistrarAttribute).Assembly.Location));

    var compilation = CSharpCompilation.Create(
      "TestApp",
      [syntaxTree],
      references,
      new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    var generator = new ServiceRegistrationGenerator();
    GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
    driver = driver.RunGenerators(compilation);
    return driver.GetRunResult();
  }
}
