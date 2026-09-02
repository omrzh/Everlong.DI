using Everlong.DI;

public partial class ServiceB : IInjectable
{
    [Inject] public partial IHelper Helper { get; }
}
