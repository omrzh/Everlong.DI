namespace Everlong.DI.Generators.Models;

internal sealed record Result<TValue>(TValue? Value, EquatableArray<DiagnosticInfo> Errors)
    where TValue : IEquatable<TValue>?;
