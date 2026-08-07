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

public interface IKeyedService;
public interface IOtherKeyedService;

[Singleton<IKeyedService>("eu")]
public class EuKeyedService : IKeyedService;

[Singleton<IKeyedService>("us")]
public class UsKeyedService : IKeyedService;

[Transient<IKeyedService>("t")]
public class TransientKeyedService : IKeyedService;

[Scoped<IOtherKeyedService>("s")]
public class ScopedKeyedService : IOtherKeyedService;

[Singleton("self")]
public class SelfKeyedService;

public enum ServiceRegion { Eu = 1 }

public interface IRegionKeyedService;

[Singleton<IRegionKeyedService>(ServiceRegion.Eu)]
public class EuRegionKeyedService : IRegionKeyedService;

public interface IMultiKeyedService;

[Singleton<IMultiKeyedService>("multi", enumerable: true)]
public class MultiKeyedServiceA : IMultiKeyedService;

[Singleton<IMultiKeyedService>("multi", enumerable: true)]
public class MultiKeyedServiceB : IMultiKeyedService;

public interface ISharedSelf;
public interface ISharedGenericMain;
public interface ISharedGenericSide;
public interface ISharedKeyedSide;
public interface ISharedEnumerable;
public interface IScopedSharedView;

[Scoped]
[AlsoAs<IScopedSharedView>]
public class ScopedSharedService : IScopedSharedView;

[Singleton]
[AlsoAs<ISharedSelf>]
[AlsoAs<ISharedEnumerable>(enumerable: true)]
public class SharedService : ISharedSelf, ISharedEnumerable;

[Singleton<ISharedGenericMain>]
[AlsoAs<ISharedGenericSide>]
public class SharedGenericService : ISharedGenericMain, ISharedGenericSide;

[Singleton<ISharedGenericMain>("sgk")]
[AlsoAs<ISharedKeyedSide>]
public class SharedKeyedService : ISharedGenericMain, ISharedKeyedSide;

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

  [Fact]
  public void TestKeyedRegistration()
  {
    IServiceCollection services = new ServiceCollection();

    IServiceRegistrar registrar = new TestServiceRegistrar();

    registrar.RegisterServices(services);

    var provider = services.BuildServiceProvider();

    var eu1 = provider.GetRequiredKeyedService<IKeyedService>("eu");
    var eu2 = provider.GetRequiredKeyedService<IKeyedService>("eu");
    var us = provider.GetRequiredKeyedService<IKeyedService>("us");
    Assert.IsType<EuKeyedService>(eu1);
    Assert.Same(eu1, eu2);
    Assert.IsType<UsKeyedService>(us);
    Assert.NotSame(eu1, us);

    // Keyed registrations must not leak into the unkeyed space.
    Assert.Null(provider.GetService<IKeyedService>());

    // Unknown key must throw.
    Assert.Throws<InvalidOperationException>(
      () => provider.GetRequiredKeyedService<IKeyedService>("missing"));

    // Transient keyed: a new instance per resolution.
    var t1 = provider.GetRequiredKeyedService<IKeyedService>("t");
    var t2 = provider.GetRequiredKeyedService<IKeyedService>("t");
    Assert.NotSame(t1, t2);

    // Scoped keyed: same within a scope, different across scopes.
    using (var scope = provider.CreateScope())
    {
      var s1 = scope.ServiceProvider.GetRequiredKeyedService<IOtherKeyedService>("s");
      var s2 = scope.ServiceProvider.GetRequiredKeyedService<IOtherKeyedService>("s");
      Assert.Same(s1, s2);

      using (var scope2 = provider.CreateScope())
      {
        var s3 = scope2.ServiceProvider.GetRequiredKeyedService<IOtherKeyedService>("s");
        Assert.NotSame(s1, s3);
      }
    }

    // Keyed self-registration.
    var self1 = provider.GetRequiredKeyedService<SelfKeyedService>("self");
    var self2 = provider.GetRequiredKeyedService<SelfKeyedService>("self");
    Assert.Same(self1, self2);

    // Enum key.
    var region = provider.GetRequiredKeyedService<IRegionKeyedService>(ServiceRegion.Eu);
    Assert.IsType<EuRegionKeyedService>(region);

    // Keyed enumerable.
    var multi = provider.GetKeyedServices<IMultiKeyedService>("multi");
    Assert.Equal(2, multi.Count());
    Assert.All(multi, service => Assert.NotNull(service));
  }

  [Fact]
  public void TestAlsoAsSharedInstance()
  {
    IServiceCollection services = new ServiceCollection();

    IServiceRegistrar registrar = new TestServiceRegistrar();

    registrar.RegisterServices(services);

    var provider = services.BuildServiceProvider();

    var concrete = provider.GetRequiredService<SharedService>();
    var selfView = provider.GetRequiredService<ISharedSelf>();
    Assert.Same(concrete, selfView);

    var enumerableViews = provider.GetServices<ISharedEnumerable>();
    var single = Assert.Single(enumerableViews);
    Assert.Same(concrete, single);

    // Generic main + AlsoAs share one instance.
    var genericMain = provider.GetRequiredService<ISharedGenericMain>();
    var genericSide = provider.GetRequiredService<ISharedGenericSide>();
    Assert.Same(genericMain, genericSide);

    // Keyed main + unkeyed AlsoAs share one instance.
    var keyedMain = provider.GetRequiredKeyedService<ISharedGenericMain>("sgk");
    var keyedSide = provider.GetRequiredService<ISharedKeyedSide>();
    Assert.Same(keyedMain, keyedSide);
    Assert.IsType<SharedKeyedService>(keyedSide);
  }

  [Fact]
  public void TestAlsoAsScopedInstance()
  {
    IServiceCollection services = new ServiceCollection();

    IServiceRegistrar registrar = new TestServiceRegistrar();

    registrar.RegisterServices(services);

    var provider = services.BuildServiceProvider();

    using (var scope = provider.CreateScope())
    {
      var scoped = scope.ServiceProvider.GetRequiredService<ScopedSharedService>();
      var view = scope.ServiceProvider.GetRequiredService<IScopedSharedView>();
      Assert.Same(scoped, view);

      using (var scope2 = provider.CreateScope())
      {
        var scoped2 = scope2.ServiceProvider.GetRequiredService<ScopedSharedService>();
        Assert.NotSame(scoped, scoped2);
      }
    }
  }
}
