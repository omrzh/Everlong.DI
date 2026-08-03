using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Everlong.DI.Generators.Analyzers;

namespace Everlong.DI.Tests.Analyzers;

public class PropertyInjectionAnalyzerTests
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
    public async Task Report_When_Injected_Property_Is_Static()
    {
        var test = @"
using Everlong.DI;

namespace TestNamespace
{
    [Injectable]
    public partial class TestClass
    {
        [Inject]
        private static int {|DIG0005:MyProp|} { get; set; }
    }
}";

        await VerifyAnalyzer.VerifyAnalyzerAsync(test + AttributeSource);
    }

    [Fact]
    public async Task Report_When_Injected_Field_Is_Static()
    {
        var test = @"
using Everlong.DI;

namespace TestNamespace
{
    [Injectable]
    public partial class TestClass
    {
        [Inject]
        private static int {|DIG0005:_value|};
    }
}";

        await VerifyAnalyzer.VerifyAnalyzerAsync(test + AttributeSource);
    }

    [Fact]
    public async Task Report_FieldSuggestion_When_Injected_Field_Is_Instance()
    {
        var test = @"
using Everlong.DI;

namespace TestNamespace
{
    [Injectable]
    public partial class TestClass
    {
        [Inject]
        private int {|DIG0010:_value|};
    }
}";

        await VerifyAnalyzer.VerifyAnalyzerAsync(test + AttributeSource);
    }

    [Fact]
    public async Task Report_When_Injected_Member_Type_Misses_Injectable()
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

        await VerifyAnalyzer.VerifyAnalyzerAsync(test + AttributeSource);
    }

    [Fact]
    public async Task No_Report_When_Injected_Member_Type_Has_Injectable()
    {
        var test = @"
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

        await VerifyAnalyzer.VerifyAnalyzerAsync(test + AttributeSource);
    }

    private static class VerifyAnalyzer
    {
        public static Task VerifyAnalyzerAsync(string source)
        {
            var testCase = new CSharpAnalyzerTest<PropertyInjectionAnalyzer, DefaultVerifier>
            {
                TestCode = source
            };

            return testCase.RunAsync();
        }
    }
}
