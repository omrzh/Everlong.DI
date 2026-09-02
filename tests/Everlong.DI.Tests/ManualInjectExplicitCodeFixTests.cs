using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Everlong.DI.CodeFixers;
using Everlong.DI.Generators.Analyzers;

namespace Everlong.DI.Tests;

public class ManualInjectExplicitCodeFixTests
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
    public async Task Fix_Should_Convert_To_Implicit_Virtual()
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

        var fixedCode = @"
using Everlong.DI;

namespace TestNamespace
{
    public partial class ManualBase : IInjectable
    {
        public virtual void Inject(System.IServiceProvider services) { }
    }
}";

        var testCase = new CSharpCodeFixTest<ManualInjectVirtualAnalyzer, ManualInjectExplicitCodeFixProvider, DefaultVerifier>
        {
            TestCode = (test + InjectableSource).ReplaceLineEndings(),
            FixedCode = (fixedCode + InjectableSource).ReplaceLineEndings()
        };

        await testCase.RunAsync();
    }
}
