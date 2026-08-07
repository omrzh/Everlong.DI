using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Everlong.DI;
using Everlong.DI.Generators.Injection;

namespace Everlong.DI.Tests.DI;

public class MemberInjectionGeneratorTests
{
  [Fact]
  public void Generate_When_Class_Has_Injectable()
  {
    var source = @"
using Everlong.DI;

namespace TestApp;

[Injectable]
public partial class TestClass
{
    [Inject]
    private IMyService _service = null!;
}

public interface IMyService {}
";

    var runResult = RunGenerator(source);
    var generated = string.Join(
      Environment.NewLine,
      runResult.GeneratedTrees.Select(t => t.GetText().ToString()));

    Assert.Contains("partial class TestClass", generated);
    Assert.Contains("void Inject(IServiceProvider services)", generated);
    Assert.Contains("this._service = services.GetRequiredService<global::TestApp.IMyService>();", generated);
  }

  [Fact]
  public void Skip_When_Class_Misses_Injectable()
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

    var runResult = RunGenerator(source);
    var generated = string.Join(
      Environment.NewLine,
      runResult.GeneratedTrees.Select(t => t.GetText().ToString()));

    Assert.DoesNotContain("void Inject(IServiceProvider services)", generated);
  }

  [Fact]
  public void Generate_Use_GetService_When_Member_Type_Is_Nullable()
  {
    var source = @"
#nullable enable
using Everlong.DI;

namespace TestApp;

[Injectable]
public partial class TestClass
{
    [Inject]
    private IMyService? _service;
}

public interface IMyService {}
";

    var runResult = RunGenerator(source);
    var generated = string.Join(
      Environment.NewLine,
      runResult.GeneratedTrees.Select(t => t.GetText().ToString()));

    Assert.Contains("this._service = services.GetService<global::TestApp.IMyService>();", generated);
    Assert.DoesNotContain("this._service = services.GetRequiredService<global::TestApp.IMyService>();", generated);
  }

  [Fact]
  public void Skip_When_Injected_Field_Is_Readonly()
  {
    var source = @"
using Everlong.DI;

namespace TestApp;

[Injectable]
public partial class TestClass
{
    [Inject]
    private readonly IMyService _service = null!;
}

public interface IMyService {}
";

    var runResult = RunGenerator(source);
    var generated = string.Join(
      Environment.NewLine,
      runResult.GeneratedTrees.Select(t => t.GetText().ToString()));

    Assert.DoesNotContain("void Inject(IServiceProvider services)", generated);
    Assert.DoesNotContain("SetValue(this", generated);
  }

  [Fact]
  public void Generate_Guard_When_Not_Reinjectable()
  {
    var source = @"
using Everlong.DI;

namespace TestApp;

[Injectable]
public partial class TestClass
{
    [Inject]
    private IMyService _service = null!;
}

public interface IMyService {}
";

    var runResult = RunGenerator(source);
    var generated = string.Join(
      Environment.NewLine,
      runResult.GeneratedTrees.Select(t => t.GetText().ToString()));

    Assert.Contains("bool __injected;", generated);
    Assert.Contains("if (__injected)", generated);
    Assert.Contains("__injected = true", generated);
  }

  [Fact]
  public void Skip_Guard_When_Reinjectable()
  {
    var source = @"
using Everlong.DI;

namespace TestApp;

[Injectable(Reinjectable = true)]
public partial class TestClass
{
    [Inject]
    private IMyService _service = null!;
}

public interface IMyService {}
";

    var runResult = RunGenerator(source);
    var generated = string.Join(
      Environment.NewLine,
      runResult.GeneratedTrees.Select(t => t.GetText().ToString()));

    Assert.DoesNotContain("__injected", generated);
    Assert.DoesNotContain("if (__injected) return;", generated);
  }

  [Fact]
  public void Generate_Guard_When_Overriding_Base_Inject()
  {
    var source = @"
using Everlong.DI;

namespace TestApp;

public interface IMyService {}

[Injectable]
public partial class BaseClass : IInjectable
{
    [Inject] private IMyService _svc;
}

[Injectable]
public partial class DerivedClass : BaseClass
{
    [Inject] private IMyService _extra;
}
";

    var runResult = RunGenerator(source);
    var generated = string.Join(
      Environment.NewLine,
      runResult.GeneratedTrees.Select(t => t.GetText().ToString()));

    // Both classes should have the guard
    Assert.Contains("__injected", generated);
  }

  [Fact]
  public void Generate_Contains_OnInjected_Call()
  {
    var source = @"
using Everlong.DI;

namespace TestApp;

[Injectable]
public partial class TestClass
{
    [Inject]
    private IMyService _service = null!;
}

public interface IMyService {}
";

    var runResult = RunGenerator(source);
    var generated = string.Join(
      Environment.NewLine,
      runResult.GeneratedTrees.Select(t => t.GetText().ToString()));

    Assert.Contains("partial void OnInjected();", generated);
    Assert.Contains("OnInjected();", generated);
  }

  [Fact]
  public void Generate_OnInjected_After_All_Injections()
  {
    var source = @"
using Everlong.DI;

namespace TestApp;

[Injectable]
public partial class TestClass
{
    [Inject] private IMyService _a;
    [Inject] private IMyService _b;
}

public interface IMyService {}
";

    var runResult = RunGenerator(source);
    var generated = string.Join(
      Environment.NewLine,
      runResult.GeneratedTrees.Select(t => t.GetText().ToString()));

    // OnInjected should come after all member assignments
    // Note: no parentheses around the range bound — Rider's parser misreports
    // range expressions with a parenthesized left bound (RSRP bug class).
    var injectBody = generated[generated.IndexOf("void Inject", StringComparison.Ordinal)..];
    var lastAssignment = injectBody.LastIndexOf("= services.", StringComparison.Ordinal);
    var onInjectedPos = injectBody.IndexOf("OnInjected();", StringComparison.Ordinal);
    Assert.True(
      lastAssignment >= 0 && onInjectedPos >= 0 && lastAssignment < onInjectedPos,
      "OnInjected() must be called after all member injections");
  }

  [Fact]
  public void Generate_OnInjected_When_Reinjectable()
  {
    var source = @"
using Everlong.DI;

namespace TestApp;

[Injectable(Reinjectable = true)]
public partial class TestClass
{
    [Inject]
    private IMyService _service = null!;
}

public interface IMyService {}
";

    var runResult = RunGenerator(source);
    var generated = string.Join(
      Environment.NewLine,
      runResult.GeneratedTrees.Select(t => t.GetText().ToString()));

    Assert.Contains("partial void OnInjected();", generated);
    Assert.Contains("OnInjected();", generated);
  }

  [Fact]
  public void Generate_OnInjected_When_Nullable()
  {
    var source = @"
#nullable enable
using Everlong.DI;

namespace TestApp;

[Injectable]
public partial class TestClass
{
    [Inject]
    private IMyService? _service;
}

public interface IMyService {}
";

    var runResult = RunGenerator(source);
    var generated = string.Join(
      Environment.NewLine,
      runResult.GeneratedTrees.Select(t => t.GetText().ToString()));

    Assert.Contains("partial void OnInjected();", generated);
    Assert.Contains("OnInjected();", generated);
  }

  [Fact]
  public void Generate_Partial_Class_When_Generic_With_BaseClass_And_Constraints()
  {
    // Case 2 from the report: generic [Injectable] partial class with a base class,
    // a type-parameter constraint, and [Inject] on partial properties.
    var source = @"
#nullable enable
using Everlong.DI;

namespace TestApp;

public class ObservableObject { }

public class PageArgs { }

public interface IShellManager { }

public interface ILayer { }

[Injectable]
public abstract partial class TargetViewModel<TArgs> : ObservableObject where TArgs : PageArgs?
{
    [Inject] public partial IShellManager Manager { get; }

    [Inject] public partial ILayer Navigator { get; }
}
";

    var (generatorDiagnostics, compilationDiagnostics, generated) = RunGeneratorAndCompile(source);

    Assert.Empty(generatorDiagnostics);
    var errors = compilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
    Assert.True(errors.Count == 0,
      "Generated partial must compile together with the source. Errors:\n" + string.Join("\n", errors.Select(e => e.ToString())));

    // The generated partial must repeat the type-parameter constraint clause,
    // otherwise the compiler reports CS0265 (inconsistent constraints).
    Assert.Contains("where TArgs : global::TestApp.PageArgs?", generated);
  }

  [Fact]
  public void Generate_Partial_Class_When_NonGeneric_With_BaseClass()
  {
    // Case 1 from the report: non-generic [Injectable] partial class with a base class
    // and [Inject] on partial properties.
    var source = @"
#nullable enable
using Everlong.DI;

namespace TestApp;

public class ObservableObject { }

public interface IShellManager { }

public interface ILayer { }

[Injectable]
public abstract partial class TargetViewModel : ObservableObject
{
    [Inject] public partial IShellManager Manager { get; }

    [Inject] public partial ILayer Navigator { get; }
}
";

    var (generatorDiagnostics, compilationDiagnostics, _) = RunGeneratorAndCompile(source);

    Assert.Empty(generatorDiagnostics);
    var errors = compilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
    Assert.True(errors.Count == 0,
      "Generated partial must compile together with the source. Errors:\n" + string.Join("\n", errors.Select(e => e.ToString())));
  }

  [Fact]
  public void Generate_Partial_Class_When_Generic_With_Special_Constraints()
  {
    // The generated partial must repeat special constraints (class / struct / unmanaged / new())
    // in the canonical order, otherwise the compiler reports CS0265.
    var source = @"
using Everlong.DI;

namespace TestApp;

public interface IMyService {}

[Injectable]
public partial class ClassStore<T> : IMyService where T : class, new()
{
    [Inject] private IMyService _svc = null!;
}

[Injectable]
public partial class StructStore<T> : IMyService where T : struct
{
    [Inject] private IMyService _svc = null!;
}

[Injectable]
public partial class UnmanagedStore<T> : IMyService where T : unmanaged
{
    [Inject] private IMyService _svc = null!;
}
";

    var (generatorDiagnostics, compilationDiagnostics, generated) = RunGeneratorAndCompile(source);

    Assert.Empty(generatorDiagnostics);
    var errors = compilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
    Assert.True(errors.Count == 0,
      "Generated partial must compile together with the source. Errors:\n" + string.Join("\n", errors.Select(e => e.ToString())));

    Assert.Contains("where T : class, new()", generated);
    Assert.Contains("where T : struct", generated);
    Assert.Contains("where T : unmanaged", generated);
  }

  private static GeneratorDriverRunResult RunGenerator(string source)
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
    return driver.GetRunResult();
  }

  private static (ImmutableArray<Diagnostic> GeneratorDiagnostics, ImmutableArray<Diagnostic> CompilationDiagnostics, string Generated) RunGeneratorAndCompile(string source)
  {
    var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
    var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);

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

    // Re-parse generated trees with the project's LangVersion (as a real build would)
    // and bind them together with the source tree.
    var generatedTrees = runResult.GeneratedTrees
      .Select(t => CSharpSyntaxTree.ParseText(t.GetText(), parseOptions, path: t.FilePath))
      .ToArray();
    var outputCompilation = compilation.AddSyntaxTrees(generatedTrees);

    var generated = string.Join(
      Environment.NewLine,
      runResult.GeneratedTrees.Select(t => t.GetText().ToString()));

    return (runResult.Diagnostics, outputCompilation.GetDiagnostics(), generated);
  }
}
