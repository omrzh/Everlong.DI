using Everlong.DI.Generators.DI;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Everlong.DI.Tests.DI;

/// <summary>
///   Generator-shape and diagnostic tests for [AlsoAs] shared-instance forwarding.
/// </summary>
public class AlsoAsRegistrationTests
{
  // --- Forwarding shapes -------------------------------------------------

  [Fact]
  public void ShouldForwardFromSelfMain_WhenSelfMainWithAlsoAs()
  {
    var source = @"
using Everlong.DI;
public interface IFoo;
[Singleton]
[AlsoAs<IFoo>]
public class Foo : IFoo;
[ServiceRegistrar]
public partial class Table {}
";
    var generated = GetGeneratedSources(source);
    Assert.Contains(
      "services.TryAdd(new ServiceDescriptor(typeof(global::Foo), typeof(global::Foo), ServiceLifetime.Singleton));",
      generated);
    Assert.Contains(
      "services.TryAdd(new ServiceDescriptor(typeof(global::IFoo), sp => sp.GetRequiredService<global::Foo>(), ServiceLifetime.Singleton));",
      generated);
  }

  [Fact]
  public void ShouldForwardFromGenericMain_WhenGenericMainWithAlsoAs()
  {
    var source = @"
using Everlong.DI;
public interface IFoo;
public interface IBar;
[Singleton<IFoo>]
[AlsoAs<IBar>]
public class Foo : IFoo, IBar;
[ServiceRegistrar]
public partial class Table {}
";
    var generated = GetGeneratedSources(source);
    Assert.Contains(
      "services.TryAdd(new ServiceDescriptor(typeof(global::IFoo), typeof(global::Foo), ServiceLifetime.Singleton));",
      generated);
    // Defensive cast against TryAdd claim races: resolve the main service, verify it is the AlsoAs type.
    Assert.Contains("return s is global::IBar b ? b : throw", generated);
  }

  [Fact]
  public void ShouldUseKeyedResolution_WhenMainHasKey()
  {
    var source = @"
using Everlong.DI;
public interface IFoo;
public interface IBar;
[Singleton<IFoo>(""k1"")]
[AlsoAs<IBar>]
public class Foo : IFoo, IBar;
[ServiceRegistrar]
public partial class Table {}
";
    var generated = GetGeneratedSources(source);
    Assert.Contains(
      "services.TryAdd(ServiceDescriptor.KeyedSingleton(typeof(global::IFoo), \"k1\", typeof(global::Foo)));",
      generated);
    Assert.Contains(
      "return s is global::IBar b ? b : throw",
      generated);
  }

  [Fact]
  public void ShouldRegisterKeyedForward_WhenAlsoAsHasKey()
  {
    var source = @"
using Everlong.DI;
public interface IFoo;
public interface IBar;
[Singleton]
[AlsoAs<IBar>(""k2"")]
public class Foo : IFoo, IBar;
[ServiceRegistrar]
public partial class Table {}
";
    var generated = GetGeneratedSources(source);
    Assert.Contains(
      "services.TryAdd(new ServiceDescriptor(typeof(global::IBar), \"k2\", sp => sp.GetRequiredService<global::Foo>(), ServiceLifetime.Singleton));",
      generated);
  }

  [Fact]
  public void ShouldAdd_WhenAlsoAsEnumerable()
  {
    var source = @"
using Everlong.DI;
public interface IFoo;
[Singleton]
[AlsoAs<IFoo>(enumerable: true)]
public class Foo : IFoo;
[ServiceRegistrar]
public partial class Table {}
";
    var generated = GetGeneratedSources(source);
    Assert.Contains(
      "services.Add(new ServiceDescriptor(typeof(global::IFoo), sp => sp.GetRequiredService<global::Foo>(), ServiceLifetime.Singleton));",
      generated);
  }

  [Fact]
  public void ShouldAddKeyedForward_WhenAlsoAsKeyedAndEnumerable()
  {
    var source = @"
using Everlong.DI;
public interface IFoo;
[Singleton]
[AlsoAs<IFoo>(""k2"", enumerable: true)]
public class Foo : IFoo;
[ServiceRegistrar]
public partial class Table {}
";
    var generated = GetGeneratedSources(source);
    Assert.Contains(
      "services.Add(new ServiceDescriptor(typeof(global::IFoo), \"k2\", sp => sp.GetRequiredService<global::Foo>(), ServiceLifetime.Singleton));",
      generated);
  }

  [Fact]
  public void ShouldForwardFromGenericScopedMain()
  {
    var source = @"
using Everlong.DI;
public interface IFoo;
public interface IBar;
[Scoped<IFoo>]
[AlsoAs<IBar>]
public class Foo : IFoo, IBar;
[ServiceRegistrar]
public partial class Table {}
";
    var generated = GetGeneratedSources(source);
    Assert.Contains("ServiceLifetime.Scoped));", generated);
    Assert.Contains("return s is global::IBar b ? b : throw", generated);
  }

