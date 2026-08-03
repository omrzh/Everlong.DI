using Everlong.DI;
using Microsoft.Extensions.DependencyInjection;

namespace Everlong.DI.Tests;

[Singleton]
public class MySingletonService;

[Transient]
public class MyTransientService;

public interface IMyScopedService;

[Scoped<IMyScopedService>]
public class MyScopedService : IMyScopedService;

[ServiceRegistrar]
public partial class TestServiceRegistrar;

public class ServiceRegistrationTests
{
  [Fact]
  public void TestRegistration()
  {
    IServiceCollection services = new ServiceCollection();

    IServiceRegistrar registrar = new TestServiceRegistrar();

    registrar.RegisterServices(services);

    var provider = services.BuildServiceProvider();

    var singleton1 = provider.GetService<MySingletonService>();
    var singleton2 = provider.GetService<MySingletonService>();
    Assert.NotNull(singleton1);
    Assert.Same(singleton1, singleton2);

    var transient1 = provider.GetService<MyTransientService>();
    var transient2 = provider.GetService<MyTransientService>();
    Assert.NotNull(transient1);
    Assert.NotSame(transient1, transient2);

    using (var scope = provider.CreateScope())
    {
      var scoped1 = scope.ServiceProvider.GetService<IMyScopedService>();
      var scoped2 = scope.ServiceProvider.GetService<IMyScopedService>();
      Assert.NotNull(scoped1);
      Assert.Same(scoped1, scoped2);
      Assert.IsType<MyScopedService>(scoped1);

      using (var scope2 = provider.CreateScope())
      {
        var scoped3 = scope2.ServiceProvider.GetService<IMyScopedService>();
        Assert.NotSame(scoped1, scoped3);
      }
    }
  }
}
