namespace Everlong.DI.Generators.Constants;

internal static class Interfaces
{
  internal const string IServiceProvider = "IServiceProvider";
  internal const string IServiceCollection = "IServiceCollection";

  internal const string IAutoInject = "IAutoInject";

  // Full metadata names used for symbol-level interface matching. Match the *full* name so
  // that a foreign type merely called "IInjectable" in another namespace never fools the
  // chain discovery into generating an override against a non-Everlong member.
  internal const string IInjectableFull = $"{Ns.DiNamespace}.IInjectable";
  internal const string IAutoInjectFull = $"{Ns.DiNamespace}.{IAutoInject}";
}

internal static class Methods
{
  internal const string TryAddTransient = "TryAddTransient";
  internal const string GetService = "GetService";
  internal const string GetRequiredService = "GetRequiredService";
  internal const string GetKeyedService = "GetKeyedService";
  internal const string GetRequiredKeyedService = "GetRequiredKeyedService";
  internal const string Inject = "Inject";
}
