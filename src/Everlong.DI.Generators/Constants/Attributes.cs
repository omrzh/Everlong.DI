namespace Everlong.DI.Generators.Constants;

internal static class Attributes
{
  internal const string InjectFull = $"{Ns.DiNamespace}.InjectAttribute";
  internal const string InjectableFull = $"{Ns.DiNamespace}.InjectableAttribute";

  internal const string SingletonFull = $"{Ns.DiNamespace}.SingletonAttribute";
  internal const string SingletonGenericFull = $"{Ns.DiNamespace}.SingletonAttribute`1";
  internal const string TransientFull = $"{Ns.DiNamespace}.TransientAttribute";
  internal const string TransientGenericFull = $"{Ns.DiNamespace}.TransientAttribute`1";
  internal const string ScopedFull = $"{Ns.DiNamespace}.ScopedAttribute";
  internal const string ScopedGenericFull = $"{Ns.DiNamespace}.ScopedAttribute`1";
  internal const string AlsoAsFull = $"{Ns.DiNamespace}.AlsoAsAttribute`1";

  internal const string ServiceRegistrar = "ServiceRegistrarAttribute";
  internal const string ServiceRegistrarFull = $"{Ns.DiNamespace}.{ServiceRegistrar}";

  internal const string EditorBrowsable = "global::System.ComponentModel.EditorBrowsable";
}
