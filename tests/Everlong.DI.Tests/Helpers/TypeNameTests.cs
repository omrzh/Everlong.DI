using TypeName = Everlong.DI.Generators.Models.TypeName;

namespace Everlong.DI.Tests.Helpers;

public class TypeNameTests
{
  [Fact]
  public void Constructor_SetsFullyQualifiedProperty()
  {
    var typeName = new TypeName("MyNamespace.MyClass");
    Assert.Equal("MyNamespace.MyClass", typeName.FullyQualified);
  }

  [Fact]
  public void ImplicitConversion_FromString_SetsFullyQualifiedProperty()
  {
    TypeName typeName = "MyNamespace.MyClass";
    Assert.Equal("MyNamespace.MyClass", typeName.FullyQualified);
  }

  [Theory]
  [InlineData("MyNamespace.MyClass", "MyNamespace.MyClass")]
  [InlineData("global::MyNamespace.MyClass", "MyNamespace.MyClass")]
  [InlineData("MyClass", "MyClass")]
  [InlineData("global::MyClass", "MyClass")]
  public void FullName_ReturnsNameWithoutGlobalPrefix(string input, string expected)
  {
    var typeName = new TypeName(input);
    Assert.Equal(expected, typeName.FullName);
  }

  [Theory]
  [InlineData("MyNamespace.MyClass", "MyNamespace")]
  [InlineData("global::MyNamespace.MyClass", "MyNamespace")]
  [InlineData("MyNamespace.SubNamespace.MyClass", "MyNamespace.SubNamespace")]
  [InlineData("MyClass", "")]
  [InlineData("global::MyClass", "")]
  [InlineData("global::System.Collections.Generic.List<string>", "System.Collections.Generic")]
  public void Namespace_ReturnsNamespaceOnly(string input, string expected)
  {
    var typeName = new TypeName(input);
    Assert.Equal(expected, typeName.Namespace);
  }

  [Theory]
  [InlineData("MyNamespace.MyClass", "MyClass")]
  [InlineData("global::MyNamespace.MyClass", "MyClass")]
  [InlineData("MyNamespace.SubNamespace.MyClass", "MyClass")]
  [InlineData("MyClass", "MyClass")]
  [InlineData("System.Collections.Generic.List<string>", "List<string>")]
  [InlineData("global::System.Collections.Generic.List<string>", "List<string>")]
  public void ShortName_ReturnsClassNameOnly_PreservesGenerics(string input, string expected)
  {
    var typeName = new TypeName(input);
    Assert.Equal(expected, typeName.ShortName);
  }

  [Theory]
  [InlineData("MyNamespace.MyClass", "MyClass")]
  [InlineData("List<string>", "List_string_")]
  [InlineData("Dictionary<string, int>", "Dictionary_string_ int_")]
  public void SafeName_ReplacesSpecialCharacters(string input, string expected)
  {
    var typeName = new TypeName(input);
    Assert.Equal(expected, typeName.SafeName);
  }
}
