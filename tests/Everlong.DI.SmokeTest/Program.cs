using Everlong.DI;
using Microsoft.Extensions.DependencyInjection;

// ── Main ───────────────────────────────────────────────────────────
var services = new ServiceCollection();

// Apply registrar
services.AddServices(new MyRegistrar());

// Manually register the consumer
services.AddSingleton<Consumer>();
services.AddSingleton<PropConsumer>();

var sp = services.BuildServiceProvider();

// Resolve & inject
var consumer = sp.GetRequiredService<Consumer>();
consumer.Inject(sp);
Console.WriteLine(consumer.Run());

var propConsumer = sp.GetRequiredService<PropConsumer>();
propConsumer.Inject(sp);
Console.WriteLine(propConsumer.Service.Greet());

// Verify registered service
var svc = sp.GetRequiredService<IMyService>();
Console.WriteLine(svc.Greet());

Console.WriteLine("All OK!");

// ── Types ──────────────────────────────────────────────────────────
public interface IMyService
{
    string Greet();
}

[Singleton<IMyService>]
public partial class RegisteredService : IMyService
{
    public string Greet() => "from registrar";
}

[Injectable]
public partial class Consumer : IInjectable
{
    [Inject] private IMyService _service;

    public string Run() => _service.Greet();
}

[Injectable]
public partial class PropConsumer : IInjectable
{
    [Inject] public partial IMyService Service { get; }
}

[ServiceRegistrar]
public partial class MyRegistrar : IServiceRegistrar;
