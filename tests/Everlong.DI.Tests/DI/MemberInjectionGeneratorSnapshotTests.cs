using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Everlong.DI;
using Everlong.DI.Generators.Injection;
using VerifyTests;
using VerifyXunit;
using static VerifyXunit.Verifier;

namespace Everlong.DI.Tests.DI;

public class MemberInjectionGeneratorSnapshotTests
{
  private static readonly VerifySettings SnapshotSettings = new();

  static MemberInjectionGeneratorSnapshotTests()
  {
    // Store snapshots in a dedicated Snapshots/ directory
    SnapshotSettings.UseDirectory("Snapshots");
  }

  [Fact]
  public Task Generate_When_Class_Declares_Inject_Members()
  {
    var source = @"
using Everlong.DI;

namespace TestApp;

public partial class TestClass
{
    [Inject]
    private IMyService _service = null!;
}

public interface IMyService {}
";

    var (_, generatedTrees) = RunGenerator(source);
    return Verify(generatedTrees, SnapshotSettings);
  }

  [Fact]
  public Task Generate_When_Partial_Property()
  {
    var source = @"
using Everlong.DI;

namespace TestApp;

public partial class TestClass
{
    [Inject]
    public partial IMyService Service { get; }
}

public interface IMyService {}
";

    var (_, generatedTrees) = RunGenerator(source);
    return Verify(generatedTrees, SnapshotSettings);
  }

  private static (ImmutableArray<Diagnostic> Diagnostics, string GeneratedTrees) RunGenerator(string source)
  {
    var syntaxTree = CSharpSyntaxTree.ParseText(source);

    var references = AppDomain.CurrentDomain.GetAssemblies()
      .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
      .Select(a => MetadataReference.CreateFromFile(a.Location))
      .Distinct()
      .ToList();

    references.Add(MetadataReference.CreateFromFile(typeof(InjectAttribute).Assembly.Location));

    var compilation = CSharpCompilation.Create(
      "TestApp",
      [syntaxTree],
      references,
      new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    GeneratorDriver driver = CSharpGeneratorDriver.Create(new MemberInjectionGenerator());
    driver = driver.RunGenerators(compilation);
    var runResult = driver.GetRunResult();

    var generated = string.Join(
      Environment.NewLine + Environment.NewLine,
      runResult.GeneratedTrees.Select(t => t.GetText().ToString()));

    return (runResult.Diagnostics, generated);
  }
}
