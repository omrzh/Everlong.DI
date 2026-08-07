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

  [Fact]
  public void ShouldGenerateKeyedRegistration_WhenKeyProvided()
  {
    var source = @"
using Everlong.DI;
public interface IFoo;
[Singleton<IFoo>(""tenant:eu"")]
public class Foo : IFoo;
[ServiceRegistrar]
public partial class Table {}
";
    var generated = GetGeneratedSources(source);
    Assert.Contains("ServiceRegistrarHelper.VerifyImplementation<global::IFoo, global::Foo>();", generated);
    Assert.Contains(
      "services.TryAdd(ServiceDescriptor.KeyedSingleton(typeof(global::IFoo), \"tenant:eu\", typeof(global::Foo)));",
      generated);
  }

  [Fact]
  public void ShouldGenerateKeyedEnumerableRegistration_WhenEnumerableAndKeyProvided()
  {
    var source = @"
using Everlong.DI;
public interface IFoo;
[Singleton<IFoo>(""k"", enumerable: true)]
public class Foo : IFoo;
[ServiceRegistrar]
public partial class Table {}
";
    var generated = GetGeneratedSources(source);
    Assert.Contains(
      "services.Add(ServiceDescriptor.KeyedSingleton(typeof(global::IFoo), \"k\", typeof(global::Foo)));",
      generated);
  }

  [Fact]
  public void ShouldGenerateKeyedSelfRegistration_WhenKeyOnNonGenericAttribute()
  {
    var source = @"
using Everlong.DI;
[Singleton(""k"")]
public class Foo;
[ServiceRegistrar]
public partial class Table {}
";
    var generated = GetGeneratedSources(source);
    Assert.Contains("ServiceRegistrarHelper.EnsureConcreteType<global::Foo>();", generated);
    Assert.Contains(
      "services.TryAdd(ServiceDescriptor.KeyedSingleton(typeof(global::Foo), \"k\", typeof(global::Foo)));",
      generated);
  }

  [Fact]
  public void ShouldGenerateTypeKey_WhenKeyIsType()
  {
    var source = @"
using Everlong.DI;
public interface IFoo;
public class KeyMarker;
[Singleton<IFoo>(typeof(KeyMarker))]
public class Foo : IFoo;
[ServiceRegistrar]
public partial class Table {}
";
    var generated = GetGeneratedSources(source);
    Assert.Contains(
      "ServiceDescriptor.KeyedSingleton(typeof(global::IFoo), typeof(global::KeyMarker), typeof(global::Foo))",
      generated);
  }

  [Fact]
  public void ShouldGenerateEnumKey_WhenKeyIsEnum()
  {
    var source = @"
using Everlong.DI;
public interface IFoo;
public enum Tenant { Eu = 2 }
[Singleton<IFoo>(Tenant.Eu)]
public class Foo : IFoo;
[ServiceRegistrar]
public partial class Table {}
";
    var generated = GetGeneratedSources(source);
    Assert.Contains(
      "ServiceDescriptor.KeyedSingleton(typeof(global::IFoo), (global::Tenant)2, typeof(global::Foo))",
      generated);
  }

  [Fact]
  public void ShouldGenerateIntKey_WhenKeyIsInt()
  {
    var source = @"
using Everlong.DI;
public interface IFoo;
[Singleton<IFoo>(42)]
public class Foo : IFoo;
[ServiceRegistrar]
public partial class Table {}
";
    var generated = GetGeneratedSources(source);
    Assert.Contains(
      "ServiceDescriptor.KeyedSingleton(typeof(global::IFoo), 42, typeof(global::Foo))",
      generated);
  }

  [Fact]
  public void ShouldKeepPositionalBoolAsEnumerable_WhenTrueProvided()
  {
    var source = @"
using Everlong.DI;
public interface IFoo;
[Singleton<IFoo>(true)]
public class Foo : IFoo;
[ServiceRegistrar]
public partial class Table {}
";
    var generated = GetGeneratedSources(source);
    Assert.DoesNotContain("KeyedSingleton", generated);
    Assert.Contains(
      "services.Add(new ServiceDescriptor(typeof(global::IFoo), typeof(global::Foo), ServiceLifetime.Singleton));",
      generated);
  }

  [Fact]
  public void ShouldNotGenerateKeyedDescriptor_WhenNoKey()
  {
    var source = @"
using Everlong.DI;
public interface IFoo;
[Singleton<IFoo>]
public class Foo : IFoo;
[ServiceRegistrar]
public partial class Table {}
";
    var generated = GetGeneratedSources(source);
    Assert.DoesNotContain("KeyedSingleton", generated);
    Assert.Contains(
      "services.TryAdd(new ServiceDescriptor(typeof(global::IFoo), typeof(global::Foo), ServiceLifetime.Singleton));",
      generated);
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
