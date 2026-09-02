using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Everlong.DI.Generators.Analyzers;
using Everlong.DI.CodeFixers;

namespace Everlong.DI.Tests;

public class PropertyInjectionCodeFixTests
{
    private const string AttributeSource = @"
namespace Everlong.DI
{
    [System.AttributeUsage(System.AttributeTargets.Field | System.AttributeTargets.Property)]
    public class InjectAttribute : System.Attribute {}
}
";

    [Fact]
    public async Task Fix_FieldInjection_ShouldConvertDeclarationAndReferences()
    {
        var test = @"
using Everlong.DI;

namespace TestNamespace
{
    public partial class TestClass
    {
        [Inject]
        private IMyService {|DIG0010:_service|};

        public IMyService Read() => _service;
        public IMyService ReadWithThis() => this._service;
        public string Name() => nameof(_service);
    }

    public interface IMyService {}
}";

        var fixedCode = @"
using Everlong.DI;

namespace TestNamespace
{
    public partial class TestClass
    {
        [Inject]
        private partial IMyService Service { get; }

        public IMyService Read() => Service;
        public IMyService ReadWithThis() => this.Service;
        public string Name() => nameof(Service);
    }

    public interface IMyService {}
}";

        var testCase = new CSharpCodeFixTest<PropertyInjectionAnalyzer, PropertyInjectionCodeFixProvider, DefaultVerifier>
        {
            CompilerDiagnostics = CompilerDiagnostics.None,
            TestCode = (test + AttributeSource).ReplaceLineEndings("\n"),
            FixedCode = (fixedCode + AttributeSource).ReplaceLineEndings("\n")
        };

        await testCase.RunAsync();
    }
}