  [Fact]
  public void ShouldForwardScoped_WhenScopedMainWithAlsoAs()
  {
    var source = @"
using Everlong.DI;
public interface IFoo;
[Scoped]
[AlsoAs<IFoo>]
public class Foo : IFoo;
[ServiceRegistrar]
public partial class Table {}
";
    var generated = GetGeneratedSources(source);
    Assert.Contains("ServiceLifetime.Scoped));", generated);
  }

  [Fact]
  public void ShouldEmitOneForwardPerAlsoAs()
  {
    var source = @"
using Everlong.DI;
public interface IFoo;
public interface IBar;
public interface IBaz;
[Singleton]
[AlsoAs<IFoo>]
[AlsoAs<IBar>]
[AlsoAs<IBaz>]
public class Foo : IFoo, IBar, IBaz;
[ServiceRegistrar]
public partial class Table {}
";
    var generated = GetGeneratedSources(source);
    Assert.Equal(3, CountOccurrences(generated, "sp.GetRequiredService<global::Foo>()"));
  }

  [Fact]
  public void ShouldDeduplicate_WhenAlsoAsRepeated()
  {
    var source = @"
using Everlong.DI;
public interface IFoo;
[Singleton]
[AlsoAs<IFoo>]
[AlsoAs<IFoo>]
public class Foo : IFoo;
[ServiceRegistrar]
public partial class Table {}
";
    var generated = GetGeneratedSources(source);
    Assert.Equal(1, CountOccurrences(generated, "sp.GetRequiredService<global::Foo>()"));
  }

  // --- Diagnostics -------------------------------------------------------

  [Fact]
  public void ShouldReportError_WhenAlsoAsWithoutMain()
  {
    var source = @"
using Everlong.DI;
public interface IFoo;
[AlsoAs<IFoo>]
public class Foo : IFoo;
[ServiceRegistrar]
public partial class Table {}
";
    var diagnostics = GetDiagnostics(source);
    Assert.Contains(diagnostics, d => d.Id == "DIG0011");
  }

  [Fact]
  public void ShouldReportError_WhenMultipleTransientMainsWithAlsoAs()
  {
    // Two transient mains are already illegal for AlsoAs; the transient
    // violation (DIG0012) must win over the ambiguity check (DIG0013).
    var source = @"
using Everlong.DI;
public interface IFoo;
public interface IBar;
public interface IBaz;
[Transient<IFoo>]
[Transient<IBar>]
[AlsoAs<IBaz>]
public class Foo : IFoo, IBar, IBaz;
[ServiceRegistrar]
public partial class Table {}
";
    var diagnostics = GetDiagnostics(source);
    Assert.Contains(diagnostics, d => d.Id == "DIG0012");
    Assert.DoesNotContain(diagnostics, d => d.Id == "DIG0013");
  }

  [Fact]
  public void ShouldReportError_WhenSelfRegistrationWithGenericScoped()
  {
    // Cross-lifetime: self [Singleton] + generic [Scoped<T>] are forbidden
    // even though the service types differ.
    var source = @"
using Everlong.DI;
public interface IBar;
[Singleton]
[Scoped<IBar>]
public class Foo : IBar;
[ServiceRegistrar]
public partial class Table {}
";
    var diagnostics = GetDiagnostics(source);
    Assert.Contains(diagnostics, d => d.Id == "DIG0016");
  }

  [Fact]
  public void ShouldReportError_WhenAlsoAsOnTransient()
  {
    var source = @"
using Everlong.DI;
public interface IFoo;
[Transient]
[AlsoAs<IFoo>]
public class Foo : IFoo;
[ServiceRegistrar]
public partial class Table {}
";
    var diagnostics = GetDiagnostics(source);
    Assert.Contains(diagnostics, d => d.Id == "DIG0012");
  }

  [Fact]
  public void ShouldReportError_WhenAlsoAsOnTransientGeneric()
  {
    var source = @"
using Everlong.DI;
public interface IFoo;
public interface IBar;
[Transient<IFoo>]
[AlsoAs<IBar>]
public class Foo : IFoo, IBar;
[ServiceRegistrar]
public partial class Table {}
";
    var diagnostics = GetDiagnostics(source);
    Assert.Contains(diagnostics, d => d.Id == "DIG0012");
  }

  [Fact]
  public void ShouldReportError_WhenMultipleGenericMainsWithAlsoAs()
  {
    var source = @"
using Everlong.DI;
public interface IFoo;
public interface IBar;
public interface IBaz;
[Singleton<IFoo>]
[Singleton<IBar>]
[AlsoAs<IBaz>]
public class Foo : IFoo, IBar, IBaz;
[ServiceRegistrar]
public partial class Table {}
";
    var diagnostics = GetDiagnostics(source);
    Assert.Contains(diagnostics, d => d.Id == "DIG0013");
  }

  [Fact]
  public void ShouldReportError_WhenEnumerableMainWithAlsoAs()
  {
    var source = @"
using Everlong.DI;
public interface IFoo;
public interface IBar;
[Singleton<IFoo>(enumerable: true)]
[AlsoAs<IBar>]
public class Foo : IFoo, IBar;
[ServiceRegistrar]
public partial class Table {}
";
    var diagnostics = GetDiagnostics(source);
    Assert.Contains(diagnostics, d => d.Id == "DIG0014");
  }

