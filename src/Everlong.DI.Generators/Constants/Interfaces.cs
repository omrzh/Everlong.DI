namespace Everlong.DI.Generators.Constants;

internal static class Interfaces
{
  internal const string IServiceProvider = "IServiceProvider";
  internal const string IServiceCollection = "IServiceCollection";
  internal const string IInjectable = "IInjectable";
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
