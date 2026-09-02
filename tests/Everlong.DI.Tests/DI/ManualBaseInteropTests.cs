using System.Collections.Immutable;
using Everlong.DI;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Everlong.DI.Generators.Injection;

namespace Everlong.DI.Tests.DI;

// Interop contract: a base class that implements IInjectable BY HAND, under
// member-anchored generated derived classes.
public class ManualBaseInteropTests
{
  // Awkward case 1: a hand-written `public void Inject(...)` is NON-virtual by default.
  // The generated derived override needs a virtual/abstract member → CS0506. The error is
  // loud but points at generated code; the fix belongs in the manual base (add `virtual`).
  [Fact]
  public void Manual_NonVirtual_Inject_Breaks_Generated_Derived()
  {
    var (_, compilationDiagnostics, _) = Run(@"using System;
using Everlong.DI;

namespace TestApp;

public interface IService { }

public partial class ManualBase : IInjectable
{
    public void Inject(IServiceProvider services) { }
}

public partial class Derived : ManualBase
{
    [Inject] public partial IService Service { get; }
}");
    Assert.Contains("CS0506", compilationDiagnostics.Select(d => d.Id));
  }

  // `virtual` (or abstract) manual implementation chains correctly: the derived level
  // overrides and forwards to the hand-written wiring.
  [Fact]
  public void Manual_Virtual_Inject_Chains()
  {
    var (generatorDiagnostics, compilationDiagnostics, generated) = Run(@"using System;
using Everlong.DI;

namespace TestApp;

public interface IService { }

public partial class ManualBase : IInjectable
{
    public virtual void Inject(IServiceProvider services) { }
}

public partial class Derived : ManualBase
{
    [Inject] public partial IService Service { get; }
}");
    Assert.Empty(generatorDiagnostics);
    Assert.DoesNotContain("CS0506", compilationDiagnostics.Select(d => d.Id));
    Assert.Contains("public override void Inject(IServiceProvider services)", generated);
    Assert.Contains("base.Inject(services);", generated);
  }

  [Fact]
  public void Manual_Abstract_Inject_Works()
  {
    var (generatorDiagnostics, compilationDiagnostics, _) = Run(@"using System;
using Everlong.DI;

namespace TestApp;

public interface IService { }

public abstract partial class ManualBase : IInjectable
{
    public abstract void Inject(IServiceProvider services);
}

public partial class Derived : ManualBase
{
    [Inject] public partial IService Service { get; }
}");
    Assert.Empty(generatorDiagnostics);
    Assert.DoesNotContain("CS0506", compilationDiagnostics.Select(d => d.Id));
  }

  // Awkward case 2: IAutoInject in source means "generator, make my Inject". Declaring it
  // while ALSO implementing Inject by hand makes the generator emit a duplicate root →
  // CS0111. Hand-written injectables must stay on plain IInjectable.
  [Fact]
  public void Manual_Inject_Plus_IAutoInject_Duplicates_Root()
  {
    var (_, compilationDiagnostics, _) = Run(@"using System;
using Everlong.DI;

namespace TestApp;

public interface IService { }

public partial class ManualBase : IAutoInject
{
    public void Inject(IServiceProvider services) { }
}

public partial class Derived : ManualBase
{
    [Inject] public partial IService Service { get; }
}");
    Assert.Contains("CS0111", compilationDiagnostics.Select(d => d.Id)); // duplicate Inject
  }

  private static (ImmutableArray<Diagnostic> Generator, ImmutableArray<Diagnostic> Compilation, string Generated) Run(string source)
  {
    var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
    var tree = CSharpSyntaxTree.ParseText(source, parseOptions, path: "Test.cs");
    var references = AppDomain.CurrentDomain.GetAssemblies()
      .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
      .Select(a => MetadataReference.CreateFromFile(a.Location)).Distinct().ToList();
    references.Add(MetadataReference.CreateFromFile(typeof(InjectAttribute).Assembly.Location));
    var compilation = CSharpCompilation.Create("TestApp", [tree], references,
      new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    var runResult = CSharpGeneratorDriver.Create(new MemberInjectionGenerator())
      .RunGenerators(compilation).GetRunResult();
    var trees = runResult.GeneratedTrees
      .Select(t => CSharpSyntaxTree.ParseText(t.GetText(), parseOptions, path: t.FilePath)).ToArray();
    var output = compilation.AddSyntaxTrees(trees).GetDiagnostics();
    var generated = string.Join(Environment.NewLine,
      runResult.GeneratedTrees.Select(t => t.GetText().ToString()));
    return (runResult.Diagnostics, output, generated);
  }
}
