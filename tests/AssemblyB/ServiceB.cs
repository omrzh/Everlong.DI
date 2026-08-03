using Everlong.DI;

[Injectable]
public partial class ServiceB : IInjectable
{
    [Inject] public partial IHelper Helper { get; }
}
