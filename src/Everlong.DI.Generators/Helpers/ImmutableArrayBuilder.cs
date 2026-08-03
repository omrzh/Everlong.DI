using System.Collections;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Everlong.DI.Generators.Helpers;

internal ref struct ImmutableArrayBuilder<T>
{
  private Writer? _writer;

  public static ImmutableArrayBuilder<T> Rent() => new(new Writer());

  private ImmutableArrayBuilder(Writer writer) => this._writer = writer;

  public readonly int Count
  {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    get => this._writer!.Count;
  }

  [UnscopedRef]
  public readonly ReadOnlySpan<T> WrittenSpan
  {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    get => this._writer!.WrittenSpan;
  }

  public readonly void Add(T item) => this._writer!.Add(item);

  public readonly void AddRange(scoped ReadOnlySpan<T> items) => this._writer!.AddRange(items);

  public readonly ImmutableArray<T> ToImmutable()
  {
    T[] array = this._writer!.WrittenSpan.ToArray();
    return Unsafe.As<T[], ImmutableArray<T>>(ref array);
  }

  public readonly T[] ToArray() => this._writer!.WrittenSpan.ToArray();

  public readonly IEnumerable<T> AsEnumerable() => this._writer!;

  public override readonly string ToString() => this._writer!.WrittenSpan.ToString();

  public void Dispose()
  {
    Writer? writer = this._writer;
    this._writer = null;
    writer?.Dispose();
  }

  private sealed class Writer : ICollection<T>, IDisposable
  {
    private T?[]? array;
    private int index;

    public Writer()
    {
      this.array = ArrayPool<T?>.Shared.Rent(typeof(T) == typeof(char) ? 1024 : 8);
      this.index = 0;
    }

    public int Count
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get => this.index;
    }

    public ReadOnlySpan<T> WrittenSpan
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get => new(this.array!, 0, this.index);
    }

    bool ICollection<T>.IsReadOnly => true;

    public void Add(T value)
    {
      EnsureCapacity(1);
      this.array![this.index++] = value;
    }

    public void AddRange(ReadOnlySpan<T> items)
    {
      EnsureCapacity(items.Length);
      items.CopyTo(this.array.AsSpan(this.index)!);
      this.index += items.Length;
    }

    public void Dispose()
    {
      T?[]? array = this.array;
      this.array = null;
      if (array is not null)
        ArrayPool<T?>.Shared.Return(array, clearArray: typeof(T) != typeof(char));
    }

    void ICollection<T>.Clear() => throw new NotSupportedException();
    bool ICollection<T>.Contains(T item) => throw new NotSupportedException();
    void ICollection<T>.CopyTo(T[] array, int arrayIndex) => Array.Copy(this.array!, 0, array, arrayIndex, this.index);

    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
      T?[] array = this.array!;
      int length = this.index;
      for (int i = 0; i < length; i++)
        yield return array[i]!;
    }

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<T>)this).GetEnumerator();
    bool ICollection<T>.Remove(T item) => throw new NotSupportedException();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureCapacity(int requestedSize)
    {
      if (requestedSize > this.array!.Length - this.index)
        ResizeBuffer(requestedSize);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ResizeBuffer(int sizeHint)
    {
      int minimumSize = this.index + sizeHint;
      T?[] oldArray = this.array!;
      T?[] newArray = ArrayPool<T?>.Shared.Rent(minimumSize);
      Array.Copy(oldArray, newArray, this.index);
      this.array = newArray;
      ArrayPool<T?>.Shared.Return(oldArray, clearArray: typeof(T) != typeof(char));
    }
  }
}
