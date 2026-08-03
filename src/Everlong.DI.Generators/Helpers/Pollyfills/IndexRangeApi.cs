namespace System
{
  internal struct Index
  {
    private readonly int _value;

    public Index(int value, bool fromEnd = false)
    {
      _value = fromEnd ? ~value : value;
    }

    public int GetOffset(int length)
    {
      int offset = _value;
      if (_value < 0) offset += length + 1;
      return offset;
    }

    public static implicit operator Index(int value) => new(value);
  }

  internal struct Range
  {
    public Index Start { get; }
    public Index End { get; }

    public Range(Index start, Index end)
    {
      Start = start;
      End = end;
    }
  }
}