  [Fact]
  public void ShouldReportError_WhenSelfAndGenericMainInSameLifetime()
  {
    var source = @"
using Everlong.DI;
public interface IFoo;
[Singleton]
[Singleton<IFoo>]
public class Foo : IFoo;
[ServiceRegistrar]
public partial class Table {}
";
    var diagnostics = GetDiagnostics(source);
    Assert.Contains(diagnostics, d => d.Id == "DIG0015");
  }

  [Fact]
  public void ShouldReportError_WhenTransientSelfAndGenericInSameLifetime()
  {
    var source = @"
using Everlong.DI;
public interface IFoo;
[Transient]
[Transient<IFoo>]
public class Foo : IFoo;
[ServiceRegistrar]
public partial class Table {}
";
    var diagnostics = GetDiagnostics(source);
    Assert.Contains(diagnostics, d => d.Id == "DIG0015");
  }

  [Fact]
  public void ShouldReportError_WhenCrossLifetimeSelfRegistrations()
  {
    var source = @"
using Everlong.DI;
[Singleton]
[Scoped]
public class Foo;
[ServiceRegistrar]
public partial class Table {}
";
    var diagnostics = GetDiagnostics(source);
    Assert.Contains(diagnostics, d => d.Id == "DIG0016");
  }

  [Fact]
  public void ShouldReportError_WhenCrossLifetimeGenericRegistrations()
  {
    var source = @"
using Everlong.DI;
public interface IFoo;
public interface IBar;
[Singleton<IFoo>]
[Scoped<IBar>]
public class Foo : IFoo, IBar;
[ServiceRegistrar]
public partial class Table {}
";
    var diagnostics = GetDiagnostics(source);
    Assert.Contains(diagnostics, d => d.Id == "DIG0016");
  }

  [Fact]
  public void ShouldReportError_WhenAlsoAsTypeNotImplemented()
  {
    var source = @"
using Everlong.DI;
public interface IFoo;
public interface IBar;
[Singleton]
[AlsoAs<IBar>]
public class Foo : IFoo;
[ServiceRegistrar]
public partial class Table {}
";
    var diagnostics = GetDiagnostics(source);
    Assert.Contains(diagnostics, d => d.Id == "DIG0017");
  }

  [Fact]
  public void ShouldReportError_WhenAlsoAsTypeIsNotInterface()
  {
    var source = @"
using Everlong.DI;
public interface IFoo;
public class Bar;
[Singleton]
[AlsoAs<Bar>]
public class Foo : IFoo;
[ServiceRegistrar]
public partial class Table {}
";
    var diagnostics = GetDiagnostics(source);
    Assert.Contains(diagnostics, d => d.Id == "DIG0017");
  }

  // --- Unlocked combinations (no diagnostics) ----------------------------

  [Fact]
  public void ShouldAllowDuplicateGenericRegistrations()
  {
    var source = @"
using Everlong.DI;
public interface IFoo;
[Singleton<IFoo>]
[Singleton<IFoo>]
public class Foo : IFoo;
[ServiceRegistrar]
public partial class Table {}
";
    var diagnostics = GetDiagnostics(source);
    Assert.DoesNotContain(diagnostics, d => d.Id.StartsWith("DIG"));
  }

  [Fact]
  public void ShouldAllowKeyedAndUnkeyedSameServiceType()
  {
    var source = @"
using Everlong.DI;
public interface IFoo;
[Singleton<IFoo>]
[Singleton<IFoo>(""k"")]
public class Foo : IFoo;
[ServiceRegistrar]
public partial class Table {}
";
    var diagnostics = GetDiagnostics(source);
    Assert.DoesNotContain(diagnostics, d => d.Id.StartsWith("DIG"));
  }

  [Fact]
  public void ShouldAllowMultipleGenericMains_WithoutAlsoAs()
  {
    var source = @"
using Everlong.DI;
public interface IFoo;
public interface IBar;
[Singleton<IFoo>]
[Singleton<IBar>]
public class Foo : IFoo, IBar;
[ServiceRegistrar]
public partial class Table {}
";
    var diagnostics = GetDiagnostics(source);
    Assert.DoesNotContain(diagnostics, d => d.Id.StartsWith("DIG"));
  }

  [Fact]
  public void ShouldAllowAlsoAsSameTypeAsMain()
  {
    // Redundant but harmless: TryAdd drops the forward. Not a diagnostic.
    var source = @"
using Everlong.DI;
public interface IFoo;
[Singleton<IFoo>]
[AlsoAs<IFoo>]
public class Foo : IFoo;
[ServiceRegistrar]
public partial class Table {}
";
    var diagnostics = GetDiagnostics(source);
    Assert.DoesNotContain(diagnostics, d => d.Id.StartsWith("DIG"));
  }

  // --- Helpers -----------------------------------------------------------

  private static int CountOccurrences(string text, string fragment)
  {
    int count = 0;
    int index = 0;
    while ((index = text.IndexOf(fragment, index, StringComparison.Ordinal)) >= 0)
    {
      count++;
      index += fragment.Length;
    }
    return count;
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
