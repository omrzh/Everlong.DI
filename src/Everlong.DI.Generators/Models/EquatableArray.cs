using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Everlong.DI.Generators.Models;

internal static class EquatableArrayExtensions
{
  internal static EquatableArray<T> ToEquatableArray<T>(this IEnumerable<T> source)
    where T : IEquatable<T>
    => new(source.ToImmutableArray());

  internal static EquatableArray<T> AsEquatable<T>(this ImmutableArray<T> source)
    where T : IEquatable<T>
    => new(source);

  public static EquatableArray<T> AsEquatableArray<T>(this ImmutableArray<T> array)
    where T : IEquatable<T> => new(array);
}

internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
  where T : IEquatable<T>
{
  private readonly T[]? _array;

  public EquatableArray(ImmutableArray<T> array)
  {
    if (array.IsDefault)
    {
      array = ImmutableArray<T>.Empty;
    }
    this._array = Unsafe.As<ImmutableArray<T>, T[]?>(ref array);
  }

  public ref readonly T this[int index]
  {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    get => ref AsImmutableArray().ItemRef(index);
  }

  public bool IsEmpty
  {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    get => AsImmutableArray().IsEmpty;
  }

  public int Length => _array?.Length ?? 0;

  public bool Equals(EquatableArray<T> array) => AsSpan().SequenceEqual(array.AsSpan());

  public override bool Equals([NotNullWhen(true)] object? obj) => obj is EquatableArray<T> array && Equals(array);

  public override int GetHashCode()
  {
    if (this._array == null) return 0;
    HashCode hashCode = default;
    foreach (T item in _array) hashCode.Add(item);
    return hashCode.ToHashCode();
  }

  public static readonly EquatableArray<T> Empty = new(ImmutableArray<T>.Empty);

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public ImmutableArray<T> AsImmutableArray()
  {
    if (this._array is not T[] array) return ImmutableArray<T>.Empty;
    return Unsafe.As<T[], ImmutableArray<T>>(ref array);
  }

  public static EquatableArray<T> FromImmutableArray(ImmutableArray<T> array) => new(array);

  public ReadOnlySpan<T> AsSpan() => AsImmutableArray().AsSpan();
  public T[] ToArray() => AsImmutableArray().ToArray();
  public ImmutableArray<T>.Enumerator GetEnumerator() => AsImmutableArray().GetEnumerator();

  IEnumerator<T> IEnumerable<T>.GetEnumerator() => ((IEnumerable<T>)AsImmutableArray()).GetEnumerator();
  IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)AsImmutableArray()).GetEnumerator();

  public static implicit operator EquatableArray<T>(ImmutableArray<T> array) => FromImmutableArray(array);
  public static implicit operator ImmutableArray<T>(EquatableArray<T> array) => array.AsImmutableArray();
  public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right) => left.Equals(right);
  public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right) => !left.Equals(right);
}
