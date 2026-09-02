using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Everlong.DI.Generators.Analyzers;

namespace Everlong.DI.Tests.Analyzers;

public class ManualInjectVirtualAnalyzerTests
{
    private const string InjectableSource = @"
namespace Everlong.DI
{
    public interface IInjectable
    {
        void Inject(System.IServiceProvider services);
    }
}
";

    [Fact]
    public async Task Report_When_Manual_Inject_Is_Not_Virtual()
    {
        var test = @"
using Everlong.DI;

namespace TestNamespace
{
    public partial class ManualBase : IInjectable
    {
        public void {|DIG0018:Inject|}(System.IServiceProvider services) { }
    }
}";

        await VerifyAnalyzer.VerifyAnalyzerAsync(test + InjectableSource);
    }

    [Fact]
    public async Task No_Report_When_Sealed()
    {
        var test = @"
using Everlong.DI;

namespace TestNamespace
{
    public sealed class ManualBase : IInjectable
    {
        public void Inject(System.IServiceProvider services) { }
    }
}";

        await VerifyAnalyzer.VerifyAnalyzerAsync(test + InjectableSource);
    }

    [Fact]
    public async Task No_Report_When_Virtual()
    {
        var test = @"
using Everlong.DI;

namespace TestNamespace
{
    public partial class ManualBase : IInjectable
    {
        public virtual void Inject(System.IServiceProvider services) { }
    }
}";

        await VerifyAnalyzer.VerifyAnalyzerAsync(test + InjectableSource);
    }

    [Fact]
    public async Task No_Report_When_Abstract()
    {
        var test = @"
using Everlong.DI;

namespace TestNamespace
{
    public abstract partial class ManualBase : IInjectable
    {
        public abstract void Inject(System.IServiceProvider services);
    }
}";

        await VerifyAnalyzer.VerifyAnalyzerAsync(test + InjectableSource);
    }

    internal static class VerifyAnalyzer
    {
        public static Task VerifyAnalyzerAsync(string source)
        {
            var testCase = new CSharpAnalyzerTest<ManualInjectVirtualAnalyzer, DefaultVerifier>
            {
                TestCode = source
            };

            return testCase.RunAsync();
        }
    }
}

public class ManualInjectExplicitAnalyzerTests
{
    private const string InjectableSource = @"
namespace Everlong.DI
{
    public interface IInjectable
    {
        void Inject(System.IServiceProvider services);
    }
}
";

    [Fact]
    public async Task Report_When_Explicit_Implementation_On_NonSealed_Type()
    {
        var test = @"
using Everlong.DI;

namespace TestNamespace
{
    public partial class ManualBase : IInjectable
    {
        void IInjectable.{|DIG0019:Inject|}(System.IServiceProvider services) { }
    }
}";

        await ManualInjectVirtualAnalyzerTests.VerifyAnalyzer.VerifyAnalyzerAsync(test + InjectableSource);
    }

    [Fact]
    public async Task No_Report_When_Explicit_Implementation_On_Sealed_Type()
    {
        var test = @"
using Everlong.DI;

namespace TestNamespace
{
    public sealed class ManualBase : IInjectable
    {
        void IInjectable.Inject(System.IServiceProvider services) { }
    }
}";

        await ManualInjectVirtualAnalyzerTests.VerifyAnalyzer.VerifyAnalyzerAsync(test + InjectableSource);
    }
}
