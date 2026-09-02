using Everlong.DI;

public partial class ServiceA : IInjectable
{
    [Inject] private IHelper _helper;

    public string Run() => _helper?.Help() ?? "no-helper";
}

public interface IHelper
{
    string Help();
}
