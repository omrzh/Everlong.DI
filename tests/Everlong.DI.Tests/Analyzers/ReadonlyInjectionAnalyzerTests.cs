using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Everlong.DI.Generators.Analyzers;
using Everlong.DI.Generators.Constants;

namespace Everlong.DI.Tests.Analyzers;

public class ReadonlyInjectionAnalyzerTests
{
    private const string AttributeSource = @"
namespace Everlong.DI
{
    [System.AttributeUsage(System.AttributeTargets.Field | System.AttributeTargets.Property)]
    public class InjectAttribute : System.Attribute {}
}
";

    [Fact]
    public async Task Report_When_Injected_Field_Is_Readonly()
    {
        var test = @"
using Everlong.DI;

namespace TestNamespace
{
    public partial class TestClass
    {
        [Inject]
        private readonly IMyService {|#0:_service|} = null!;
    }

    public interface IMyService {}
}";

        var expected = new DiagnosticResult(Descriptors.ReadonlyInjection)
            .WithLocation(0)
            .WithArguments("_service");

        await VerifyAnalyzerAsync(test + AttributeSource, expected);
    }

    [Fact]
    public async Task No_Report_When_Injected_Field_Is_Not_Readonly()
    {
        var test = @"
using Everlong.DI;

namespace TestNamespace
{
    public partial class TestClass
    {
        [Inject]
        private IMyService _service = null!;
    }

    public interface IMyService {}
}";

        await VerifyAnalyzerAsync(test + AttributeSource);
    }

    private static Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expectedDiagnostics)
    {
        var testCase = new CSharpAnalyzerTest<ReadonlyInjectionAnalyzer, DefaultVerifier>
        {
            TestCode = source
        };

        testCase.ExpectedDiagnostics.AddRange(expectedDiagnostics);
        return testCase.RunAsync();
    }
}
