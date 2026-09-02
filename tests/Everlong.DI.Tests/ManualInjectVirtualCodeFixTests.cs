using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Everlong.DI.CodeFixers;
using Everlong.DI.Generators.Analyzers;

namespace Everlong.DI.Tests;

public class ManualInjectVirtualCodeFixTests
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
    public async Task Fix_Should_Add_Virtual()
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

        var fixedCode = @"
using Everlong.DI;

namespace TestNamespace
{
    public partial class ManualBase : IInjectable
    {
        public virtual void Inject(System.IServiceProvider services) { }
    }
}";

        var testCase = new CSharpCodeFixTest<ManualInjectVirtualAnalyzer, ManualInjectVirtualCodeFixProvider, DefaultVerifier>
        {
            TestCode = (test + InjectableSource).ReplaceLineEndings(),
            FixedCode = (fixedCode + InjectableSource).ReplaceLineEndings()
        };

        await testCase.RunAsync();
    }
}
