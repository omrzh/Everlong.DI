using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Everlong.DI;
using Everlong.DI.Generators.Injection;

namespace Everlong.DI.Tests.DI;

public class MemberInjectionGeneratorTests
{
  [Fact]
  public void Generate_When_Only_Inject_Members_Declared()
  {
    // v2: no class-level attribute exists. [Inject] members alone anchor generation; the
    // class becomes a chain start: virtual Inject + the IAutoInject stamp.
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

    Assert.Contains("partial class TestClass : IAutoInject", generated);
    Assert.Contains("public virtual void Inject(IServiceProvider services)", generated);
    Assert.Contains("__inject_value_0 = services.GetRequiredService<global::TestApp.IMyService>();", generated);
    Assert.Contains("this._service = __inject_value_0;", generated);
  }

  [Fact]
  public void Generate_Use_GetService_When_Member_Type_Is_Nullable()
  {
    var source = @"
#nullable enable
using Everlong.DI;

namespace TestApp;

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

    Assert.Contains("__inject_value_0 = services.GetService<global::TestApp.IMyService>();", generated);
    Assert.Contains("this._service = __inject_value_0;", generated);
    Assert.DoesNotContain("services.GetRequiredService<global::TestApp.IMyService>();", generated);
  }

  [Fact]
  public void Skip_When_Injected_Field_Is_Readonly()
  {
    var source = @"
using Everlong.DI;

namespace TestApp;

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
  public void Generate_Idempotency_Guard()
  {
    // v2: the idempotency guard is unconditional — Inject() wires an instance exactly once.
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

    Assert.Contains("bool Δinjected;", generated);
    Assert.Contains("if (Δinjected)", generated);
    Assert.Contains("Δinjected = true", generated);
  }

  [Fact]
  public void Generate_Guard_When_Overriding_Base_Inject()
  {
    var source = @"
using Everlong.DI;

namespace TestApp;

public interface IMyService {}

public partial class BaseClass : IInjectable
{
    [Inject] private IMyService _svc;
}

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
    Assert.Contains("Δinjected", generated);
  }

  [Fact]
  public void Generate_Contains_OnInjected_Call()
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

    Assert.Contains("partial void OnInjected();", generated);
    Assert.Contains("OnInjected();", generated);
  }

