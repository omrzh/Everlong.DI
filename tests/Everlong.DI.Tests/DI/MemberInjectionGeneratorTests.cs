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
    // Deliberately avoids the range operator: Rider's parser misreports
    // `string[expr..]` slices, and the false error poisons the whole
    // test project's analysis. Substring is equivalent here.
    int injectStart = generated.IndexOf("void Inject", StringComparison.Ordinal);
    var injectBody = generated.Substring(injectStart);
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

  [Fact]
  public void Generate_When_Multiple_Partials_And_Injectable_Part_Sorts_First_By_Path()
  {
    // Sanity guard: when the [Injectable] partial's file path sorts first,
    // canonical-selection picks it and generation proceeds.
    var (generatorDiagnostics, compilationDiagnostics, generated) = RunGeneratorAndCompile(
      ("A.cs", @"
using Everlong.DI;

namespace TestApp;

[Injectable]
public partial class TestClass
{
    [Inject] public partial IMyService Service { get; }
}"),
      ("Services.cs", @"
namespace TestApp;

public interface IMyService {}"),
      ("Z.cs", @"
namespace TestApp;

public partial class TestClass { }"));

    Assert.Empty(generatorDiagnostics);
    var errors = compilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
    Assert.True(errors.Count == 0,
      "Generated partial must compile together with the source. Errors:\n" + string.Join("\n", errors.Select(e => e.ToString())));
    Assert.Contains("__injected_Service", generated);
  }

  [Fact]
  public void Generate_When_Multiple_Partials_And_Injectable_Part_Sorts_Last_By_Path()
  {
    // Bug repro (TMP-BUG-member-injection-partial-canonical): the [Injectable] partial
    // lives in Z.cs, but a plain partial in B.cs sorts first by SyntaxTree.FilePath.
    // Canonical selection must not depend on file path ordering — the attribute-bearing
    // part is the only one that can be canonical ([Injectable] is not AllowMultiple),
    // so the plain part must never cause generation to be skipped.
    var (generatorDiagnostics, compilationDiagnostics, generated) = RunGeneratorAndCompile(
      ("B.cs", @"
namespace TestApp;

public partial class TestClass { }"),
      ("Services.cs", @"
namespace TestApp;

public interface IMyService {}"),
      ("Z.cs", @"
using Everlong.DI;

namespace TestApp;

[Injectable]
public partial class TestClass
{
    [Inject] public partial IMyService Service { get; }
}"));

    Assert.Empty(generatorDiagnostics);
    var errors = compilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
    Assert.True(errors.Count == 0,
      "File path ordering of partial files must not skip generation. Errors:\n" + string.Join("\n", errors.Select(e => e.ToString())));
    Assert.Contains("__injected_Service", generated);
  }

  [Fact]
  public void Generate_When_Multiple_Partials_And_Inject_Members_Spread_Across_Parts()
  {
    // [Inject] 成员可以分布在任意 partial 部分,生成器按合并后的类型符号收集,
    // 不应只处理 canonical 文件里的成员。
    var (generatorDiagnostics, compilationDiagnostics, generated) = RunGeneratorAndCompile(
      ("B.cs", @"
using Everlong.DI;

namespace TestApp;

public partial class TestClass
{
    [Inject] public partial IMyService Other { get; }
}"),
      ("Services.cs", @"
namespace TestApp;

public interface IMyService {}
public interface IAnotherService {}"),
      ("Z.cs", @"
using Everlong.DI;

namespace TestApp;

[Injectable]
public partial class TestClass
{
    [Inject] public partial IAnotherService Service { get; }
}"));

    Assert.Empty(generatorDiagnostics);
    var errors = compilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
    Assert.True(errors.Count == 0,
      "Members declared in any partial part must be injected. Errors:\n" + string.Join("\n", errors.Select(e => e.ToString())));
    Assert.Contains("__injected_Service", generated);
    Assert.Contains("__injected_Other", generated);
  }

  [Fact]
  public void Generate_When_Another_Part_Carries_Injectable_Suffixed_Attribute()
  {
    // 病态/误报场景:另一个部分挂了名字以 "Injectable" 结尾的异类属性(例如两个
    // 程序集各有一个同名 InjectableAttribute)。去重分支应只影响这种场景,且当
    // canonical(路径最小)恰好是 [Injectable] 命中部分时仍正常生成、只产出一份。
    var (generatorDiagnostics, compilationDiagnostics, generated) = RunGeneratorAndCompile(
      ("A.cs", @"
using Everlong.DI;

namespace TestApp;

[Injectable]
public partial class TestClass
{
    [Inject] public partial IMyService Service { get; }
}"),
      ("Other.cs", @"
namespace Other;

[System.AttributeUsage(System.AttributeTargets.Class)]
public sealed class InjectableAttribute : System.Attribute { }
"),
      ("Services.cs", @"
namespace TestApp;

public interface IMyService {}"),
      ("Z.cs", @"
namespace TestApp;

[Other.Injectable]
public partial class TestClass { }"));

    Assert.Empty(generatorDiagnostics);
    var errors = compilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
    Assert.True(errors.Count == 0,
      "An Injectable-suffixed attribute on another part must not suppress generation. Errors:\n" + string.Join("\n", errors.Select(e => e.ToString())));
    Assert.Contains("__injected_Service", generated);
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
    => RunGeneratorAndCompile(("Test.cs", source));

  private static (ImmutableArray<Diagnostic> GeneratorDiagnostics, ImmutableArray<Diagnostic> CompilationDiagnostics, string Generated) RunGeneratorAndCompile(params (string Path, string Source)[] sources)
  {
    var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
    var syntaxTrees = sources
      .Select(s => CSharpSyntaxTree.ParseText(s.Source, parseOptions, path: s.Path))
      .ToArray();

    var references = AppDomain.CurrentDomain.GetAssemblies()
      .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
      .Select(a => MetadataReference.CreateFromFile(a.Location))
      .Distinct()
      .ToList();

    references.Add(MetadataReference.CreateFromFile(typeof(InjectAttribute).Assembly.Location));

    var compilation = CSharpCompilation.Create(
      "TestApp",
      syntaxTrees,
      references,
      new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    GeneratorDriver driver = CSharpGeneratorDriver.Create(new MemberInjectionGenerator());
    driver = driver.RunGenerators(compilation);
    var runResult = driver.GetRunResult();

    // Re-parse generated trees with the project's LangVersion (as a real build would)
    // and bind them together with the source trees.
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
