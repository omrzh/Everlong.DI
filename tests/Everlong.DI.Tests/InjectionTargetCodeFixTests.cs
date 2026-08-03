using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Everlong.DI.CodeFixers;
using Everlong.DI.Generators.Analyzers;

namespace Everlong.DI.Tests;

public class InjectableCodeFixTests
{
    private const string AttributeSource = @"
namespace Everlong.DI
{
    [System.AttributeUsage(System.AttributeTargets.Field | System.AttributeTargets.Property)]
    public class InjectAttribute : System.Attribute {}

    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class InjectableAttribute : System.Attribute {}
}
";

    [Fact]
    public async Task Fix_Should_Add_Injectable_And_Partial()
    {
        var test = @"
using Everlong.DI;

namespace TestNamespace
{
    public partial class {|DIG0009:TestClass|}
    {
        [Inject]
        private int Value { get; set; }
    }
}";

        var fixedCode = @"
using Everlong.DI;

namespace TestNamespace
{
    [Injectable]
    public partial class TestClass
    {
        [Inject]
        private int Value { get; set; }
    }
}";

        var testCase = new CSharpCodeFixTest<PropertyInjectionAnalyzer, InjectableCodeFixProvider, DefaultVerifier>
        {
            TestCode = (test + AttributeSource).ReplaceLineEndings(),
            FixedCode = (fixedCode + AttributeSource).ReplaceLineEndings()
        };

        await testCase.RunAsync();
    }
}