  [Fact]
  public void Generate_OnInjected_After_All_Injections()
  {
    var source = @"
using Everlong.DI;

namespace TestApp;

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
  public void Generate_OnInjected_When_Nullable()
  {
    var source = @"
#nullable enable
using Everlong.DI;

namespace TestApp;

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
    // Case 2 from the report: generic partial class with [Inject] members, a base class,
    // a type-parameter constraint, and [Inject] on partial properties.
    var source = @"
#nullable enable
using Everlong.DI;

namespace TestApp;

public class ObservableObject { }

public class PageArgs { }

public interface IShellManager { }

public interface ILayer { }

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
    // Case 1 from the report: non-generic partial class with [Inject] members and a base class
    // and [Inject] on partial properties.
    var source = @"
#nullable enable
using Everlong.DI;

namespace TestApp;

public class ObservableObject { }

public interface IShellManager { }

public interface ILayer { }

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

public partial class ClassStore<T> : IMyService where T : class, new()
{
    [Inject] private IMyService _svc = null!;
}

public partial class StructStore<T> : IMyService where T : struct
{
    [Inject] private IMyService _svc = null!;
}

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
  public void Generate_When_Multiple_Partials_And_Plain_Part_Present()
  {
    // Partial parts without [Inject] members are not generation candidates: they neither
    // trigger the syntax predicate nor interfere with the candidate part. Generation is
    // driven by members, never by file path ordering.
    var (generatorDiagnostics, compilationDiagnostics, generated) = RunGeneratorAndCompile(
      ("A.cs", @"
using Everlong.DI;

namespace TestApp;

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
    Assert.Contains("Δinjected_Service", generated);
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

public partial class TestClass
{
    [Inject] public partial IAnotherService Service { get; }
}"));

    Assert.Empty(generatorDiagnostics);
    var errors = compilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
    Assert.True(errors.Count == 0,
      "Members declared in any partial part must be injected. Errors:\n" + string.Join("\n", errors.Select(e => e.ToString())));
    Assert.Contains("Δinjected_Service", generated);
    Assert.Contains("Δinjected_Other", generated);
  }

  [Fact]
  public void Generate_When_Another_Part_Carries_Injectable_Suffixed_Attribute()
  {
    // 病态/误报场景:另一个部分挂了名字以 "Injectable" 结尾的异类属性(例如两个
    // 程序集各有一个同名 InjectableAttribute)。v2 只有 [Inject] 成员 / IAutoInject 会锚定,
    // 该部分既不触发也绝不抑制真正的成员部分,仍只产出一份。
    var (generatorDiagnostics, compilationDiagnostics, generated) = RunGeneratorAndCompile(
      ("A.cs", @"
using Everlong.DI;

namespace TestApp;

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
    Assert.Contains("Δinjected_Service", generated);
  }

  // --------------------------------------------------------------------------------------
  // v2: member-anchored model — [Inject] members are the anchor; IAutoInject / stamped
  // IAutoInject ancestors carry the chain. Memberless intermediate levels are transparent
  // and need no marker. Sealed rationale is covered below.
  // --------------------------------------------------------------------------------------

  [Fact]
  public void Generate_Chain_Through_Unmarked_Memberless_Intermediate()
  {
    // The v2 answer to the memberless-chain bug: the middle level carries NO attribute and
    // NO members — it is transparent. The leaf's override binds the top-most generated
    // Inject THROUGH the intermediate; base.Inject reaches the Shell/Router wiring.
    var (generatorDiagnostics, compilationDiagnostics, generated) = RunGeneratorAndCompile(@"
#nullable enable
using Everlong.DI;

namespace TestApp;

public class PostDetailArgs { }
public interface IShell { }
public interface IRouter { }
public interface IService { }

public partial class RoutableViewModel
{
    [Inject] public partial IShell Shell { get; }
    [Inject] public partial IRouter Router { get; }
}

// No [Inject] members, no IAutoInject — nothing is generated for it.
public partial class RoutableViewModel<TArgs> : RoutableViewModel { }

// Only [Inject] members — member-anchored. Must emit override + base.Inject, no CS0114.
public partial class PostDetailPageModel : RoutableViewModel<PostDetailArgs>
{
    [Inject] public partial IService Service { get; }
}
");

    Assert.Empty(generatorDiagnostics);
    var errors = compilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
    var cs0114 = compilationDiagnostics.Where(d => d.Id == "CS0114").ToList();
    Assert.True(errors.Count == 0, "Errors:\n" + string.Join("\n", errors.Select(e => e.ToString())));
    Assert.True(cs0114.Count == 0, "CS0114 must not fire:\n" + string.Join("\n", cs0114.Select(e => e.ToString())));

    // Exactly two generated partials: the root and the leaf. The transparent middle emits nothing.
    Assert.Equal(2, runResultTreeCount(generated));
    Assert.Contains("public virtual void Inject(IServiceProvider services)", generated); // root
    Assert.Contains("public override void Inject(IServiceProvider services)", generated); // leaf
    Assert.Contains("base.Inject(services);", generated);
  }

  [Fact]
  public void Generate_Chain_Through_AutoInject_Marked_Memberless_Intermediate()
  {
    // An explicitly marked memberless level (source `: IAutoInject`) still gets its own
    // level: a chain-through override (own guard + OnInjected hook), and the leaf chains
    // into it.
    var (generatorDiagnostics, compilationDiagnostics, generated) = RunGeneratorAndCompile(@"
#nullable enable
using Everlong.DI;

namespace TestApp;

public interface IShell { }
public interface IService { }

public partial class RoutableViewModel
{
    [Inject] public partial IShell Shell { get; }
}

public partial class RoutableViewModel<TArgs> : RoutableViewModel, IAutoInject { }

public partial class PostDetailPageModel : RoutableViewModel<int>
{
    [Inject] public partial IService Service { get; }
}
");

    Assert.Empty(generatorDiagnostics);
    var errors = compilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
    var cs0114 = compilationDiagnostics.Where(d => d.Id == "CS0114").ToList();
    Assert.True(errors.Count == 0, "Errors:\n" + string.Join("\n", errors.Select(e => e.ToString())));
    Assert.True(cs0114.Count == 0, "CS0114 must not fire:\n" + string.Join("\n", cs0114.Select(e => e.ToString())));

    Assert.Equal(3, runResultTreeCount(generated));
    // Chain-through body for the memberless marked level: guard, base call, no member wiring.
    Assert.Contains("public override void Inject(IServiceProvider services)", generated);
    Assert.Contains("base.Inject(services);", generated);
  }

  [Fact]
  public void Generate_Virtual_Root_When_Memberless_Implements_IAutoInject()
  {
    // IAutoInject is the interface-form anchor: a memberless class that declares it in
    // source gets a virtual root Inject (guard + OnInjected hook), so its OnInjected partial
    // hook actually fires and its hierarchy is a valid chain target.
    var (generatorDiagnostics, compilationDiagnostics, generated) = RunGeneratorAndCompile(@"
#nullable enable
using Everlong.DI;

namespace TestApp;

public interface IService { }

public partial class ViewModelBase : IAutoInject { }

public partial class Derived : ViewModelBase
{
    [Inject] public partial IService Service { get; }
}
");

    Assert.Empty(generatorDiagnostics);
    var errors = compilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
    Assert.True(errors.Count == 0, "Errors:\n" + string.Join("\n", errors.Select(e => e.ToString())));

    Assert.Contains("public virtual void Inject(IServiceProvider services)", generated);
    Assert.Contains("partial void OnInjected();", generated);
    // Derived must chain into the IAutoInject root (interface is source-visible on the base).
    Assert.Contains("public override void Inject(IServiceProvider services)", generated);
    Assert.Contains("base.Inject(services);", generated);
  }

  [Fact]
  public void Generate_Plain_Inject_When_Sealed_Chain_Start()
  {
    // Sealed chain start: no injectable ancestor. The generated method is `public void
    // Inject` — NOT virtual — because a sealed class cannot be derived, so a virtual method
    // could never be overridden; C# additionally forbids virtual in sealed classes (CS0549).
    var (generatorDiagnostics, compilationDiagnostics, generated) = RunGeneratorAndCompile(@"
using Everlong.DI;

namespace TestApp;

public interface IMyService { }

public sealed partial class FinalService
{
    [Inject] private IMyService _svc = null!;
}
");

    Assert.Empty(generatorDiagnostics);
    var errors = compilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
    Assert.True(errors.Count == 0, "Errors:\n" + string.Join("\n", errors.Select(e => e.ToString())));

    Assert.Contains("public void Inject(IServiceProvider services)", generated);
    Assert.DoesNotContain("public virtual void Inject", generated);
    Assert.DoesNotContain("public override void Inject", generated);
    Assert.Contains("partial class FinalService : IAutoInject", generated);
  }

  [Fact]
  public void Generate_Override_When_Sealed_But_Chained()
  {
    // sealed + existing chain: the generated member is still `override` (overriding is legal
    // in sealed classes — only `virtual` is forbidden there). Virtual-ness dies with the
    // sealed class, which is fine: nothing can derive from it anyway.
    var (generatorDiagnostics, compilationDiagnostics, generated) = RunGeneratorAndCompile(@"
using Everlong.DI;

namespace TestApp;

public interface IShell { }
public interface IService { }

public partial class BaseService
{
    [Inject] private IShell _shell = null!;
}

public sealed partial class FinalService : BaseService
{
    [Inject] private IService _svc = null!;
}
");

    Assert.Empty(generatorDiagnostics);
    var errors = compilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
    var cs0114 = compilationDiagnostics.Where(d => d.Id == "CS0114").ToList();
    Assert.True(errors.Count == 0, "Errors:\n" + string.Join("\n", errors.Select(e => e.ToString())));
    Assert.True(cs0114.Count == 0, "CS0114 must not fire:\n" + string.Join("\n", cs0114.Select(e => e.ToString())));

    Assert.Contains("public override void Inject(IServiceProvider services)", generated);
    Assert.Contains("base.Inject(services);", generated);
  }

  [Fact]
  public void Generate_Empty_Virtual_Root_When_Memberless_AutoInject()
  {
    // v2: a memberless class with no injectable ancestry that declares IAutoInject gets an
    // empty virtual root Inject (guard + OnInjected), making its hook reachable.
    var (generatorDiagnostics, compilationDiagnostics, generated) = RunGeneratorAndCompile(@"
using Everlong.DI;

namespace TestApp;

public partial class HookOnlyBase : IAutoInject { }
");

    Assert.Empty(generatorDiagnostics);
    var errors = compilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
    Assert.True(errors.Count == 0, "Errors:\n" + string.Join("\n", errors.Select(e => e.ToString())));

    Assert.Contains("public virtual void Inject(IServiceProvider services)", generated);
    Assert.Contains("partial class HookOnlyBase : IAutoInject", generated);
    Assert.Contains("OnInjected();", generated);
  }

  [Fact]
  public void Generate_Once_When_Inject_Members_Spread_Across_Parts_Without_Markers()
  {
    // No IAutoInject anywhere: member-anchored. Both parts carry [Inject]
    // members, so both are syntax candidates — the canonical-part dedupe must emit exactly
    // one generated partial, and it must collect members from both parts.
    var (generatorDiagnostics, compilationDiagnostics, generated) = RunGeneratorAndCompile(
      ("A.cs", @"
using Everlong.DI;

namespace TestApp;

public partial class TestClass
{
    [Inject] public partial IMyService Service { get; }
}"),
      ("Services.cs", @"
namespace TestApp;

public interface IMyService {}
public interface IAnotherService {}"),
      ("Z.cs", @"
using Everlong.DI;

namespace TestApp;

public partial class TestClass
{
    [Inject] public partial IAnotherService Other { get; }
}"));

    Assert.Empty(generatorDiagnostics);
    var errors = compilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
    Assert.True(errors.Count == 0,
      "Members declared in any partial part must be injected exactly once. Errors:\n" + string.Join("\n", errors.Select(e => e.ToString())));
    Assert.Equal(1, runResultTreeCount(generated));
    Assert.Contains("Δinjected_Service", generated);
    Assert.Contains("Δinjected_Other", generated);
  }

  [Fact]
  public void Generate_Own_Level_When_Derived_Redeclares_AutoInject()
  {
    // A derived class re-listing IAutoInject in its own base list is legal C# and means
    // "give me my own Inject level" — here a chain-through override with its own guard and
    // OnInjected hook. Without the redeclaration the same memberless class is transparent.
    var (generatorDiagnostics, compilationDiagnostics, generated) = RunGeneratorAndCompile(@"
#nullable enable
using Everlong.DI;

namespace TestApp;

public interface IService { }

public partial class Root : IAutoInject { }                       // memberless anchor root

public partial class Derived : Root, IAutoInject { }              // redundant re-listing → own level

public partial class Leaf : Derived
{
    [Inject] public partial IService Service { get; }
}
");

    Assert.Empty(generatorDiagnostics);
    var errors = compilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
    var cs0114 = compilationDiagnostics.Where(d => d.Id == "CS0114").ToList();
    Assert.True(errors.Count == 0,
      "Errors: " + string.Join(" | ", errors.Select(e => e.ToString())));
    Assert.True(cs0114.Count == 0,
      "CS0114 must not fire: " + string.Join(" | ", cs0114.Select(e => e.ToString())));

    Assert.Equal(3, runResultTreeCount(generated));               // Root, Derived, Leaf
    Assert.Contains("public override void Inject(IServiceProvider services)", generated);
    Assert.Contains("base.Inject(services);", generated);
  }

  [Fact]
  public void Generate_No_Level_When_IInjectable_Declared_Alone_And_Memberless()
  {
    // IInjectable is the resolution CONTRACT, not the generator anchor: a memberless class
    // declaring only : IInjectable is not a target. It must implement Inject() itself or add
    // IAutoInject / [Inject] members — otherwise CS0535.
    var (generatorDiagnostics, compilationDiagnostics, generated) = RunGeneratorAndCompile(@"
using Everlong.DI;

namespace TestApp;

public partial class NotOptedIn : IInjectable { }
");

    Assert.Empty(generatorDiagnostics);
    Assert.Equal(0, runResultTreeCount(generated));
    var cs0535 = compilationDiagnostics.Where(d => d.Id == "CS0535").ToList();
    Assert.True(cs0535.Count > 0,
      "IInjectable without IAutoInject/members must surface CS0535. Diagnostics: "
      + string.Join(" | ", compilationDiagnostics.Select(d => d.ToString())));
  }

  [Fact]
  public void Generate_Partial_Property_When_Nullable()
  {
    // Regression (found by the examples/ dogfood app): a nullable partial property must emit
    // a backing field/accessor that keeps the '?' annotation, otherwise the compiler reports
    // CS9256 (partial member signature mismatch) plus CS8601 noise.
    var (generatorDiagnostics, compilationDiagnostics, generated) = RunGeneratorAndCompile(@"
#nullable enable
using Everlong.DI;

namespace TestApp;

public partial class TestClass
{
    [Inject] public partial IMyService? Service { get; }
    [Inject] public partial IMyService? Other { get; }
}

public interface IMyService {}
");

    Assert.Empty(generatorDiagnostics);
    var errors = compilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
    var warnings = compilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning && d.Id is "CS9256" or "CS8601").ToList();
    Assert.True(errors.Count == 0, "Errors: " + string.Join(" | ", errors.Select(e => e.ToString())));
    Assert.True(warnings.Count == 0, "CS9256/CS8601 must not fire: " + string.Join(" | ", warnings.Select(e => e.ToString())));

    Assert.Contains("private global::TestApp.IMyService? Δinjected_Service = default !", generated);
    Assert.Contains("public partial global::TestApp.IMyService? Service => Δinjected_Service;", generated);
    Assert.Contains("__inject_value_0 = services.GetService<global::TestApp.IMyService>();", generated);
    Assert.Contains("Δinjected_Service = __inject_value_0;", generated);
  }

  private static int runResultTreeCount(string generated)
    => generated.Split(new[] { "#nullable enable" }, System.StringSplitOptions.None).Length - 1;

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
