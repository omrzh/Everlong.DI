#pragma warning disable CS8603, CS8777

using System.Diagnostics.CodeAnalysis;

namespace Everlong.DI.Generators.Helpers;

internal static class Guard
{
  public static T NotNull<T>([NotNull] T? value, string? paramName = null) where T : class
    => value ?? throw new ArgumentNullException(paramName);

  public static string NotNullOrEmpty([NotNull] string? value, string? paramName = null)
  {
    if (string.IsNullOrEmpty(value))
      throw new ArgumentException("String cannot be null or empty.", paramName);
    return value;
  }

  public static bool TryNotNull<T>(T? value, [NotNullWhen(true)] out T? result) where T : class
  {
    if (value is null) { result = null; return false; }
    result = value;
    return true;
  }

  public static bool TryNotNullOrEmpty(string? value, [NotNullWhen(true)] out string? result)
  {
    if (string.IsNullOrEmpty(value)) { result = null; return false; }
    result = value!;
    return true;
  }
}
