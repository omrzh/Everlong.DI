using Microsoft.Extensions.DependencyInjection;

namespace Everlong.DI.Tests.DI;

/// <summary>
///   Runtime tests for <see cref="IScopeMarker" />, <c>AddScopeMarker</c> and <c>IsScoped</c>.
/// </summary>
public class ScopeMarkerTests
{
  // --- IsScoped: root ----------------------------------------------------

  [Fact]
  public void IsScoped_ReturnsFalse_ForRootProvider_WithDefaultOptions()
  {
    var root = BuildProvider(marker: true);
    Assert.False(root.IsScoped());
  }

  [Fact]
  public void IsScoped_ReturnsFalse_ForRootProvider_UnderValidateScopes()
  {
    var services = new ServiceCollection();
    services.AddScopeMarker();
    var root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    Assert.False(root.IsScoped());
  }

  // --- IsScoped: scopes --------------------------------------------------

  [Fact]
  public void IsScoped_ReturnsTrue_ForChildScope()
  {
    var root = BuildProvider(marker: true);
    using var scope = root.CreateScope();
    Assert.True(scope.ServiceProvider.IsScoped());
  }

  [Fact]
  public void IsScoped_ReturnsTrue_ForNestedScope()
  {
    var root = BuildProvider(marker: true);
    using var outer = root.CreateScope();
    using var inner = outer.ServiceProvider.CreateScope();
    Assert.True(inner.ServiceProvider.IsScoped());
  }

  // --- IsScoped: marker not registered -----------------------------------

  [Fact]
  public void IsScoped_ReturnsFalse_WhenMarkerNotRegistered()
  {
    var root = BuildProvider(marker: false);
    Assert.False(root.IsScoped());
    using var scope = root.CreateScope();
    Assert.False(scope.ServiceProvider.IsScoped());
  }

  // --- Marker resolution semantics ---------------------------------------

  [Fact]
  public void ResolveMarker_FromChildScope_IsNotRootScope()
  {
    var root = BuildProvider(marker: true);
    using var scope = root.CreateScope();
    var marker = scope.ServiceProvider.GetRequiredService<IScopeMarker>();
    Assert.False(marker.IsRootScope);
  }

  [Fact]
  public void ResolveMarker_FromRoot_IsRootScope()
  {
    var root = BuildProvider(marker: true);
    var marker = root.GetRequiredService<IScopeMarker>();
    Assert.True(marker.IsRootScope);
  }

  [Fact]
  public void ResolveMarker_IsSharedWithinScope_AndIsolatedAcrossScopes()
  {
    var root = BuildProvider(marker: true);
    using var scope = root.CreateScope();
    Assert.Same(
      scope.ServiceProvider.GetRequiredService<IScopeMarker>(),
      scope.ServiceProvider.GetRequiredService<IScopeMarker>());
    using var other = root.CreateScope();
    Assert.NotSame(
      scope.ServiceProvider.GetRequiredService<IScopeMarker>(),
      other.ServiceProvider.GetRequiredService<IScopeMarker>());
  }

  [Fact]
  public void GetService_ReturnsNull_WhenMarkerNotRegistered()
  {
    var root = BuildProvider(marker: false);
    Assert.Null(root.GetService<IScopeMarker>());
  }

  // --- API shape ---------------------------------------------------------

  [Fact]
  public void AddScopeMarker_ReturnsSameCollection_ForChaining()
  {
    var services = new ServiceCollection();
    Assert.Same(services, services.AddScopeMarker());
  }

  [Fact]
  public void IsScoped_Throws_ForNullProvider()
  {
    IServiceProvider? provider = null;
    Assert.Throws<ArgumentNullException>(() => provider!.IsScoped());
  }

  // --- Helpers -----------------------------------------------------------

  private static IServiceProvider BuildProvider(bool marker)
  {
    var services = new ServiceCollection();
    if (marker)
    {
      services.AddScopeMarker();
    }
    return services.BuildServiceProvider();
  }
}
